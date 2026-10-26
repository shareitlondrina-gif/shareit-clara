// Clara
// Programador: Paulo Andrade
// Objetivo: observar a sessão interativa autorizada.
// Origem: sessão Windows do usuário.
// Destino: agente Clara por canal local controlado.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Clara.Dominio.Enumeracoes;

namespace Clara.Sessao.Windows;

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
            // GetLastInputInfo trabalha com o contador de 32 bits do Windows.
            // A subtração uint intencionalmente preserva o comportamento correto mesmo após o wrap (~49,7 dias).
            var agora = unchecked((uint)Environment.TickCount);
            var diferencaMs = unchecked(agora - info.dwTime);
            ocioso = diferencaMs >= TimeSpan.FromMinutes(Math.Max(1, limiarOciosidadeMinutos)).TotalMilliseconds;
        }

        var processo = ObterProcessoPrimeiroPlano();
        if (!ocioso && EhAplicativoReuniao(processo))
            return (EstadoAtividade.Reuniao, processo);

        return (ocioso ? EstadoAtividade.Ocioso : EstadoAtividade.Ativo, processo);
    }

    private static bool EstaBloqueado()
    {
        try
        {
            return Process.GetProcessesByName("LogonUI").Length > 0 || Process.GetProcessesByName("LockApp").Length > 0;
        }
        catch { return false; }
    }

    private static bool EhAplicativoReuniao(string? processo)
    {
        if (string.IsNullOrWhiteSpace(processo)) return false;
        return processo.Equals("ms-teams", StringComparison.OrdinalIgnoreCase)
            || processo.Equals("teams", StringComparison.OrdinalIgnoreCase)
            || processo.Equals("zoom", StringComparison.OrdinalIgnoreCase)
            || processo.Equals("zoomhost", StringComparison.OrdinalIgnoreCase)
            || processo.Equals("webexmta", StringComparison.OrdinalIgnoreCase)
            || processo.Equals("webex", StringComparison.OrdinalIgnoreCase);
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
