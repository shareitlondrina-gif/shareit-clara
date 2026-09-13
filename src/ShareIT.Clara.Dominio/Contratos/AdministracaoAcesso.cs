namespace ShareIT.Clara.Dominio.Contratos;

/// <summary>
/// Objetivo: representar empresas/tenants e administração de acesso da plataforma.
/// Origem: gestão Master e administração do cliente.
/// Destino: telas Empresas, Usuários e Perfis.
/// Dependências: tenants, empresas, usuarios, perfis_acesso e perfil_modulos.
/// Impacto: formaliza o modelo multiempresa e o controle de acesso por módulos.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record EmpresaMasterResumo(
    Guid TenantId,
    Guid EmpresaId,
    string Nome,
    string Identificador,
    string? Documento,
    int Usuarios,
    int Estacoes,
    bool Ativo,
    string Plano = "Gestão",
    int LimiteEstacoes = 15,
    string StatusAssinatura = "Ativa",
    DateOnly? Vencimento = null,
    int RetencaoDias = 90,
    int ArmazenamentoGb = 5,
    string? ObservacaoAssinatura = null);

public sealed record AssinaturaTenantResumo(
    Guid TenantId,
    string Plano,
    string Status,
    int LimiteEstacoes,
    int RetencaoDias,
    int ArmazenamentoGb,
    DateOnly? Vencimento,
    string? Observacao);

public sealed record UsuarioAcessoResumo(
    Guid Id,
    string Nome,
    string Email,
    string PerfilSistema,
    string? PerfilAcesso,
    bool Ativo,
    DateTimeOffset CriadoEmUtc);

public sealed record PerfilAcessoResumo(
    Guid Id,
    string Codigo,
    string Nome,
    bool Padrao,
    bool Sistema,
    IReadOnlyCollection<ModuloAcessoResumo> Modulos);

public sealed record ModuloAcessoResumo(
    string Codigo,
    string Nome,
    string Grupo,
    bool Permitido);
