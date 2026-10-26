// Clara
// Programador: Paulo Andrade
// Objetivo: executar coleta e comunicação do agente Windows.
// Origem: Windows, configuração local e API Clara.
// Destino: API Clara, telemetria autorizada e estado local.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Text.Json;
using Clara.Agente.Configuracao;
using Clara.Dominio.Modelos;

namespace Clara.Agente.Telemetria;

/// <summary>
/// Objetivo: preservar períodos de atividade quando servidor ou internet estiverem indisponíveis.
/// Origem: Named Pipe/processo de sessão.
/// Destino: envio posterior à API.
/// Dependências: armazenamento local em ProgramData.
/// Impacto: evita perda imediata de telemetria durante falhas temporárias de conectividade.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class FilaOffline
{
    private readonly SemaphoreSlim _trava = new(1, 1);
    private readonly string _arquivo;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FilaOffline()
    {
        var pasta = Path.Combine(ConfiguracaoAgenteLocal.DiretorioDados, "fila");
        Directory.CreateDirectory(pasta);
        _arquivo = Path.Combine(pasta, "periodos.jsonl");
    }

    public async Task AdicionarAsync(PeriodoAtividade periodo, CancellationToken ct = default)
    {
        await _trava.WaitAsync(ct);
        try { await File.AppendAllTextAsync(_arquivo, JsonSerializer.Serialize(periodo, _json) + Environment.NewLine, ct); }
        finally { _trava.Release(); }
    }

    public async Task<IReadOnlyList<PeriodoAtividade>> RetirarLoteAsync(int maximo, CancellationToken ct = default)
    {
        await _trava.WaitAsync(ct);
        try
        {
            if (!File.Exists(_arquivo)) return Array.Empty<PeriodoAtividade>();
            var linhas = await File.ReadAllLinesAsync(_arquivo, ct);
            var lote = linhas.Take(maximo).Select(l => JsonSerializer.Deserialize<PeriodoAtividade>(l, _json)).Where(x => x is not null).Cast<PeriodoAtividade>().ToArray();
            await File.WriteAllLinesAsync(_arquivo, linhas.Skip(maximo), ct);
            return lote;
        }
        finally { _trava.Release(); }
    }

    public async Task RecolocarNoInicioAsync(IEnumerable<PeriodoAtividade> periodos, CancellationToken ct = default)
    {
        var novos = periodos.Select(p => JsonSerializer.Serialize(p, _json)).ToArray();
        if (novos.Length == 0) return;
        await _trava.WaitAsync(ct);
        try
        {
            var existentes = File.Exists(_arquivo) ? await File.ReadAllLinesAsync(_arquivo, ct) : Array.Empty<string>();
            await File.WriteAllLinesAsync(_arquivo, novos.Concat(existentes), ct);
        }
        finally { _trava.Release(); }
    }
}
