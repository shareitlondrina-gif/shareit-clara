namespace ShareIT.Clara.Dominio.Contratos;

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
    DateTimeOffset AtualizadoEmUtc);

/// <summary>
/// Objetivo: enviar ao agente apenas os parâmetros necessários para a coleta autorizada.
/// Origem: configuracoes_monitoramento do tenant autenticado pelo agente.
/// Destino: configuração local em ProgramData e processo ShareIT.Clara.Sessao.
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
    DateTimeOffset AtualizadaEmUtc);
