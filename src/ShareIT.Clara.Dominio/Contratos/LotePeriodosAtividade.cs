using ShareIT.Clara.Dominio.Modelos;

namespace ShareIT.Clara.Dominio.Contratos;

public sealed record LotePeriodosAtividade(IReadOnlyCollection<PeriodoAtividade> Periodos);
