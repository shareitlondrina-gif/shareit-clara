using System.Text.Json;
using System.Text.Json.Serialization;
using ShareIT.Clara.Dominio.Contratos;

namespace ShareIT.Clara.Agente.Configuracao;

/// <summary>
/// Objetivo: representar a configuração local do agente e a política recebida do tenant.
/// Origem: C:\ProgramData\ShareIT\Clara\configuracao-agente.json e API /politica.
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
    public bool PermitirClassificacaoWeb { get; set; } = true;
    public bool MonitorarUrlsDominios { get; set; }
    public bool UsarCategoriasWeb { get; set; } = true;
    public bool CapturaScreenshots { get; set; }
    public int IntervaloCapturaMinutos { get; set; } = 5;
    public int RetencaoEvidenciasDias { get; set; } = 90;
    public bool AnonimizacaoParcial { get; set; } = true;
    public DateTimeOffset? PoliticaAtualizadaEmUtc { get; set; }

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

    public void AplicarPolitica(PoliticaMonitoramentoAgente politica)
    {
        IntervaloHeartbeatSegundos = Math.Clamp(politica.IntervaloHeartbeatSegundos, 15, 3600);
        TempoOfflineMinutos = Math.Clamp(politica.TempoOfflineMinutos, 1, 1440);
        LimiarOciosidadeMinutos = Math.Clamp(politica.LimiarOciosidadeMinutos, 1, 240);
        HorarioInicio = politica.HorarioInicio;
        HorarioFim = politica.HorarioFim;
        DiasMonitorados = politica.DiasMonitorados;
        PermitirClassificacaoWeb = politica.PermitirClassificacaoWeb;
        MonitorarUrlsDominios = politica.MonitorarUrlsDominios;
        UsarCategoriasWeb = politica.UsarCategoriasWeb;
        CapturaScreenshots = politica.CapturaScreenshots;
        IntervaloCapturaMinutos = Math.Clamp(politica.IntervaloCapturaMinutos, 1, 1440);
        RetencaoEvidenciasDias = Math.Clamp(politica.RetencaoEvidenciasDias, 1, 3650);
        AnonimizacaoParcial = politica.AnonimizacaoParcial;
        PoliticaAtualizadaEmUtc = politica.AtualizadaEmUtc;
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
