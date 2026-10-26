// Clara
// Programador: Paulo Andrade
// Objetivo: executar uma regra de aplicação da Clara.
// Origem: controladores, configuração e serviços hospedados.
// Destino: repositórios, integrações ou eventos da aplicação.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using Clara.Agente.Comunicacao;
using Clara.Agente.Configuracao;
using Clara.Agente.Identidade;
using Clara.Agente.Telemetria;

namespace Clara.Agente.Servicos;

/// <summary>
/// Objetivo: manter identidade, heartbeat e sincronização de telemetria do agente.
/// Origem: ciclo de vida do Windows Service.
/// Destino: API Clara.
/// Dependências: ClienteApiClara, fila offline e identidade local.
/// Impacto: garante continuidade da coleta mesmo com indisponibilidade temporária da rede.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServicoAgente(
    ClienteApiClara api,
    ConfiguracaoAgenteLocal configuracao,
    GerenciadorIdentidadeAgente identidade,
    FilaOffline fila,
    FilaNavegacao filaNavegacao,
    ILogger<ServicoAgente> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await GarantirRegistroAsync(stoppingToken);
        var proximoHeartbeat = DateTimeOffset.MinValue;
        var proximaSincronizacao = DateTimeOffset.MinValue;
        var proximaPolitica = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var agora = DateTimeOffset.UtcNow;
                if (agora >= proximaPolitica)
                {
                    var politica = await api.ConsultarPoliticaAsync(stoppingToken);
                    if (politica is not null)
                    {
                        configuracao.AplicarPolitica(politica);
                        configuracao.Salvar();
                    }
                    proximaPolitica = agora.AddMinutes(5);
                }

                if (agora >= proximoHeartbeat)
                {
                    await api.EnviarHeartbeatAsync(stoppingToken);
                    proximoHeartbeat = agora.AddSeconds(Math.Max(15, configuracao.IntervaloHeartbeatSegundos));
                }

                if (agora >= proximaSincronizacao)
                {
                    await SincronizarFilaAsync(stoppingToken);
                    await SincronizarNavegacaoAsync(stoppingToken);
                    proximaSincronizacao = agora.AddSeconds(Math.Max(10, configuracao.IntervaloSincronizacaoSegundos));
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "Falha temporária no ciclo do agente."); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task GarantirRegistroAsync(CancellationToken ct)
    {
        if (identidade.Atual is not null) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var resposta = await api.RegistrarAsync(ct);
                if (resposta is not null)
                {
                    identidade.Salvar(new IdentidadeAgente(resposta.IdentificadorAgente, resposta.TokenDispositivo, resposta.AtivoId, resposta.TenantId));
                    configuracao.SalvarSemTokenInstalacao();
                    logger.LogInformation("Agente registrado com sucesso: {Agente}", resposta.IdentificadorAgente);
                    return;
                }
                logger.LogWarning("Agente ainda não registrado. Verifique o token temporário de instalação.");
            }
            catch (Exception ex) { logger.LogWarning(ex, "Falha ao registrar agente."); }
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    private async Task SincronizarFilaAsync(CancellationToken ct)
    {
        var lote = await fila.RetirarLoteAsync(200, ct);
        if (lote.Count == 0) return;
        try
        {
            if (!await api.EnviarPeriodosAsync(lote, ct)) await fila.RecolocarNoInicioAsync(lote, ct);
        }
        catch
        {
            await fila.RecolocarNoInicioAsync(lote, ct);
            throw;
        }
    }
    private async Task SincronizarNavegacaoAsync(CancellationToken ct)
    {
        if (!configuracao.MonitorarUrlsDominios) return;
        var lote = await filaNavegacao.RetirarLoteAsync(200, ct);
        if (lote.Count == 0) return;
        try
        {
            if (!await api.EnviarNavegacaoAsync(lote, ct)) await filaNavegacao.RecolocarNoInicioAsync(lote, ct);
        }
        catch
        {
            await filaNavegacao.RecolocarNoInicioAsync(lote, ct);
            throw;
        }
    }

}
