namespace ShareIT.Clara.Dominio.Contratos;

public sealed record DashboardAgora(
    int TotalAgentes,
    int Online,
    int Offline,
    int Ativos,
    int Ociosos,
    int Bloqueados,
    IReadOnlyCollection<LinhaAgenteAgora> Agentes);

public sealed record LinhaAgenteAgora(
    Guid AgenteId,
    string Hostname,
    string? Responsavel,
    string Status,
    string? EstadoAtual,
    string Versao,
    DateTimeOffset? UltimaComunicacaoUtc);
