// Clara
// Programador: Paulo Andrade
// Objetivo: executar coleta e comunicação do agente Windows.
// Origem: Windows, configuração local e API Clara.
// Destino: API Clara, telemetria autorizada e estado local.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Text.Json;
using System.Text.Json.Serialization;
using Clara.Dominio.Contratos;

namespace Clara.Agente.Configuracao;

/// <summary>
/// Objetivo: representar a configuração local do agente e a política recebida do tenant.
/// Origem: C:\ProgramData\Clara\configuracao-agente.json e API /politica.
/// Destino: heartbeat, sessão interativa, telemetria autorizada e atualização.
/// Dependências: System.Text.Json e ACL preparada pelo instalador.
/// Impacto: mantém política sincronizada sem persistir o token temporário em arquivo comum.
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
    public int TempoOfflineMinutos { get; set; } = 15;
    public string HorarioInicio { get; set; } = "08:00";
    public string HorarioFim { get; set; } = "18:00";
    public string DiasMonitorados { get; set; } = "1,2,3,4,5";
    public string FusoHorario { get; set; } = "America/Sao_Paulo";
    public bool PermitirClassificacaoWeb { get; set; } = true;
    public bool MonitorarUrlsDominios { get; set; }
    public bool UsarCategoriasWeb { get; set; } = true;
    public bool CapturaScreenshots { get; set; }
    public int IntervaloCapturaMinutos { get; set; } = 5;
    public int RetencaoEvidenciasDias { get; set; } = 90;
    public bool AnonimizacaoParcial { get; set; } = true;
    public List<string> AplicativosExcluidos { get; set; } = new();
    public List<string> DominiosExcluidos { get; set; } = new();
    public DateTimeOffset? PoliticaAtualizadaEmUtc { get; set; }

    public static string DiretorioDados => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Clara", "Clara");
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

    public void AplicarPolitica(PoliticaMonitoramentoAgente politica)
    {
        IntervaloHeartbeatSegundos = Math.Clamp(politica.IntervaloHeartbeatSegundos, 15, 3600);
        TempoOfflineMinutos = Math.Clamp(politica.TempoOfflineMinutos, 1, 1440);
        LimiarOciosidadeMinutos = Math.Clamp(politica.LimiarOciosidadeMinutos, 1, 240);
        HorarioInicio = politica.HorarioInicio;
        HorarioFim = politica.HorarioFim;
        DiasMonitorados = politica.DiasMonitorados;
        FusoHorario = string.IsNullOrWhiteSpace(politica.FusoHorario) ? "America/Sao_Paulo" : politica.FusoHorario.Trim();
        PermitirClassificacaoWeb = politica.PermitirClassificacaoWeb;
        MonitorarUrlsDominios = politica.MonitorarUrlsDominios;
        UsarCategoriasWeb = politica.UsarCategoriasWeb;
        CapturaScreenshots = politica.CapturaScreenshots;
        IntervaloCapturaMinutos = Math.Clamp(politica.IntervaloCapturaMinutos, 1, 1440);
        RetencaoEvidenciasDias = Math.Clamp(politica.RetencaoEvidenciasDias, 1, 3650);
        AnonimizacaoParcial = politica.AnonimizacaoParcial;
        AplicativosExcluidos = politica.AplicativosExcluidos?.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new();
        DominiosExcluidos = politica.DominiosExcluidos?.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim().Trim('.').ToLowerInvariant()).Distinct().ToList() ?? new();
        PoliticaAtualizadaEmUtc = politica.AtualizadaEmUtc;
    }

    /// <summary>
    /// Retorna verdadeiro somente dentro da janela efetiva de monitoramento configurada.
    /// Heartbeat/inventário técnico podem continuar fora da janela; atividade de sessão/navegação não.
    /// </summary>
    public bool EstaDentroPeriodoMonitoramento(DateTimeOffset agora)
    {
        var agoraLocal = ConverterParaFusoConfigurado(agora);
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


    private DateTimeOffset ConverterParaFusoConfigurado(DateTimeOffset agora)
    {
        try
        {
            var zona = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(FusoHorario) ? "America/Sao_Paulo" : FusoHorario.Trim());
            return TimeZoneInfo.ConvertTime(agora, zona);
        }
        catch
        {
            // Falha de configuração de timezone não deve ampliar a coleta. Usa o horário local da estação
            // apenas como fallback operacional até a próxima política válida.
            return agora.ToLocalTime();
        }
    }


    public bool AplicativoEstaExcluido(string? processo)
    {
        if(string.IsNullOrWhiteSpace(processo)) return false;
        var original=processo.Trim();var nome=Path.GetFileName(original);
        return AplicativosExcluidos.Any(x=>string.Equals(x,original,StringComparison.OrdinalIgnoreCase)||string.Equals(x,nome,StringComparison.OrdinalIgnoreCase));
    }

    public bool DominioEstaExcluido(string? dominio)
    {
        if(string.IsNullOrWhiteSpace(dominio)) return false;
        var d=dominio.Trim().Trim('.').ToLowerInvariant();
        return DominiosExcluidos.Any(x=>d==x||d.EndsWith("."+x,StringComparison.OrdinalIgnoreCase));
    }

    public void Salvar() => GravarArquivo();

    public void SalvarSemTokenInstalacao()
    {
        TokenInstalacao = null;
        if (File.Exists(CaminhoTokenInstalacao)) File.Delete(CaminhoTokenInstalacao);
        GravarArquivo();
    }

    private void GravarArquivo()
    {
        Directory.CreateDirectory(DiretorioDados);
        var temporario = CaminhoArquivo + ".tmp";
        File.WriteAllText(temporario, JsonSerializer.Serialize(this, OpcoesJson()));
        File.Move(temporario, CaminhoArquivo, true);
    }

    private static JsonSerializerOptions OpcoesJson() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
