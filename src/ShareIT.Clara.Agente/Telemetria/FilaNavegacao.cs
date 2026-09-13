using System.Text.Json;
using ShareIT.Clara.Agente.Configuracao;
using ShareIT.Clara.Dominio.Modelos;

namespace ShareIT.Clara.Agente.Telemetria;

/// <summary>
/// Objetivo: preservar períodos de navegação autorizada quando a API estiver temporariamente indisponível.
/// Origem: ServidorPipeNavegacao.
/// Destino: envio posterior para /api/v1/agentes/navegacao.
/// Dependências: armazenamento local em ProgramData.
/// Impacto: evita perda imediata do contexto web sem armazenar URL completa ou conteúdo de página.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class FilaNavegacao
{
    private readonly SemaphoreSlim _trava = new(1, 1);
    private readonly string _arquivo;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FilaNavegacao()
    {
        var pasta = Path.Combine(ConfiguracaoAgenteLocal.DiretorioDados, "fila");
        Directory.CreateDirectory(pasta);
        _arquivo = Path.Combine(pasta, "navegacao.jsonl");
    }

    public async Task AdicionarAsync(PeriodoNavegacao periodo, CancellationToken ct = default)
    {
        await _trava.WaitAsync(ct);
        try { await File.AppendAllTextAsync(_arquivo, JsonSerializer.Serialize(periodo, _json) + Environment.NewLine, ct); }
        finally { _trava.Release(); }
    }

    public async Task<IReadOnlyList<PeriodoNavegacao>> RetirarLoteAsync(int maximo, CancellationToken ct = default)
    {
        await _trava.WaitAsync(ct);
        try
        {
            if (!File.Exists(_arquivo)) return Array.Empty<PeriodoNavegacao>();
            var linhas = await File.ReadAllLinesAsync(_arquivo, ct);
            var lote = linhas.Take(maximo)
                .Select(l => JsonSerializer.Deserialize<PeriodoNavegacao>(l, _json))
                .Where(x => x is not null).Cast<PeriodoNavegacao>().ToArray();
            await File.WriteAllLinesAsync(_arquivo, linhas.Skip(maximo), ct);
            return lote;
        }
        finally { _trava.Release(); }
    }

    public async Task RecolocarNoInicioAsync(IEnumerable<PeriodoNavegacao> periodos, CancellationToken ct = default)
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
