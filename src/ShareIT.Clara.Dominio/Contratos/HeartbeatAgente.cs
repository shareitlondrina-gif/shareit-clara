namespace ShareIT.Clara.Dominio.Contratos;

public sealed record HeartbeatAgente(
    string VersaoAgente,
    InventarioEquipamento Inventario,
    string? ProcessoAtual,
    DateTimeOffset MomentoUtc);
