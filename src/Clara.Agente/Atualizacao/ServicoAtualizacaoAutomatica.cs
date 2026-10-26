// Clara
// Programador: Paulo Andrade
// Objetivo: executar coleta e comunicação do agente Windows.
// Origem: Windows, configuração local e API Clara.
// Destino: API Clara, telemetria autorizada e estado local.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Diagnostics;
using System.Text.Json;
using Clara.Agente.Comunicacao;
using Clara.Agente.Configuracao;
using Clara.Agente.Identidade;
using Clara.Comum.Seguranca;
using Clara.Comum.Versoes;
using Clara.Dominio.Contratos;

namespace Clara.Agente.Atualizacao;

/// <summary>
/// Objetivo: consultar, baixar, validar e iniciar automaticamente a atualização do agente.
/// Origem: manifesto publicado pelo servidor Clara.
/// Destino: Clara.Atualizador executado fora da pasta instalada.
/// Dependências: ClienteApiClara, SHA-256 e identidade do dispositivo.
/// Impacto: mantém o parque atualizado sem intervenção manual e preserva rollback em falhas.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServicoAtualizacaoAutomatica(
    ClienteApiClara api,
    ConfiguracaoAgenteLocal configuracao,
    GerenciadorIdentidadeAgente identidade,
    ILogger<ServicoAtualizacaoAutomatica> logger) : BackgroundService
{
    private readonly string _pastaAtualizacoes = Path.Combine(ConfiguracaoAgenteLocal.DiretorioDados, "atualizacoes");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(_pastaAtualizacoes);
        LimparArtefatosAntigos();
        await ReportarResultadoPendenteAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (identidade.Atual is not null)
                    await VerificarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogWarning(ex, "Falha ao verificar atualização automática."); }

            await Task.Delay(TimeSpan.FromMinutes(Math.Max(5, configuracao.IntervaloAtualizacaoMinutos)), stoppingToken);
        }
    }

    private async Task VerificarAsync(CancellationToken ct)
    {
        var manifesto = await api.ConsultarAtualizacaoAsync(ct);
        if (manifesto is null || !ComparadorVersao.HaVersaoMaisNova(api.VersaoAtual, manifesto.Versao)) return;

        if (AtualizacaoVisaoEmAndamento())
        {
            logger.LogInformation("Atualização da Clara adiada: foi detectada manutenção/atualização recente do VISÃO na mesma estação.");
            return;
        }

        var pastaVersao = Path.Combine(_pastaAtualizacoes, manifesto.Versao);
        Directory.CreateDirectory(pastaVersao);
        var pacote = Path.Combine(pastaVersao, "Clara-Agent.zip");

        logger.LogInformation("Atualização {Atual} -> {Nova} encontrada.", api.VersaoAtual, manifesto.Versao);
        await api.RegistrarStatusAtualizacaoAsync(new(api.VersaoAtual, manifesto.Versao, "DownloadIniciado", null, DateTimeOffset.UtcNow), ct);

        await api.BaixarPacoteAtualizacaoAsync(manifesto.UrlPacote, pacote, ct);

        var hash = await HashSeguro.Sha256ArquivoAsync(pacote, ct);
        if (!string.Equals(hash, manifesto.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            await api.RegistrarStatusAtualizacaoAsync(new(api.VersaoAtual, manifesto.Versao, "HashInvalido", $"Esperado {manifesto.Sha256}; recebido {hash}", DateTimeOffset.UtcNow), ct);
            File.Delete(pacote);
            return;
        }

        await api.RegistrarStatusAtualizacaoAsync(new(api.VersaoAtual, manifesto.Versao, "PacoteValidado", null, DateTimeOffset.UtcNow), ct);
        IniciarAtualizador(pacote, manifesto.Versao);
    }

    private void IniciarAtualizador(string pacote, string versaoDestino)
    {
        var raizInstalacao = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName
            ?? throw new InvalidOperationException("Não foi possível determinar a raiz de instalação.");
        var origemAtualizador = Path.Combine(raizInstalacao, "Atualizador");
        if (!Directory.Exists(origemAtualizador)) throw new DirectoryNotFoundException($"Atualizador não encontrado em {origemAtualizador}");

        var temp = Path.Combine(_pastaAtualizacoes, "executor-" + Guid.NewGuid().ToString("N"));
        CopiarDiretorio(origemAtualizador, temp);
        var exe = Path.Combine(temp, "Clara.Atualizador.exe");
        var backup = Path.Combine(ConfiguracaoAgenteLocal.DiretorioDados, "backup");

        var argumentos = string.Join(" ", new[]
        {
            "--pacote", Q(pacote),
            "--destino", Q(raizInstalacao),
            "--backup", Q(backup),
            "--servico", Q("Clara.Agente"),
            "--processo-sessao", Q("Clara.Sessao"),
            "--tarefa-sessao", Q("Clara - Sessao"),
            "--versao-origem", Q(api.VersaoAtual),
            "--versao-destino", Q(versaoDestino)
        });

        Process.Start(new ProcessStartInfo(exe, argumentos)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = temp
        });

        logger.LogInformation("Atualizador externo iniciado para a versão {Versao}.", versaoDestino);
    }


    private static bool AtualizacaoVisaoEmAndamento()
    {
        try
        {
            var raiz = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Clara", "Visao", "Updates");
            if (!Directory.Exists(raiz)) return false;
            var limite = DateTime.UtcNow.AddMinutes(-20);
            return Directory.GetDirectories(raiz).Any(p => Directory.GetLastWriteTimeUtc(p) >= limite);
        }
        catch { return false; }
    }

    private void LimparArtefatosAntigos()
    {
        try
        {
            var limite = DateTime.UtcNow.AddDays(-14);
            foreach (var pasta in Directory.GetDirectories(_pastaAtualizacoes))
            {
                var nome = Path.GetFileName(pasta);
                if ((nome.StartsWith("executor-", StringComparison.OrdinalIgnoreCase) || char.IsDigit(nome.FirstOrDefault()))
                    && Directory.GetLastWriteTimeUtc(pasta) < limite)
                {
                    try { Directory.Delete(pasta, true); } catch { }
                }
            }

            var backup = Path.Combine(ConfiguracaoAgenteLocal.DiretorioDados, "backup");
            if (Directory.Exists(backup))
            {
                foreach (var pasta in Directory.GetDirectories(backup).OrderByDescending(Directory.GetLastWriteTimeUtc).Skip(3))
                    try { Directory.Delete(pasta, true); } catch { }
            }
        }
        catch (Exception ex) { logger.LogDebug(ex, "Limpeza de artefatos antigos de atualização não concluída."); }
    }

    private async Task ReportarResultadoPendenteAsync(CancellationToken ct)
    {
        var arquivo = Path.Combine(_pastaAtualizacoes, "resultado-atualizacao.json");
        if (!File.Exists(arquivo) || identidade.Atual is null) return;
        try
        {
            var status = JsonSerializer.Deserialize<StatusAtualizacaoAgente>(await File.ReadAllTextAsync(arquivo, ct), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (status is not null) await api.RegistrarStatusAtualizacaoAsync(status, ct);
            File.Delete(arquivo);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Não foi possível reportar o resultado da última atualização."); }
    }

    private static void CopiarDiretorio(string origem, string destino)
    {
        Directory.CreateDirectory(destino);
        foreach (var arquivo in Directory.GetFiles(origem)) File.Copy(arquivo, Path.Combine(destino, Path.GetFileName(arquivo)), true);
        foreach (var pasta in Directory.GetDirectories(origem)) CopiarDiretorio(pasta, Path.Combine(destino, Path.GetFileName(pasta)));
    }

    private static string Q(string valor) => "\"" + valor.Replace("\"", "\\\"") + "\"";
}
