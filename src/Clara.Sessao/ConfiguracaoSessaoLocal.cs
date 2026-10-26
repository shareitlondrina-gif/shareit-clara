// Clara
// Programador: Paulo Andrade
// Objetivo: observar a sessão interativa autorizada.
// Origem: sessão Windows do usuário.
// Destino: agente Clara por canal local controlado.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Text.Json;

namespace Clara.Sessao;

/// <summary>
/// Objetivo: carregar a política local usada pelo coletor da sessão interativa.
/// Origem: configuracao-agente.json sincronizado pelo Windows Service.
/// Destino: LeitorAtividadeWindows e janela de monitoramento autorizada.
/// Dependências: JSON local em ProgramData.
/// Impacto: o processo de sessão respeita limiar e horários sem depender da rede a cada amostra.
/// Desenvolvedor: Paulo Andrade
/// </summary>
internal sealed class ConfiguracaoSessaoLocal
{
    public int LimiarOciosidadeMinutos { get; set; } = 5;
    public string HorarioInicio { get; set; } = "08:00";
    public string HorarioFim { get; set; } = "18:00";
    public string DiasMonitorados { get; set; } = "1,2,3,4,5";

    public bool EstaDentroHorario(DateTimeOffset agoraLocal)
    {
        var isoDia = ((int)agoraLocal.DayOfWeek + 6) % 7 + 1;
        var dias = (DiasMonitorados ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var d) ? d : 0)
            .ToHashSet();
        if (dias.Count > 0 && !dias.Contains(isoDia)) return false;

        var inicio = TimeSpan.TryParse(HorarioInicio, out var i) ? i : TimeSpan.FromHours(8);
        var fim = TimeSpan.TryParse(HorarioFim, out var f) ? f : TimeSpan.FromHours(18);
        var atual = agoraLocal.TimeOfDay;
        return fim >= inicio ? atual >= inicio && atual <= fim : atual >= inicio || atual <= fim;
    }

    public static ConfiguracaoSessaoLocal Carregar()
    {
        var caminho = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Clara", "Clara", "configuracao-agente.json");
        if (!File.Exists(caminho)) return new();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(caminho));
            var raiz = doc.RootElement;
            return new ConfiguracaoSessaoLocal
            {
                LimiarOciosidadeMinutos = raiz.TryGetProperty("limiarOciosidadeMinutos", out var limiar) && limiar.TryGetInt32(out var minutos) ? minutos : 5,
                HorarioInicio = raiz.TryGetProperty("horarioInicio", out var inicio) ? inicio.GetString() ?? "08:00" : "08:00",
                HorarioFim = raiz.TryGetProperty("horarioFim", out var fim) ? fim.GetString() ?? "18:00" : "18:00",
                DiasMonitorados = raiz.TryGetProperty("diasMonitorados", out var dias) ? dias.GetString() ?? "1,2,3,4,5" : "1,2,3,4,5"
            };
        }
        catch
        {
            return new();
        }
    }
}
