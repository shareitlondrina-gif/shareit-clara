namespace ShareIT.Clara.Dominio.Contratos;

public sealed record StatusAtualizacaoAgente(
    string VersaoOrigem,
    string VersaoDestino,
    string Status,
    string? Detalhe,
    DateTimeOffset MomentoUtc);
