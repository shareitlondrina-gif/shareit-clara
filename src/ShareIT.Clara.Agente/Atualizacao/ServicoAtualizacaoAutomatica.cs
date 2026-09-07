using System.Diagnostics;
using System.Text.Json;
using ShareIT.Clara.Agente.Comunicacao;
using ShareIT.Clara.Agente.Configuracao;
using ShareIT.Clara.Agente.Identidade;
using ShareIT.Clara.Comum.Seguranca;
using ShareIT.Clara.Comum.Versoes;
using ShareIT.Clara.Dominio.Contratos;

namespace ShareIT.Clara.Agente.Atualizacao;

/// <summary>
/// Objetivo: consultar, baixar, validar e iniciar automaticamente a atualização do agente.
/// Origem: manifesto publicado pelo servidor Share IT .Clara.
/// Destino: ShareIT.Clara.Atualizador executado fora da pasta instalada.
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
        var exe = Path.Combine(temp, "ShareIT.Clara.Atualizador.exe");
        var backup = Path.Combine(ConfiguracaoAgenteLocal.DiretorioDados, "backup");

        var argumentos = string.Join(" ", new[]
        {
            "--pacote", Q(pacote),
            "--destino", Q(raizInstalacao),
            "--backup", Q(backup),
            "--servico", Q("ShareIT.Clara.Agente"),
            "--processo-sessao", Q("ShareIT.Clara.Sessao"),
            "--tarefa-sessao", Q("Share IT .Clara - Sessao"),
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
