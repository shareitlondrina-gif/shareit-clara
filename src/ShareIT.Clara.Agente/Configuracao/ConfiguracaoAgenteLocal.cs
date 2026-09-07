using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShareIT.Clara.Agente.Configuracao;

/// <summary>
/// Objetivo: representar a configuração local de cada instalação do agente sem persistir segredos no arquivo legível pela sessão.
/// Origem: C:\ProgramData\ShareIT\Clara\configuracao-agente.json e pasta protegida segredos.
/// Destino: serviços de comunicação, heartbeat, telemetria e atualização.
/// Dependências: System.Text.Json e ACL preparada pelo instalador.
/// Impacto: permite alterar endereço e intervalos sem recompilar e mantém o token temporário fora da configuração comum.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ConfiguracaoAgenteLocal
{
    public string UrlServidor { get; set; } = "http://localhost:5080";
    [JsonIgnore] public string? TokenInstalacao { get; set; }
    public int IntervaloHeartbeatSegundos { get; set; } = 60;
    public int IntervaloSincronizacaoSegundos { get; set; } = 30;
    public int IntervaloAtualizacaoMinutos { get; set; } = 15;
    public int LimiarOciosidadeMinutos { get; set; } = 5;

    public static string DiretorioDados => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ShareIT", "Clara");
    public static string DiretorioSegredos => Path.Combine(DiretorioDados, "segredos");
    public static string CaminhoArquivo => Path.Combine(DiretorioDados, "configuracao-agente.json");
    public static string CaminhoTokenInstalacao => Path.Combine(DiretorioSegredos, "token-instalacao.txt");

    public static ConfiguracaoAgenteLocal Carregar()
    {
        Directory.CreateDirectory(DiretorioDados);
        if (!File.Exists(CaminhoArquivo)) throw new FileNotFoundException("Configuração do agente não encontrada.", CaminhoArquivo);
        var configuracao = JsonSerializer.Deserialize<ConfiguracaoAgenteLocal>(File.ReadAllText(CaminhoArquivo), OpcoesJson())
            ?? throw new InvalidOperationException("Configuração do agente inválida.");
        if (File.Exists(CaminhoTokenInstalacao)) configuracao.TokenInstalacao = File.ReadAllText(CaminhoTokenInstalacao).Trim();
        return configuracao;
    }

    public void SalvarSemTokenInstalacao()
    {
        TokenInstalacao = null;
        if (File.Exists(CaminhoTokenInstalacao)) File.Delete(CaminhoTokenInstalacao);
        File.WriteAllText(CaminhoArquivo, JsonSerializer.Serialize(this, OpcoesJson()));
    }

    private static JsonSerializerOptions OpcoesJson() => new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, PropertyNameCaseInsensitive = true };
}
