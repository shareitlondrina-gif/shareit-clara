using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ShareIT.Clara.Dominio.Modelos;

namespace ShareIT.Clara.NavegadorHost;

/// <summary>
/// Objetivo: receber do Chrome/Edge somente o domínio da aba ativa e encaminhá-lo ao agente local por Named Pipe.
/// Origem: extensão Share IT .Clara — Contexto Web via Native Messaging.
/// Destino: ShareIT.Clara.Agente, pipe ShareIT.Clara.Navegador.
/// Dependências: protocolo Native Messaging e Named Pipes do Windows.
/// Impacto: permite contexto web autorizado sem coletar caminho da URL, query string, título, conteúdo, senha ou texto digitado.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public static class Program
{
    private const string NomePipe = "ShareIT.Clara.Navegador";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<int> Main()
    {
        try
        {
            await using var entrada = Console.OpenStandardInput();
            await using var saida = Console.OpenStandardOutput();
            var mensagem = await LerMensagemAsync(entrada, CancellationToken.None);
            if (mensagem is null)
            {
                await ResponderAsync(saida, new { ok = false, erro = "Mensagem vazia." });
                return 2;
            }

            var dominio = NormalizarDominio(mensagem.Dominio);
            var navegador = NormalizarNavegador(mensagem.Navegador);
            if (dominio is null)
            {
                await ResponderAsync(saida, new { ok = false, erro = "Domínio inválido." });
                return 3;
            }

            var usuario = string.IsNullOrWhiteSpace(Environment.UserDomainName)
                ? Environment.UserName
                : $"{Environment.UserDomainName}\\{Environment.UserName}";
            var evento = new EventoNavegacao(DateTimeOffset.UtcNow, usuario, dominio, navegador);

            using var pipe = new NamedPipeClientStream(".", NomePipe, PipeDirection.Out, PipeOptions.Asynchronous);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await pipe.ConnectAsync(cts.Token);
            await using var escritor = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
            await escritor.WriteLineAsync(JsonSerializer.Serialize(evento, Json));
            await ResponderAsync(saida, new { ok = true, dominio });
            return 0;
        }
        catch (Exception ex)
        {
            // Native Messaging reserva stdout para o protocolo. Diagnóstico técnico deve ir somente para stderr.
            await Console.Error.WriteLineAsync($"Share IT .Clara NavegadorHost: {ex.GetType().Name}: {ex.Message}");
            try { await ResponderAsync(Console.OpenStandardOutput(), new { ok = false, erro = "Agente local indisponível." }); } catch { }
            return 1;
        }
    }

    private static async Task<MensagemExtensao?> LerMensagemAsync(Stream entrada, CancellationToken ct)
    {
        var tamanhoBytes = new byte[4];
        if (!await LerExatoAsync(entrada, tamanhoBytes, ct)) return null;
        var tamanho = BinaryPrimitives.ReadInt32LittleEndian(tamanhoBytes);
        if (tamanho <= 0 || tamanho > 16 * 1024) throw new InvalidDataException("Mensagem Native Messaging fora do limite permitido.");
        var corpo = new byte[tamanho];
        if (!await LerExatoAsync(entrada, corpo, ct)) throw new EndOfStreamException("Mensagem Native Messaging incompleta.");
        return JsonSerializer.Deserialize<MensagemExtensao>(corpo, Json);
    }

    private static async Task<bool> LerExatoAsync(Stream entrada, byte[] buffer, CancellationToken ct)
    {
        var deslocamento = 0;
        while (deslocamento < buffer.Length)
        {
            var lidos = await entrada.ReadAsync(buffer.AsMemory(deslocamento, buffer.Length - deslocamento), ct);
            if (lidos == 0) return false;
            deslocamento += lidos;
        }
        return true;
    }

    private static async Task ResponderAsync(Stream saida, object resposta)
    {
        var corpo = JsonSerializer.SerializeToUtf8Bytes(resposta, Json);
        var tamanho = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(tamanho, corpo.Length);
        await saida.WriteAsync(tamanho);
        await saida.WriteAsync(corpo);
        await saida.FlushAsync();
    }

    private static string? NormalizarDominio(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var dominio = valor.Trim().Trim('.').ToLowerInvariant();
        if (dominio.Length > 500 || dominio.Contains('/') || dominio.Contains('\\') || dominio.Contains(':') || dominio.Contains(' ') || !dominio.Contains('.')) return null;
        if (dominio.StartsWith("www.", StringComparison.Ordinal)) dominio = dominio[4..];
        return Uri.CheckHostName(dominio) is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6 ? dominio : null;
    }

    private static string NormalizarNavegador(string? valor)
        => valor?.Trim() switch
        {
            "Edge" => "Edge",
            "Chrome" => "Chrome",
            _ => "Chromium"
        };

    private sealed record MensagemExtensao(string? Dominio, string? Navegador, string? MomentoUtc);
}
