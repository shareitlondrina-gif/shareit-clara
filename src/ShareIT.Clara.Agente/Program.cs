using System.Reflection;
using System.Text.Json;
using ShareIT.Clara.Agente.Atualizacao;
using ShareIT.Clara.Agente.Comunicacao;
using ShareIT.Clara.Agente.Configuracao;
using ShareIT.Clara.Agente.Identidade;
using ShareIT.Clara.Agente.Inventario;
using ShareIT.Clara.Agente.Servicos;
using ShareIT.Clara.Agente.Telemetria;

var builder = Host.CreateApplicationBuilder(args);
var configuracao = ConfiguracaoAgenteLocal.Carregar();
var http = new HttpClient
{
    BaseAddress = new Uri(configuracao.UrlServidor.TrimEnd('/') + "/"),
    Timeout = TimeSpan.FromSeconds(30)
};

builder.Services.AddWindowsService(opcoes => opcoes.ServiceName = "Share IT .Clara - Agente");
builder.Services.AddSingleton(configuracao);
builder.Services.AddSingleton(http);
builder.Services.AddSingleton<GerenciadorIdentidadeAgente>();
builder.Services.AddSingleton<ColetorInventarioWindows>();
builder.Services.AddSingleton<FilaOffline>();
builder.Services.AddSingleton<FilaNavegacao>();
builder.Services.AddSingleton<ClienteApiClara>();
builder.Services.AddHostedService<ServidorPipeSessao>();
builder.Services.AddHostedService<ServidorPipeNavegacao>();
builder.Services.AddHostedService<ServicoConectorNavegador>();
builder.Services.AddHostedService<ServicoAgente>();
builder.Services.AddHostedService<ServicoVigilanciaSessao>();
builder.Services.AddHostedService<ServicoAtualizacaoAutomatica>();

var host = builder.Build();
await host.StartAsync();
RegistrarSaudeLocal();
await host.WaitForShutdownAsync();

static void RegistrarSaudeLocal()
{
    try
    {
        var diretorio = Path.Combine(ConfiguracaoAgenteLocal.DiretorioDados, "atualizacoes");
        Directory.CreateDirectory(diretorio);
        var versao = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "desconhecida";
        var marcador = new
        {
            versao,
            pid = Environment.ProcessId,
            iniciadoEmUtc = DateTimeOffset.UtcNow
        };
        var caminho = Path.Combine(diretorio, "saude-agente.json");
        var tmp = caminho + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(marcador));
        File.Move(tmp, caminho, true);
    }
    catch
    {
        // O marcador é uma proteção adicional do atualizador; falha local não derruba o agente.
    }
}
