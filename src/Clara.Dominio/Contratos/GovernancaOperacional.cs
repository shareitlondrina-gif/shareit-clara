// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Dominio.Contratos;

/// <summary>
/// Objetivo: separar jornada de referência (análise) do período efetivo de monitoramento (coleta).
/// A Clara registra fatos; interpretação de produtividade pertence ao cliente.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record ConfiguracaoJornadaEstacao(
    Guid AtivoId,
    bool PossuiSobrescrita,
    string JornadaInicio,
    string JornadaFim,
    int CargaHorariaDiariaMinutos,
    int MargemAntesMinutos,
    int MargemDepoisMinutos,
    string DiasJornada,
    string MonitoramentoInicio,
    string MonitoramentoFim,
    string DiasMonitorados,
    string FusoHorario);

public sealed record RegraAlertaResumo(
    Guid Id,
    string Nome,
    string Tipo,
    int LimiteMinutos,
    string EscopoTipo,
    Guid? DepartamentoId,
    string? Departamento,
    Guid? AtivoId,
    string? Hostname,
    string Severidade,
    bool CanalPlataforma,
    bool CanalEmail,
    string? DestinatariosEmail,
    string DestinatariosPerfis,
    bool GerarEvidencia,
    bool GerarOportunidade,
    bool Ativo);

public sealed record NotificacaoResumo(
    Guid Id,
    string Tipo,
    string Titulo,
    string? Resumo,
    string Severidade,
    string? UrlDestino,
    bool Lida,
    DateTimeOffset CriadaEmUtc);

public sealed record PainelNotificacoes(
    IReadOnlyCollection<NotificacaoResumo> Itens,
    int NaoLidas);

public sealed record LinhaRelatorioHistorico(
    Guid AtivoId,
    string Hostname,
    string? Pessoa,
    string? Equipe,
    long SegundosProdutivosJornada,
    long SegundosOciososJornada,
    long SegundosNaoProdutivosJornada,
    long SegundosProdutivosForaJornada,
    long SegundosMonitorados,
    int CargaHorariaDiariaMinutos,
    string JornadaReferencia);

public sealed record PainelRelatoriosHistorico(
    IReadOnlyCollection<LinhaRelatorioHistorico> Linhas,
    int Dias,
    long SegundosProdutivosJornada,
    long SegundosOciososJornada,
    long SegundosNaoProdutivosJornada,
    long SegundosProdutivosForaJornada,
    string InsightPrincipal);

public sealed record DocumentoGovernancaResumo(
    Guid Id,
    string Tipo,
    string Titulo,
    string? Versao,
    string NomeArquivo,
    long TamanhoBytes,
    string Sha256,
    DateOnly? ValidadeAte,
    string StatusValidacao,
    DateTimeOffset CriadoEmUtc);

public sealed record SolicitacaoJuridicaResumo(
    Guid Id,
    Guid? DocumentoId,
    string Recurso,
    string? EscopoDescricao,
    string EmailJuridico,
    DateOnly ValidadeSolicitadaAte,
    string Status,
    string? AprovadorNome,
    string? AprovadorCargo,
    DateTimeOffset CriadoEmUtc,
    DateTimeOffset ExpiraEmUtc,
    DateTimeOffset? DecisaoEmUtc);

public sealed record AutorizacaoGovernancaResumo(
    Guid Id,
    string Recurso,
    string? EscopoDescricao,
    DateTimeOffset AprovadaEmUtc,
    DateOnly ValidadeAte,
    string AprovadorEmail,
    string? AprovadorNome,
    string? AprovadorCargo,
    DateTimeOffset? RevogadaEmUtc);

public sealed record CienciaPoliticaResumo(
    Guid Id,
    Guid DocumentoId,
    string DocumentoTitulo,
    string ColaboradorNome,
    string? ColaboradorEmail,
    string FormaCiencia,
    DateTimeOffset CienciaEmUtc,
    string? Observacao);

public sealed record ConfiguracaoEmailTenant(
    string RemetenteNome,
    string RemetenteEmail,
    string? ReplyTo,
    string? DestinatariosAdicionais);

public sealed record PainelGovernanca(
    IReadOnlyCollection<DocumentoGovernancaResumo> Documentos,
    IReadOnlyCollection<SolicitacaoJuridicaResumo> Solicitacoes,
    IReadOnlyCollection<AutorizacaoGovernancaResumo> Autorizacoes,
    IReadOnlyCollection<CienciaPoliticaResumo> Ciencias,
    ConfiguracaoEmailTenant Email,
    IReadOnlyDictionary<string, string> StatusRecursos);

