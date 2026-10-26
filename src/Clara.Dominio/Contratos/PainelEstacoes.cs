// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Dominio.Contratos;

/// <summary>
/// Objetivo: representar a gestão administrativa das estações descobertas automaticamente pelo agente.
/// Origem: inventário do agente e cadastro administrativo do tenant.
/// Destino: telas Estações e edição de cadastro.
/// Dependências: ativos, agentes, empresas, unidades e departamentos.
/// Impacto: separa dados técnicos automáticos dos dados administrativos complementares.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record PainelEstacoes(
    IReadOnlyCollection<EstacaoResumo> Estacoes,
    IReadOnlyCollection<OpcaoCadastro> Empresas,
    IReadOnlyCollection<OpcaoCadastro> Unidades,
    IReadOnlyCollection<OpcaoCadastro> Departamentos);

public sealed record EstacaoResumo(
    Guid AtivoId,
    Guid? AgenteId,
    string Hostname,
    string? UsuarioWindows,
    string? NumeroSerie,
    string? Fabricante,
    string? Modelo,
    string? SistemaOperacional,
    string? Patrimonio,
    string? Responsavel,
    string Empresa,
    string? Unidade,
    string? Departamento,
    bool CadastroCompleto,
    string Status,
    string? Versao,
    DateTimeOffset? UltimaComunicacaoUtc,
    string EstadoAtual,
    string? ProcessoAtual,
    string LocalTrabalho);

public sealed record DetalheEstacao(
    Guid AtivoId,
    Guid EmpresaId,
    Guid? UnidadeId,
    Guid? DepartamentoId,
    string Hostname,
    string? UsuarioWindows,
    string? NumeroSerie,
    string? UuidEquipamento,
    string? Fabricante,
    string? Modelo,
    string? SistemaOperacional,
    string? Arquitetura,
    long? MemoriaTotalBytes,
    long? DiscoTotalBytes,
    string? EnderecoIpLocal,
    string? EnderecoIpPublico,
    string? Patrimonio,
    string? Responsavel,
    bool CadastroCompleto,
    DateTimeOffset? PrimeiraComunicacaoUtc,
    DateTimeOffset? UltimaComunicacaoUtc,
    string? VersaoAgente);

public sealed record OpcaoCadastro(Guid Id, string Nome, Guid? EmpresaId = null);
