namespace ShareIT.Clara.Dominio.Contratos;

public sealed record RegistroAgenteSolicitacao(
    string TokenInstalacao,
    string VersaoAgente,
    InventarioEquipamento Inventario);

public sealed record RegistroAgenteResposta(
    Guid IdentificadorAgente,
    string TokenDispositivo,
    Guid AtivoId,
    Guid TenantId);

public sealed record AutorizacaoManutencaoSolicitacao(string Codigo, string Operacao);
