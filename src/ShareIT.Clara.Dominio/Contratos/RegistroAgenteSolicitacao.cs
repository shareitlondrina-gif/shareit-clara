namespace ShareIT.Clara.Dominio.Contratos;

public sealed record RegistroAgenteSolicitacao(
    string TokenInstalacao,
    string Hostname,
    string? UsuarioWindows,
    string SistemaOperacional,
    string VersaoAgente,
    string? Fabricante,
    string? Modelo);

public sealed record RegistroAgenteResposta(
    Guid IdentificadorAgente,
    string TokenDispositivo,
    Guid AtivoId,
    Guid TenantId);
