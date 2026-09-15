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
/// Fora do período efetivo de monitoramento, responde apenas ao status e não recebe/persiste domínio.
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

            var politica = LerPoliticaLocal();
            if (string.Equals(mensagem.Acao, "status", StringComparison.OrdinalIgnoreCase))
            {
                await ResponderAsync(saida, new { ok = true, monitorar = politica.PodeMonitorarAgora });
                return 0;
            }

            // Defesa em profundidade: mesmo que uma extensão antiga envie domínio diretamente,
            // o host não encaminha nada fora da janela efetiva de monitoramento.
            if (!politica.PodeMonitorarAgora)
            {
                await ResponderAsync(saida, new { ok = true, monitorar = false });
                return 0;
            }

            var dominio = NormalizarDominio(mensagem.Dominio);
            var navegador = NormalizarNavegador(mensagem.Navegador);
            if (dominio is null)
            {
                await ResponderAsync(saida, new { ok = false, erro = "Domínio inválido." });
                return 3;
            }

            if (DominioExcluido(dominio, politica.DominiosExcluidos))
            {
                // Não expõe a lista de exclusões à extensão e não encaminha domínio ao agente.
                await ResponderAsync(saida, new { ok = true, monitorar = false });
                return 0;
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
            await ResponderAsync(saida, new { ok = true, monitorar = true, dominio });
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

    private static PoliticaLocal LerPoliticaLocal()
    {
        try
        {
            var caminho = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ShareIT", "Clara", "configuracao-agente.json");
            if (!File.Exists(caminho)) return PoliticaLocal.Bloqueada;

            using var doc = JsonDocument.Parse(File.ReadAllText(caminho));
            var raiz = doc.RootElement;
            if (!raiz.TryGetProperty("monitorarUrlsDominios", out var monitorar) || monitorar.ValueKind != JsonValueKind.True)
                return PoliticaLocal.Bloqueada;

            var horarioInicio = raiz.TryGetProperty("horarioInicio", out var i) ? i.GetString() ?? "08:00" : "08:00";
            var horarioFim = raiz.TryGetProperty("horarioFim", out var f) ? f.GetString() ?? "18:00" : "18:00";
            var diasMonitorados = raiz.TryGetProperty("diasMonitorados", out var d) ? d.GetString() ?? "1,2,3,4,5" : "1,2,3,4,5";
            var fusoHorario = raiz.TryGetProperty("fusoHorario", out var z) ? z.GetString() ?? "America/Sao_Paulo" : "America/Sao_Paulo";

            var dominiosExcluidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (raiz.TryGetProperty("dominiosExcluidos", out var exclusoes) && exclusoes.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in exclusoes.EnumerateArray())
                {
                    var valor = item.GetString()?.Trim().Trim('.').ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(valor)) dominiosExcluidos.Add(valor);
                }
            }

            var agora = ConverterParaFusoConfigurado(DateTimeOffset.UtcNow, fusoHorario);
            var isoDia = ((int)agora.DayOfWeek + 6) % 7 + 1;
            var dias = diasMonitorados
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var n) ? n : 0)
                .ToHashSet();
            if (dias.Count > 0 && !dias.Contains(isoDia)) return new PoliticaLocal(false, dominiosExcluidos);

            var inicio = TimeSpan.TryParse(horarioInicio, out var hi) ? hi : TimeSpan.FromHours(8);
            var fim = TimeSpan.TryParse(horarioFim, out var hf) ? hf : TimeSpan.FromHours(18);
            var atual = agora.TimeOfDay;
            var permitido = fim >= inicio ? atual >= inicio && atual <= fim : atual >= inicio || atual <= fim;
            return new PoliticaLocal(permitido, dominiosExcluidos);
        }
        catch
        {
            // Falha fechada: sem política legível, não coleta contexto web.
            return PoliticaLocal.Bloqueada;
        }
    }

    private static DateTimeOffset ConverterParaFusoConfigurado(DateTimeOffset agora, string fusoHorario)
    {
        try
        {
            var zona = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(fusoHorario) ? "America/Sao_Paulo" : fusoHorario.Trim());
            return TimeZoneInfo.ConvertTime(agora, zona);
        }
        catch
        {
            // Fallback conservador: usa relógio local apenas quando a configuração de fuso for inválida.
            return agora.ToLocalTime();
        }
    }

    private static bool DominioExcluido(string dominio, IReadOnlyCollection<string> exclusoes)
    {
        var d = dominio.Trim().Trim('.').ToLowerInvariant();
        return exclusoes.Any(x => d.Equals(x, StringComparison.OrdinalIgnoreCase)
                                || d.EndsWith("." + x, StringComparison.OrdinalIgnoreCase));
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

    private sealed record PoliticaLocal(bool PodeMonitorarAgora, IReadOnlyCollection<string> DominiosExcluidos)
    {
        public static PoliticaLocal Bloqueada { get; } = new(false, Array.Empty<string>());
    }

    private sealed record MensagemExtensao(string? Acao, string? Dominio, string? Navegador, string? MomentoUtc);
}
