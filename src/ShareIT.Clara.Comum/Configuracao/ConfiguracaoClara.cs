namespace ShareIT.Clara.Comum.Configuracao;

/// <summary>
/// Objetivo: representar as configurações não secretas do servidor Share IT .Clara.
/// Origem: C:\ConfigSys\Clara\ConfiguracaoClara.xml ou caminho definido por CLARA_CONFIG.
/// Destino: Web/API e serviços internos.
/// Dependências: XML de configuração local.
/// Impacto: centraliza comportamento de ambiente sem incorporar segredos no código ou release.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ConfiguracaoClara
{
    public ConfiguracaoServidor Servidor { get; init; } = new();
    public ConfiguracaoBanco Banco { get; init; } = new();
    public ConfiguracaoBootstrap Bootstrap { get; init; } = new();
    public ConfiguracaoAgente Agente { get; init; } = new();
}

public sealed class ConfiguracaoServidor
{
    public string UrlPublica { get; init; } = "http://localhost:5080";
    public bool ExecutarMigracoesAutomaticamente { get; init; } = true;
    public int MinutosParaConsiderarOffline { get; init; } = 3;
}

public sealed class ConfiguracaoBanco
{
    public string StringConexaoVariavel { get; init; } = "CLARA_POSTGRES";
    public string NomeBanco { get; init; } = "shareit_clara";
    public bool CriarAutomaticamente { get; init; } = true;
}

public sealed class ConfiguracaoBootstrap
{
    public string NomeTenant { get; init; } = "Share IT";
    public string IdentificadorTenant { get; init; } = "shareit";
    public string NomeAdministrador { get; init; } = "Administrador";
    public string EmailAdministrador { get; init; } = "admin@localhost";
    public string SenhaAdministradorVariavel { get; init; } = "CLARA_ADMIN_SENHA";
}

public sealed class ConfiguracaoAgente
{
    public int IntervaloHeartbeatSegundos { get; init; } = 60;
    public int IntervaloSincronizacaoSegundos { get; init; } = 30;
    public int IntervaloAtualizacaoMinutos { get; init; } = 15;
    public int LimiarOciosidadeMinutos { get; init; } = 5;
}
