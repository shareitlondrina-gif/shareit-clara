// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Dominio.Contratos;

/// <summary>
/// Objetivo: apresentar tempo de uso de aplicativos e classificação configurável por tenant.
/// Origem: períodos de atividade já coletados pelo processo de sessão.
/// Destino: dashboard de Aplicativos e futura composição do Índice Geral.
/// Dependências: periodos_atividade e regras_aplicativos.
/// Impacto: transforma telemetria de processo em contexto de trabalho sem ler conteúdo de tela.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record PainelAplicativos(
    DateTimeOffset InicioPeriodoUtc,
    IReadOnlyCollection<ResumoAplicativoDetalhado> Aplicativos);

public sealed record ResumoAplicativoDetalhado(
    string ProcessoExecutavel,
    long Segundos,
    int Estacoes,
    string Classificacao,
    string? Categoria);
