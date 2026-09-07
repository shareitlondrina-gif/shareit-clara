using System.Diagnostics;
using System.Runtime.InteropServices;
using ShareIT.Clara.Dominio.Enumeracoes;

namespace ShareIT.Clara.Sessao.Windows;

/// <summary>
/// Objetivo: observar presença/ociosidade e aplicativo em primeiro plano sem capturar conteúdo digitado.
/// Origem: APIs nativas do Windows e processo em primeiro plano.
/// Destino: EventoSessao enviado ao agente.
/// Dependências: user32.dll.
/// Impacto: fornece telemetria mínima de performance preservando o limite explícito contra keylogger.
/// Desenvolvedor: Paulo Andrade
/// </summary>
internal static partial class LeitorAtividadeWindows
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    public static (EstadoAtividade Estado, string? Processo) Obter(int limiarOciosidadeMinutos)
    {
        if (EstaBloqueado()) return (EstadoAtividade.Bloqueado, null);

        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        var ocioso = false;
        if (GetLastInputInfo(ref info))
        {
            var agora = Environment.TickCount64;
            var ultimo = info.dwTime;
            var diferencaMs = agora >= ultimo ? agora - ultimo : 0;
            ocioso = diferencaMs >= TimeSpan.FromMinutes(Math.Max(1, limiarOciosidadeMinutos)).TotalMilliseconds;
        }

        return (ocioso ? EstadoAtividade.Ocioso : EstadoAtividade.Ativo, ObterProcessoPrimeiroPlano());
    }

    private static bool EstaBloqueado()
    {
        try
        {
            return Process.GetProcessesByName("LogonUI").Length > 0 || Process.GetProcessesByName("LockApp").Length > 0;
        }
        catch { return false; }
    }

    private static string? ObterProcessoPrimeiroPlano()
    {
        try
        {
            var janela = GetForegroundWindow();
            if (janela == nint.Zero) return null;
            _ = GetWindowThreadProcessId(janela, out var processoId);
            return processoId == 0 ? null : Process.GetProcessById((int)processoId).ProcessName;
        }
        catch { return null; }
    }
}
