using System.Xml.Linq;

namespace ShareIT.Clara.Comum.Configuracao;

/// <summary>
/// Objetivo: carregar a configuração persistente do Share IT .Clara com sobreposição segura por variáveis de ambiente.
/// Origem: XML local, CLARA_CONFIG e variáveis CLARA_*.
/// Destino: Web/API e serviços internos.
/// Dependências: System.Xml.Linq.
/// Impacto: atende tanto publicação Windows/ConfigSys quanto hospedagem Linux/Render sem espalhar parâmetros pela solução.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public static class CarregadorConfiguracaoClara
{
    public static ConfiguracaoClara Carregar(string? caminho = null)
    {
        caminho ??= Environment.GetEnvironmentVariable("CLARA_CONFIG");
        caminho ??= @"C:\ConfigSys\Clara\ConfiguracaoClara.xml";

        XElement? raiz = null;
        if (File.Exists(caminho))
            raiz = XDocument.Load(caminho).Root ?? throw new InvalidOperationException("ConfiguracaoClara.xml inválido.");

        var servidor = raiz?.Element("Servidor");
        var banco = raiz?.Element("Banco");
        var bootstrap = raiz?.Element("Bootstrap");
        var agente = raiz?.Element("Agente");

        return new ConfiguracaoClara
        {
            Servidor = new ConfiguracaoServidor
            {
                UrlPublica = Ambiente("CLARA_URL_PUBLICA", Valor(servidor, "UrlPublica", "http://localhost:5080")),
                ExecutarMigracoesAutomaticamente = AmbienteBool("CLARA_EXECUTAR_MIGRACOES", Bool(servidor, "ExecutarMigracoesAutomaticamente", true)),
                MinutosParaConsiderarOffline = AmbienteInt("CLARA_OFFLINE_MINUTOS", Int(servidor, "MinutosParaConsiderarOffline", 3))
            },
            Banco = new ConfiguracaoBanco
            {
                StringConexaoVariavel = Valor(banco, "StringConexaoVariavel", "CLARA_POSTGRES"),
                NomeBanco = Ambiente("CLARA_BANCO_NOME", Valor(banco, "NomeBanco", "shareit_clara")),
                CriarAutomaticamente = AmbienteBool("CLARA_CRIAR_BANCO", Bool(banco, "CriarAutomaticamente", true))
            },
            Bootstrap = new ConfiguracaoBootstrap
            {
                NomeTenant = Ambiente("CLARA_TENANT_NOME", Valor(bootstrap, "NomeTenant", "Share IT")),
                IdentificadorTenant = Ambiente("CLARA_TENANT_IDENTIFICADOR", Valor(bootstrap, "IdentificadorTenant", "shareit")),
                NomeAdministrador = Ambiente("CLARA_ADMIN_NOME", Valor(bootstrap, "NomeAdministrador", "Administrador")),
                EmailAdministrador = Ambiente("CLARA_ADMIN_EMAIL", Valor(bootstrap, "EmailAdministrador", "admin@localhost")),
                SenhaAdministradorVariavel = Valor(bootstrap, "SenhaAdministradorVariavel", "CLARA_ADMIN_SENHA")
            },
            Agente = new ConfiguracaoAgente
            {
                IntervaloHeartbeatSegundos = AmbienteInt("CLARA_HEARTBEAT_SEGUNDOS", Int(agente, "IntervaloHeartbeatSegundos", 60)),
                IntervaloSincronizacaoSegundos = AmbienteInt("CLARA_SINCRONIZACAO_SEGUNDOS", Int(agente, "IntervaloSincronizacaoSegundos", 30)),
                IntervaloAtualizacaoMinutos = AmbienteInt("CLARA_ATUALIZACAO_MINUTOS", Int(agente, "IntervaloAtualizacaoMinutos", 15)),
                LimiarOciosidadeMinutos = AmbienteInt("CLARA_OCIOSIDADE_MINUTOS", Int(agente, "LimiarOciosidadeMinutos", 5))
            }
        };
    }

    public static string ObterStringConexao(ConfiguracaoClara configuracao)
    {
        var valor = Environment.GetEnvironmentVariable(configuracao.Banco.StringConexaoVariavel);
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException($"Variável {configuracao.Banco.StringConexaoVariavel} não definida.");
        return valor;
    }

    private static string Valor(XElement? no, string nome, string padrao) => no?.Element(nome)?.Value?.Trim() is { Length: > 0 } valor ? valor : padrao;
    private static int Int(XElement? no, string nome, int padrao) => int.TryParse(no?.Element(nome)?.Value, out var valor) ? valor : padrao;
    private static bool Bool(XElement? no, string nome, bool padrao) => bool.TryParse(no?.Element(nome)?.Value, out var valor) ? valor : padrao;
    private static string Ambiente(string nome, string padrao) => Environment.GetEnvironmentVariable(nome)?.Trim() is { Length: > 0 } valor ? valor : padrao;
    private static int AmbienteInt(string nome, int padrao) => int.TryParse(Environment.GetEnvironmentVariable(nome), out var valor) ? valor : padrao;
    private static bool AmbienteBool(string nome, bool padrao) => bool.TryParse(Environment.GetEnvironmentVariable(nome), out var valor) ? valor : padrao;
}
