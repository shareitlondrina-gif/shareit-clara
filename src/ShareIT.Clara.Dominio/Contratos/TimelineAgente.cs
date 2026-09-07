namespace ShareIT.Clara.Dominio.Contratos;

public sealed record TimelineAgente(
    Guid AgenteId,
    string Hostname,
    string? Responsavel,
    IReadOnlyCollection<LinhaTimeline> Periodos);

public sealed record LinhaTimeline(
    DateTimeOffset InicioUtc,
    DateTimeOffset FimUtc,
    string Estado,
    string? ProcessoExecutavel,
    string UsuarioWindows);
