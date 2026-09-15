using System.Diagnostics;

namespace ShareIT.Clara.Agente.Servicos;

/// <summary>
/// Objetivo: restaurar o processo de sessão quando ele encerrar inesperadamente enquanto houver sessão interativa.
/// Origem: serviço Windows ShareIT.Clara.Agente.
/// Destino: tarefa agendada "Share IT .Clara - Sessao".
/// Impacto: reduz lacunas acidentais de telemetria sem alterar a política/período de monitoramento.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServicoVigilanciaSessao(ILogger<ServicoVigilanciaSessao> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (OperatingSystem.IsWindows() && HaSessaoInterativa() && Process.GetProcessesByName("ShareIT.Clara.Sessao").Length == 0)
                {
                    ExecutarTarefaSessao();
                    logger.LogInformation("Watchdog solicitou reinício do processo ShareIT.Clara.Sessao.");
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Watchdog da sessão não conseguiu avaliar/reiniciar o processo nesta tentativa.");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static bool HaSessaoInterativa()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("query.exe", "user")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            });
            if (p is null) return false;
            var saida = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return p.ExitCode == 0 && !string.IsNullOrWhiteSpace(saida) &&
                (saida.Contains("Active", StringComparison.OrdinalIgnoreCase) || saida.Contains("Ativo", StringComparison.OrdinalIgnoreCase));
        }
        catch { return Environment.UserInteractive; }
    }

    private static void ExecutarTarefaSessao()
    {
        using var p = Process.Start(new ProcessStartInfo("schtasks.exe", "/Run /TN \"Share IT .Clara - Sessao\"")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        });
        p?.WaitForExit(10000);
    }
}