public sealed record SolicitarAprovacaoResultado(Guid Id, string Token, DateTimeOffset ExpiraEmUtc);

public sealed record SolicitacaoAprovacaoPublica(
    Guid Id,
    string Empresa,
    string Recurso,
    string? EscopoDescricao,
    string EmailJuridico,
    string? DocumentoTitulo,
    string? DocumentoSha256,
    DateOnly ValidadeSolicitadaAte,
    string Status,
    DateTimeOffset ExpiraEmUtc);

/// <summary>Relatório recorrente enviado pela fila oficial de e-mail da Clara.</summary>
public sealed record RelatorioAgendadoResumo(
    Guid Id,
    string Nome,
    string TipoRelatorio,
    string Periodicidade,
    string HoraLocal,
    int? DiaSemana,
    int? DiaMes,
    string FusoHorario,
    string Destinatarios,
    bool Ativo,
    DateTimeOffset? UltimaExecucaoUtc,
    DateTimeOffset? ProximaExecucaoUtc,
    string? UltimoStatus);

public sealed record FiltroSalvoRelatorioResumo(
    Guid Id,
    string Nome,
    string TipoRelatorio,
    string ParametrosJson,
    bool Compartilhado,
    DateTimeOffset AtualizadoEmUtc);

/// <summary>Estado da integração opcional com outro produta Clara.</summary>
public sealed record IntegracaoProdutoResumo(
    string Produto,
    bool Contratado,
    bool IntegracaoAtiva,
    string? UrlBase,
    DateTimeOffset? UltimoSucessoUtc,
    string? UltimaFalha);

public sealed record ContextoTecnicoVisaoResumo(
    long Id,
    Guid? AtivoId,
    string? Hostname,
    string Categoria,
    string Severidade,
    string Resumo,
    string? Detalhe,
    string? ReferenciaExterna,
    DateTimeOffset ObservadoEmUtc);

public sealed record ItemPulsoOperacao(
    string Codigo,
    string Titulo,
    string Valor,
    string Tendencia,
    string Situacao,
    string Explicacao);

public sealed record PainelPulsoOperacao(
    int Indice,
    string Confianca,
    IReadOnlyCollection<ItemPulsoOperacao> Indicadores,
    IReadOnlyCollection<string> PontosPositivos,
    IReadOnlyCollection<string> PontosObservacao,
    IReadOnlyCollection<string> Prioridades,
    IReadOnlyCollection<string> AcoesSugeridas,
    bool ContextoTiDisponivel,
    string? ResumoContextoTi);

public sealed record ConfiguracaoRetencaoResumo(
    int TelemetriaDias,
    int EvidenciasDias,
    int NotificacoesDias,
    int EmailsDias,
    int AuditoriaDias);

public sealed record ExclusaoMonitoramentoResumo(
    Guid Id,
    Guid? DepartamentoId,
    string? Departamento,
    string Tipo,
    string Valor,
    string? Motivo,
    bool Ativo);

public sealed record CustoSoftwareResumo(
    Guid Id,
    string ProcessoOuProduto,
    int QuantidadeLicencas,
    decimal CustoMensalUnitario,
    string? Observacao,
    bool Ativo,
    int UsuariosObservados30Dias,
    decimal CustoMensalTotal,
    decimal CustoPotencialSubutilizado);

public sealed record ReferenciaOperacionalResumo(
    Guid Id,
    Guid? DepartamentoId,
    string? Departamento,
    string Nome,
    string Metrica,
    decimal ValorReferencia,
    string Unidade,
    bool Ativo);

public sealed record ResumoAdocaoIa(
    int PessoasComUso,
    int EstacoesComUso,
    long SegundosObservados,
    IReadOnlyCollection<string> Ferramentas);

public sealed record ItemCapacidadeOperacional(
    Guid AtivoId,
    string Hostname,
    string? Pessoa,
    string? Equipe,
    int MinutosEngajadosDiaMedio,
    int CargaReferenciaMinutos,
    int PercentualCarga,
    string Situacao);

public sealed record ResumoCapacidadeOperacional(
    int Subutilizados,
    int Equilibrados,
    int Sobrecarregados,
    IReadOnlyCollection<ItemCapacidadeOperacional> Itens);

public sealed record PadraoRepetitivoOperacional(
    string Processo,
    string? Pessoa,
    string? Equipe,
    int SegmentosObservados,
    int DiasComUso,
    int MediaSegmentosPorDia,
    int DuracaoMediaMinutos,
    string Leitura);

public sealed record ResumoPadroesRepetitivos(
    int Candidatos,
    IReadOnlyCollection<PadraoRepetitivoOperacional> Itens);
