// Clara
// Programador: Paulo Andrade
// Objetivo: executar coleta e comunicação do agente Windows.
// Origem: Windows, configuração local e API Clara.
// Destino: API Clara, telemetria autorizada e estado local.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.IO.Pipes;
using System.Text.Json;
using Clara.Dominio.Modelos;
using Clara.Agente.Configuracao;

namespace Clara.Agente.Telemetria;

/// <summary>
/// Objetivo: receber e agregar telemetria da sessão interativa sem expor porta TCP local.
/// Origem: Clara.Sessao.
/// Destino: fila offline do agente.
/// Dependências: Named Pipes do Windows.
/// Impacto: separa corretamente o Windows Service da sessão e reduz o volume para períodos de até um minuto ou por mudança de contexto.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServidorPipeSessao(FilaOffline fila, ConfiguracaoAgenteLocal configuracao, ILogger<ServidorPipeSessao> logger) : BackgroundService
{
    public const string NomePipe = "Clara.Sessao";
    private EventoSessao? _amostraInicialPeriodo;
    private EventoSessao? _ultimaAmostra;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(NomePipe, PipeDirection.In, 5, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(stoppingToken);
                using var leitor = new StreamReader(pipe);
                var linha = await leitor.ReadLineAsync(stoppingToken);
                if (string.IsNullOrWhiteSpace(linha)) continue;
                var evento = JsonSerializer.Deserialize<EventoSessao>(linha, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (evento is null) continue;

                // Fora do período efetivo de monitoramento não agregamos nem persistimos
                // atividade da sessão. Heartbeat/inventário técnico permanecem independentes.
                if (!configuracao.EstaDentroPeriodoMonitoramento(evento.MomentoUtc))
                {
                    _amostraInicialPeriodo = null;
                    _ultimaAmostra = null;
                    continue;
                }

                // Exclusão de privacidade não elimina o fato de atividade/ociosidade; apenas remove
                // o contexto do aplicativo excluído antes que ele entre na agregação/fila.
                if (configuracao.AplicativoEstaExcluido(evento.ProcessoExecutavel))
                    evento = evento with { ProcessoExecutavel = null };

                await ProcessarAmostraAsync(evento, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao receber evento da sessão.");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task ProcessarAmostraAsync(EventoSessao atual, CancellationToken ct)
    {
        if (_amostraInicialPeriodo is null)
        {
            _amostraInicialPeriodo = atual;
            _ultimaAmostra = atual;
            return;
        }

        if (_ultimaAmostra is not null && atual.MomentoUtc <= _ultimaAmostra.MomentoUtc) return;

        var inicio = _amostraInicialPeriodo;
        var mudouContexto = inicio.Estado != atual.Estado
            || !string.Equals(inicio.ProcessoExecutavel, atual.ProcessoExecutavel, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(inicio.UsuarioWindows, atual.UsuarioWindows, StringComparison.OrdinalIgnoreCase);
        var atingiuJanela = atual.MomentoUtc - inicio.MomentoUtc >= TimeSpan.FromMinutes(1);

        if (mudouContexto || atingiuJanela)
        {
            await fila.AdicionarAsync(new PeriodoAtividade(
                inicio.MomentoUtc,
                atual.MomentoUtc,
                inicio.UsuarioWindows,
                inicio.Estado,
                inicio.ProcessoExecutavel), ct);
            _amostraInicialPeriodo = atual;
        }

        _ultimaAmostra = atual;
    }
}
