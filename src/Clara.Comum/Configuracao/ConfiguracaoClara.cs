// Clara
// Programador: Paulo Andrade
// Objetivo: fornecer infraestrutura compartilhada da Clara.
// Origem: configuração e componentes internos.
// Destino: demais projetos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Comum.Configuracao;

/// <summary>
/// Objetivo: representar as configurações não secretas do servidor Clara.
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

/// <summary>
/// Objetivo: representar parâmetros não secretos do servidor Web/API.
/// Origem: configuração local e variáveis de ambiente normalizadas.
/// Destino: inicialização da aplicação, health e cálculo de status online/offline.
/// Dependências: CarregadorConfiguracaoClara.
/// Impacto: define URLs e comportamento operacional do servidor.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ConfiguracaoServidor
{
    public string UrlPublica { get; init; } = "http://localhost:5080";
    public bool ExecutarMigracoesAutomaticamente { get; init; } = true;
    public int MinutosParaConsiderarOffline { get; init; } = 3;
}

/// <summary>
/// Objetivo: representar exclusivamente a referência ao segredo de conexão e o banco lógico autorizado.
/// Origem: configuração não secreta do ambiente.
/// Destino: FabricaConexoesClara, PreparadorBancoClara e MigradorBancoClara.
/// Dependências: variável de ambiente contendo a connection string real.
/// Impacto: protege o isolamento do banco clara.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ConfiguracaoBanco
{
    public string StringConexaoVariavel { get; init; } = "CLARA_POSTGRES";
    public string NomeBanco { get; init; } = "clara";
    public bool CriarAutomaticamente { get; init; } = true;
}

/// <summary>
/// Objetivo: representar os parâmetros do tenant e administrador iniciais de homologação.
/// Origem: ConfigSys/variáveis de ambiente.
/// Destino: RepositorioBootstrap.
/// Dependências: senha fornecida exclusivamente por variável de ambiente.
/// Impacto: permite bootstrap idempotente sem persistir senha no código.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ConfiguracaoBootstrap
{
    public string NomeTenant { get; init; } = "Clara";
    public string IdentificadorTenant { get; init; } = "clara";
    public string NomeAdministrador { get; init; } = "Administrador";
    public string EmailAdministrador { get; init; } = "admin@localhost";
    public string SenhaAdministradorVariavel { get; init; } = "CLARA_ADMIN_SENHA";
}

/// <summary>
/// Objetivo: representar intervalos e limites operacionais distribuídos ao ecossistema do agente.
/// Origem: configuração do ambiente Clara.
/// Destino: heartbeat, sincronização, atualização e ociosidade.
/// Dependências: CarregadorConfiguracaoClara e instalador.
/// Impacto: centraliza parâmetros de coleta sem codificação dispersa.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ConfiguracaoAgente
{
    public int IntervaloHeartbeatSegundos { get; init; } = 60;
    public int IntervaloSincronizacaoSegundos { get; init; } = 30;
    public int IntervaloAtualizacaoMinutos { get; init; } = 15;
    public int LimiarOciosidadeMinutos { get; init; } = 5;
}
