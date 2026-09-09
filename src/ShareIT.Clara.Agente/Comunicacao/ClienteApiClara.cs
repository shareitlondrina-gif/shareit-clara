using System.Net.Http.Json;
using System.Reflection;
using ShareIT.Clara.Agente.Configuracao;
using ShareIT.Clara.Agente.Identidade;
using ShareIT.Clara.Agente.Inventario;
using ShareIT.Clara.Dominio.Contratos;
using ShareIT.Clara.Dominio.Modelos;

namespace ShareIT.Clara.Agente.Comunicacao;

/// <summary>
/// Objetivo: centralizar toda comunicação HTTPS/JSON do agente com o servidor.
/// Origem: serviços do agente e inventário automático da estação.
/// Destino: API /api/v1/agentes.
/// Dependências: HttpClient, identidade protegida do dispositivo e ColetorInventarioWindows.
/// Impacto: mantém autenticação, inventário e contratos de rede em um único componente.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ClienteApiClara(
    HttpClient http,
    ConfiguracaoAgenteLocal configuracao,
    GerenciadorIdentidadeAgente identidade,
    ColetorInventarioWindows inventario)
{
    public string VersaoAtual => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.3.2";

    public async Task<RegistroAgenteResposta?> RegistrarAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(configuracao.TokenInstalacao)) return null;
        var dados = await inventario.ObterAsync(false, ct);
        var solicitacao = new RegistroAgenteSolicitacao(configuracao.TokenInstalacao, VersaoAtual, dados);
        var resposta = await http.PostAsJsonAsync("api/v1/agentes/registrar", solicitacao, ct);
        if (!resposta.IsSuccessStatusCode) return null;
        return await resposta.Content.ReadFromJsonAsync<RegistroAgenteResposta>(cancellationToken: ct);
    }

    public async Task<bool> EnviarHeartbeatAsync(CancellationToken ct)
    {
        using var req = Criar(HttpMethod.Post, "api/v1/agentes/heartbeat");
        var dados = await inventario.ObterAsync(false, ct);
        req.Content = JsonContent.Create(new HeartbeatAgente(VersaoAtual, dados, null, DateTimeOffset.UtcNow));
        using var resposta = await http.SendAsync(req, ct);
        return resposta.IsSuccessStatusCode;
    }

    public async Task<bool> EnviarPeriodosAsync(IReadOnlyCollection<PeriodoAtividade> periodos, CancellationToken ct)
    {
        using var req = Criar(HttpMethod.Post, "api/v1/agentes/atividade");
        req.Content = JsonContent.Create(new LotePeriodosAtividade(periodos));
        using var resposta = await http.SendAsync(req, ct);
        return resposta.IsSuccessStatusCode;
    }

    public async Task<ManifestoAtualizacaoAgente?> ConsultarAtualizacaoAsync(CancellationToken ct)
    {
        using var req = Criar(HttpMethod.Get, "api/v1/agentes/atualizacao");
        using var resposta = await http.SendAsync(req, ct);
        if (resposta.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<ManifestoAtualizacaoAgente>(cancellationToken: ct);
    }

    public async Task BaixarPacoteAtualizacaoAsync(string url, string destino, CancellationToken ct)
    {
        using var req = Criar(HttpMethod.Get, url);
        using var resposta = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resposta.EnsureSuccessStatusCode();
        await using var origem = await resposta.Content.ReadAsStreamAsync(ct);
        await using var arquivo = File.Create(destino);
        await origem.CopyToAsync(arquivo, ct);
    }

    public async Task RegistrarStatusAtualizacaoAsync(StatusAtualizacaoAgente status, CancellationToken ct)
    {
        using var req = Criar(HttpMethod.Post, "api/v1/agentes/atualizacao/status");
        req.Content = JsonContent.Create(status);
        using var resposta = await http.SendAsync(req, ct);
        resposta.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage Criar(HttpMethod metodo, string caminho)
    {
        var id = identidade.Atual ?? throw new InvalidOperationException("Agente ainda não registrado.");
        var req = new HttpRequestMessage(metodo, caminho);
        req.Headers.Add("X-Clara-Agente", id.IdentificadorAgente.ToString());
        req.Headers.Add("X-Clara-Token", id.TokenDispositivo);
        return req;
    }
}
