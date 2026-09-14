namespace ShareIT.Clara.Dominio.Contratos;

/// <summary>
/// Objetivo: transportar oportunidades gerenciais explicáveis sem transformar telemetria em julgamento automático.
/// Origem: regras do tenant e dados de demonstração autorizados.
/// Destino: telas Agora, Oportunidades, detalhe da estação e drill-down.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record OportunidadeResumo(
    Guid Id,
    Guid? AtivoId,
    string? Hostname,
    string? Pessoa,
    string? Equipe,
    string Categoria,
    string EvidenciaResumida,
    string Impacto,
    string Recomendacao,
    string Status,
    DateTimeOffset DetectadaEmUtc);

public sealed record PainelOportunidades(
    IReadOnlyCollection<OportunidadeResumo> Oportunidades,
    IReadOnlyCollection<IndicadorOportunidade> Indicadores,
    int Total,
    int Pendentes,
    int EmAndamento,
    int Concluidas);

public sealed record IndicadorOportunidade(
    string Codigo,
    string Titulo,
    int Quantidade,
    string Complemento,
    int? VariacaoPercentual = null);

/// <summary>
/// Objetivo: representar fatos observáveis separados das recomendações gerenciais.
/// Origem: telemetria contextual autorizada e integrações habilitadas.
/// Destino: Evidências, Agora e detalhe da estação.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record EvidenciaResumo(
    Guid Id,
    Guid? AtivoId,
    string? Hostname,
    string? Pessoa,
    string? Equipe,
    string Categoria,
    string Gravidade,
    string Resumo,
    string? Detalhe,
    string? Fonte,
    DateTimeOffset ObservadaEmUtc);

/// <summary>
/// Objetivo: representar uma evidência visual autorizada sem armazenar o binário da imagem no banco.
/// Origem: captura governada do agente ou conteúdo fictício da empresa Demonstração.
/// Destino: Evidências e detalhe da estação.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record EvidenciaVisualResumo(
    Guid Id,
    Guid? AtivoId,
    string? Hostname,
    string? Pessoa,
    string Tipo,
    string Titulo,
    string? Descricao,
    string Url,
    DateTimeOffset CapturadaEmUtc,
    bool Demonstrativa);

public sealed record PainelEvidencias(
    IReadOnlyCollection<EvidenciaResumo> Evidencias,
    IReadOnlyCollection<EvidenciaVisualResumo> EvidenciasVisuais,
    int ProdutividadeObservada,
    int PeriodosOciosos,
    int AcessosNaoProdutivos,
    int UsoSistemaPrincipal,
    long SegundosReuniaoHoje);

public sealed record ResumoLocalTrabalho(
    string Codigo,
    string Nome,
    int Estacoes,
    int Produtividade,
    int Foco,
    int Ociosidade);

public sealed record RegraLocalResumo(
    Guid Id,
    string Nome,
    string FaixaIp,
    string Tipo);

public sealed record EstacaoLocalResumo(
    Guid AtivoId,
    string Hostname,
    string? Responsavel,
    string Local,
    string? EnderecoIpPublico,
    DateTimeOffset? UltimaComunicacaoUtc);

public sealed record PainelLocais(
    IReadOnlyCollection<ResumoLocalTrabalho> Resumos,
    IReadOnlyCollection<RegraLocalResumo> Regras,
    IReadOnlyCollection<EstacaoLocalResumo> Estacoes,
    IReadOnlyCollection<OportunidadeResumo> Oportunidades,
    string InsightPrincipal);

public sealed record ContextoGerencialEstacao(
    Guid? AgenteId,
    string? Unidade,
    string? Departamento,
    string StatusAtual,
    string EstadoAtual,
    string? ProcessoAtual,
    string LocalTrabalho,
    long SegundosProdutivosHoje,
    long SegundosOciososHoje,
    string? AplicativoPrincipal,
    int IndiceFoco,
    IReadOnlyCollection<LinhaTimeline> TimelineHoje,
    IReadOnlyCollection<OportunidadeResumo> Oportunidades,
    IReadOnlyCollection<EvidenciaResumo> Evidencias,
    IReadOnlyCollection<EvidenciaVisualResumo> EvidenciasVisuais);

public sealed record PainelDetalheEstacao(
    DetalheEstacao Estacao,
    Guid? AgenteId,
    string? Unidade,
    string? Departamento,
    string StatusAtual,
    string EstadoAtual,
    string? ProcessoAtual,
    string LocalTrabalho,
    long SegundosProdutivosHoje,
    long SegundosOciososHoje,
    string? AplicativoPrincipal,
    int IndiceFoco,
    IReadOnlyCollection<LinhaTimeline> TimelineHoje,
    IReadOnlyCollection<OportunidadeResumo> Oportunidades,
    IReadOnlyCollection<EvidenciaResumo> Evidencias,
    IReadOnlyCollection<EvidenciaVisualResumo> EvidenciasVisuais);

public sealed record PainelDrilldown(
    string Titulo,
    string? Subtitulo,
    IReadOnlyCollection<LinhaDrilldown> Linhas);

public sealed record LinhaDrilldown(
    string Principal,
    string? Secundario,
    string? Valor,
    string? Estado,
    string? Url);
