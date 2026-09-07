using System.Text.Json;

namespace ShareIT.Clara.Sessao;

internal sealed class ConfiguracaoSessaoLocal
{
    public int LimiarOciosidadeMinutos { get; set; } = 5;

    public static ConfiguracaoSessaoLocal Carregar()
    {
        var caminho = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ShareIT", "Clara", "configuracao-agente.json");
        if (!File.Exists(caminho)) return new();
        using var doc = JsonDocument.Parse(File.ReadAllText(caminho));
        return new ConfiguracaoSessaoLocal
        {
            LimiarOciosidadeMinutos = doc.RootElement.TryGetProperty("limiarOciosidadeMinutos", out var valor) && valor.TryGetInt32(out var minutos) ? minutos : 5
        };
    }
}
