using System.Text.Json;

namespace ShareIT.Clara.Sessao;

/// <summary>
/// Objetivo: carregar o limiar de ociosidade usado pelo coletor da sessão interativa.
/// Origem: arquivo persistente de configuração do agente em ProgramData.
/// Destino: LeitorAtividadeWindows.
/// Dependências: JSON local gravado pelo instalador/agente.
/// Impacto: mantém o comportamento da sessão alinhado à política local sem depender da rede a cada amostra.
/// Desenvolvedor: Paulo Andrade
/// </summary>
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
