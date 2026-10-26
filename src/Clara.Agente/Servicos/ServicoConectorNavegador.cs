// Clara
// Programador: Paulo Andrade
// Objetivo: executar uma regra de aplicação da Clara.
// Origem: controladores, configuração e serviços hospedados.
// Destino: repositórios, integrações ou eventos da aplicação.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Text.Json;
using Microsoft.Win32;
using Clara.Agente.Configuracao;

namespace Clara.Agente.Servicos;

/// <summary>
/// Objetivo: garantir o registro do Native Messaging Host após instalação nova ou atualização automática do agente.
/// Origem: inicialização do Windows Service.
/// Destino: manifesto em ProgramData e chaves HKLM do Chrome/Edge.
/// Dependências: pacote NavegadorHost instalado ao lado do Agente.
/// Impacto: permite ativar o conector web em máquinas atualizadas de versões anteriores sem reinstalação manual.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServicoConectorNavegador(ILogger<ServicoConectorNavegador> logger) : BackgroundService
{
    private const string NomeHost = "br.com.clara.agent";
    private const string IdExtensao = "diidcakejmmckhhbggepbblogonddjml";

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var pastaAgente = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var raiz = pastaAgente.Parent?.FullName;
            if (string.IsNullOrWhiteSpace(raiz)) return Task.CompletedTask;
            var hostExe = Path.Combine(raiz, "NavegadorHost", "Clara.NavegadorHost.exe");
            if (!File.Exists(hostExe))
            {
                logger.LogInformation("Native Messaging Host ainda não está presente no pacote instalado.");
                return Task.CompletedTask;
            }

            var pastaManifesto = Path.Combine(ConfiguracaoAgenteLocal.DiretorioDados, "navegador");
            Directory.CreateDirectory(pastaManifesto);
            var caminhoManifesto = Path.Combine(pastaManifesto, NomeHost + ".json");
            var manifesto = new
            {
                name = NomeHost,
                description = "Clara - Contexto web autorizado",
                path = hostExe,
                type = "stdio",
                allowed_origins = new[] { $"chrome-extension://{IdExtensao}/" }
            };
            File.WriteAllText(caminhoManifesto, JsonSerializer.Serialize(manifesto, new JsonSerializerOptions { WriteIndented = true }));

            Registrar(Registry.LocalMachine, $@"SOFTWARE\Google\Chrome\NativeMessagingHosts\{NomeHost}", caminhoManifesto);
            Registrar(Registry.LocalMachine, $@"SOFTWARE\Microsoft\Edge\NativeMessagingHosts\{NomeHost}", caminhoManifesto);
            logger.LogInformation("Native Messaging Host da Clara registrado para Chrome e Edge.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível registrar o Native Messaging Host. A telemetria principal do agente continua funcionando.");
        }
        return Task.CompletedTask;
    }

    private static void Registrar(RegistryKey raiz, string subChave, string manifesto)
    {
        using var chave = raiz.CreateSubKey(subChave, writable: true);
        chave?.SetValue(string.Empty, manifesto, RegistryValueKind.String);
    }
}
