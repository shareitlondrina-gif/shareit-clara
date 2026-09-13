using ShareIT.Clara.Dominio.Modelos;

namespace ShareIT.Clara.Dominio.Contratos;

/// <summary>
/// Objetivo: transportar períodos de navegação agregados do agente para a API.
/// Origem: fila offline de navegação do agente Windows.
/// Destino: endpoint /api/v1/agentes/navegacao e PostgreSQL.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record LotePeriodosNavegacao(IReadOnlyCollection<PeriodoNavegacao> Periodos);

public sealed record ResumoSiteUtilizado(
    string Dominio,
    long Segundos,
    int Estacoes,
    string Classificacao,
    string? Categoria);

public sealed record RegraSiteResumo(
    Guid Id,
    string Dominio,
    string? Categoria,
    string Classificacao);

public sealed record PainelNavegacaoWeb(
    DateTimeOffset InicioPeriodoUtc,
    IReadOnlyCollection<ResumoSiteUtilizado> Sites,
    IReadOnlyCollection<RegraSiteResumo> Regras,
    int EstacoesComConector,
    DateTimeOffset? UltimoEventoUtc);
