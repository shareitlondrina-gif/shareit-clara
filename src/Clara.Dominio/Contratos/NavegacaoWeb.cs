// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using Clara.Dominio.Modelos;

namespace Clara.Dominio.Contratos;

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
