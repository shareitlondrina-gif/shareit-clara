// Clara
// Programador: Paulo Andrade
// Objetivo: executar coleta e comunicação do agente Windows.
// Origem: Windows, configuração local e API Clara.
// Destino: API Clara, telemetria autorizada e estado local.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.IO.Pipes;
using System.Text.Json;
using Clara.Agente.Configuracao;
using Clara.Dominio.Modelos;

namespace Clara.Agente.Telemetria;

/// <summary>
/// Objetivo: receber do Native Messaging Host somente domínio/navegador e consolidar em períodos curtos.
/// Origem: Clara.NavegadorHost executado na sessão do usuário.
/// Destino: FilaNavegacao.
/// Dependências: Named Pipes do Windows e política sincronizada do tenant.
/// Impacto: habilita contexto web real apenas quando MonitorarUrlsDominios estiver autorizado.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServidorPipeNavegacao(
    FilaNavegacao fila,
    ConfiguracaoAgenteLocal configuracao,
    ILogger<ServidorPipeNavegacao> logger) : BackgroundService
{
    public const string NomePipe = "Clara.Navegador";
    private EventoNavegacao? _inicio;
    private EventoNavegacao? _ultimo;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(NomePipe, PipeDirection.In, 10, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(stoppingToken);
                using var leitor = new StreamReader(pipe);
                var linha = await leitor.ReadLineAsync(stoppingToken);
                if (string.IsNullOrWhiteSpace(linha)) continue;
                if (!configuracao.MonitorarUrlsDominios || !configuracao.EstaDentroPeriodoMonitoramento(DateTimeOffset.UtcNow))
                {
                    // Fora da janela efetiva de monitoramento não persistimos domínio nem continuidade da navegação.
                    _inicio = null;
                    _ultimo = null;
                    continue;
                }
                var evento = JsonSerializer.Deserialize<EventoNavegacao>(linha, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (evento is null || string.IsNullOrWhiteSpace(evento.Dominio)) continue;
                if (configuracao.DominioEstaExcluido(evento.Dominio))
                {
                    // O domínio excluído não deve formar continuidade nem ser persistido.
                    _inicio = null;
                    _ultimo = null;
                    continue;
                }
                await ProcessarAsync(evento, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao receber contexto web do navegador.");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task ProcessarAsync(EventoNavegacao atual, CancellationToken ct)
    {
        if (_inicio is null)
        {
            _inicio = atual;
            _ultimo = atual;
            return;
        }
        if (_ultimo is not null && atual.MomentoUtc <= _ultimo.MomentoUtc) return;

        var inicio = _inicio;
        var mudou = !string.Equals(inicio.Dominio, atual.Dominio, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(inicio.Navegador, atual.Navegador, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(inicio.UsuarioWindows, atual.UsuarioWindows, StringComparison.OrdinalIgnoreCase);
        var janela = atual.MomentoUtc - inicio.MomentoUtc >= TimeSpan.FromMinutes(1);
        if (mudou || janela)
        {
            var fim = atual.MomentoUtc;
            if (fim > inicio.MomentoUtc)
                await fila.AdicionarAsync(new PeriodoNavegacao(inicio.MomentoUtc, fim, inicio.UsuarioWindows, inicio.Dominio, inicio.Navegador), ct);
            _inicio = atual;
        }
        _ultimo = atual;
    }
}
