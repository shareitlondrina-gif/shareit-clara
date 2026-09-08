namespace ShareIT.Clara.Dominio.Contratos;

/// <summary>
/// Objetivo: representar um Índice Geral explicável e sua confiança com base somente nos sinais disponíveis.
/// Origem: períodos de atividade e regras explícitas de classificação do tenant.
/// Destino: painel executivo do Share IT .Clara.
/// Dependências: telemetria agregada e classificações configuradas.
/// Impacto: oferece síntese executiva sem esconder como o número foi calculado nem substituir interpretação humana.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record PainelIndiceGeral(
    int IndiceGeral,
    int ConfiancaDados,
    DateTimeOffset InicioPeriodoUtc,
    IReadOnlyCollection<ComponenteIndice> Componentes,
    long SegundosObservados,
    int EstacoesComDados,
    int TotalEstacoes,
    string Explicacao);

public sealed record ComponenteIndice(
    string Nome,
    int Valor,
    int PesoAplicado,
    bool Disponivel,
    string Descricao);
