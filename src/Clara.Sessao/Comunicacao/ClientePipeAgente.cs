// Clara
// Programador: Paulo Andrade
// Objetivo: observar a sessão interativa autorizada.
// Origem: sessão Windows do usuário.
// Destino: agente Clara por canal local controlado.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.IO.Pipes;
using System.Text.Json;
using Clara.Dominio.Modelos;

namespace Clara.Sessao.Comunicacao;

/// <summary>
/// Objetivo: enviar uma amostra de sessão ao serviço do agente por canal local.
/// Origem: coletor da sessão do usuário.
/// Destino: Named Pipe Clara.Sessao.
/// Dependências: Named Pipes do Windows.
/// Impacto: mantém a coleta interativa desacoplada do Windows Service.
/// Desenvolvedor: Paulo Andrade
/// </summary>
internal static class ClientePipeAgente
{
    public static async Task EnviarAsync(EventoSessao evento, CancellationToken ct)
    {
        await using var pipe = new NamedPipeClientStream(".", "Clara.Sessao", PipeDirection.Out, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(1500, ct);
        await using var escritor = new StreamWriter(pipe) { AutoFlush = true };
        await escritor.WriteLineAsync(JsonSerializer.Serialize(evento, new JsonSerializerOptions(JsonSerializerDefaults.Web)).AsMemory(), ct);
    }
}
