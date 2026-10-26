// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Dominio.Contratos;

/// <summary>
/// Objetivo: representar a política de monitoramento configurada por tenant.
/// Origem: Administração > Configurações.
/// Destino: PostgreSQL, API de política e agente Windows.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record ConfiguracoesMonitoramento(
    bool PermitirClassificacaoWeb,
    bool MonitorarUrlsDominios,
    bool UsarCategoriasWeb,
    int TempoMinimoOciosidadeMinutos,
    int BaixaContinuidadeMinutos,
    int LimiarAlternanciaAplicativos,
    string? RedesConfiaveisCidr,
    bool ConsiderarVpnEscritorio,
    bool ClassificarIpPublico,
    bool CapturaScreenshots,
    int IntervaloCapturaMinutos,
    int RetencaoEvidenciasDias,
    bool AnonimizacaoParcial,
    string HorarioInicio,
    string HorarioFim,
    string DiasMonitorados,
    int IntervaloHeartbeatSegundos,
    int TempoOfflineMinutos,
    bool AlertaEstacaoOffline,
    bool NotificarPorEmail,
    string? DestinatariosAlertas,
    DateTimeOffset AtualizadoEmUtc,
    string JornadaInicio,
    string JornadaFim,
    int CargaHorariaDiariaMinutos,
    int MargemAntesMinutos,
    int MargemDepoisMinutos,
    string DiasJornada,
    string FusoHorario);

/// <summary>
/// Objetivo: enviar ao agente apenas os parâmetros necessários para a coleta autorizada.
/// HorarioInicio/HorarioFim representam o PERÍODO DE MONITORAMENTO efetivo; fora dele o
/// processo de sessão não coleta telemetria. A jornada de referência é usada somente no servidor.
/// Origem: configuracoes_monitoramento + eventual sobrescrita da estação.
/// Destino: configuração local em ProgramData e processa Clara.Sessao.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record PoliticaMonitoramentoAgente(
    int IntervaloHeartbeatSegundos,
    int TempoOfflineMinutos,
    int LimiarOciosidadeMinutos,
    string HorarioInicio,
    string HorarioFim,
    string DiasMonitorados,
    bool PermitirClassificacaoWeb,
    bool MonitorarUrlsDominios,
    bool UsarCategoriasWeb,
    bool CapturaScreenshots,
    int IntervaloCapturaMinutos,
    int RetencaoEvidenciasDias,
    bool AnonimizacaoParcial,
    IReadOnlyCollection<string> AplicativosExcluidos,
    IReadOnlyCollection<string> DominiosExcluidos,
    string FusoHorario,
    DateTimeOffset AtualizadaEmUtc);
