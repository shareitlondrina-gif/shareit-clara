namespace ShareIT.Clara.Dominio.Contratos;

public sealed record HeartbeatAgente(
    string VersaoAgente,
    string Hostname,
    string? UsuarioWindows,
    DateTimeOffset MomentoUtc);
