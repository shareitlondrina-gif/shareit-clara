namespace ShareIT.Clara.Agente.Identidade;

public sealed record IdentidadeAgente(Guid IdentificadorAgente, string TokenDispositivo, Guid AtivoId, Guid TenantId);
