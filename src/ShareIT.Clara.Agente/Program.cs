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
builder.Services.AddSingleton<ClienteApiClara>();
builder.Services.AddHostedService<ServidorPipeSessao>();
builder.Services.AddHostedService<ServicoAgente>();
builder.Services.AddHostedService<ServicoAtualizacaoAutomatica>();

await builder.Build().RunAsync();
