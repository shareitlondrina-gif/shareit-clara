// Clara
// Programador: Paulo Andrade
// Objetivo: controlar uma entrada HTTP da Clara.
// Origem: requisições autenticadas do navegador/API.
// Destino: serviços, repositórios e resposta HTTP/View.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Clara.Comum.Configuracao;
using Clara.Dados.Repositorios;
using Clara.Dominio.Contratos;
using Clara.Web.Servicos;

namespace Clara.Web.Controllers;

/// <summary>
/// Objetivo: expor a API segura consumida pelos agentes Windows.
/// Origem: Clara.Agente.
/// Destino: registro, heartbeat, telemetria e atualização automática.
/// Dependências: RepositorioAgentes e manifesto publicado.
/// Impacto: é o contrato principal entre estação monitorada e servidor.
/// Desenvolvedor: Paulo Andrade
/// </summary>
[ApiController]
[Route("api/v1/agentes")]
[EnableRateLimiting("api-agente")]
public sealed class ApiAgentesController(
    RepositorioAgentes repositorio,
    RepositorioInteligencia inteligencia,
    ServicoArmazenamentoEvidencias armazenamento,
    ConfiguracaoClara configuracao,
    IWebHostEnvironment ambiente) : ControllerBase
{
    [HttpPost("registrar")]
    public async Task<IActionResult> Registrar(RegistroAgenteSolicitacao solicitacao, CancellationToken ct)
    {
        var resposta = await repositorio.RegistrarAsync(solicitacao, ObterIpOrigem(), ct);
        return resposta is null ? Unauthorized(new { erro = "Token de instalação inválido, expirado ou já utilizado." }) : Ok(resposta);
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(HeartbeatAgente heartbeat, CancellationToken ct)
    {
        var contexto = await AutenticarAsync(ct);
        if (contexto is null) return Unauthorized();
        await repositorio.RegistrarHeartbeatAsync(contexto, heartbeat, ObterIpOrigem(), ct);
        return Ok(new { servidorUtc = DateTimeOffset.UtcNow });
    }

    [HttpPost("atividade")]
    public async Task<IActionResult> Atividade(LotePeriodosAtividade lote, CancellationToken ct)
    {
        var contexto = await AutenticarAsync(ct);
        if (contexto is null) return Unauthorized();
        await repositorio.InserirPeriodosAsync(contexto, lote.Periodos, ct);
        return Ok(new { recebidos = lote.Periodos.Count });
    }

    [HttpPost("navegacao")]
    public async Task<IActionResult> Navegacao(LotePeriodosNavegacao lote, CancellationToken ct)
    {
        var contexto = await AutenticarAsync(ct);
        if (contexto is null) return Unauthorized();
        await repositorio.InserirNavegacaoAsync(contexto, lote.Periodos, ct);
        return Ok(new { recebidos = lote.Periodos.Count });
    }

    [HttpPost("evidencias/imagem")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> EnviarEvidenciaImagem(IFormFile arquivo, string? titulo, string? descricao, DateTimeOffset? capturadaEmUtc, CancellationToken ct)
    {
        var contexto = await AutenticarAsync(ct);
        if (contexto is null) return Unauthorized();
        var politica = await inteligencia.ObterConfiguracoesAsync(contexto.TenantId, ct);
        if (!politica.CapturaScreenshots) return StatusCode(StatusCodes.Status403Forbidden, new { erro = "Captura de screenshots desabilitada pela política da empresa." });
        if (arquivo is null || arquivo.Length <= 0) return BadRequest(new { erro = "Arquivo ausente." });
        await using var fluxo = arquivo.OpenReadStream();
        var item = await armazenamento.GravarEvidenciaAsync(contexto.TenantId, contexto.AtivoId, titulo ?? "Captura autorizada", descricao, arquivo.ContentType, fluxo, capturadaEmUtc ?? DateTimeOffset.UtcNow, ct);
        return Ok(new { item.Id, item.Tamanho, item.Sha256 });
    }

    [HttpGet("politica")]
    public async Task<IActionResult> Politica(CancellationToken ct)
    {
        var contexto = await AutenticarAsync(ct);
        if (contexto is null) return Unauthorized();
        return Ok(await inteligencia.ObterPoliticaAgenteAsync(contexto.TenantId, contexto.AtivoId, ct));
    }

    [HttpGet("atualizacao")]
    public async Task<IActionResult> Atualizacao(CancellationToken ct)
    {
        if (await AutenticarAsync(ct) is null) return Unauthorized();
        var caminho = Path.Combine(ambiente.ContentRootPath, "atualizacoes", "agente", "manifesto.json");
        if (!System.IO.File.Exists(caminho)) return NoContent();
        var manifesto = JsonSerializer.Deserialize<ManifestoAtualizacaoAgente>(await System.IO.File.ReadAllTextAsync(caminho, ct), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (manifesto is null) return NoContent();
        var url = manifesto.UrlPacote;
        if (Uri.TryCreate(url, UriKind.Relative, out _))
            url = new Uri(new Uri(configuracao.Servidor.UrlPublica.TrimEnd('/') + "/"), url.TrimStart('/')).ToString();
        return Ok(manifesto with { UrlPacote = url });
    }


    [HttpGet("atualizacao/pacote/{versao}")]
    public async Task<IActionResult> PacoteAtualizacao(string versao, CancellationToken ct)
    {
        if (await AutenticarAsync(ct) is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(versao) || versao.Any(c => !(char.IsDigit(c) || c == '.'))) return BadRequest();
        var arquivo = Path.Combine(ambiente.ContentRootPath, "atualizacoes", "agente", versao, $"Clara-Agent-v{versao}.zip");
        if (!System.IO.File.Exists(arquivo)) return NotFound();
        return PhysicalFile(arquivo, "application/zip", Path.GetFileName(arquivo), enableRangeProcessing: true);
    }

    [HttpPost("manutencao/autorizar")]
    public async Task<IActionResult> AutorizarManutencao([FromBody] AutorizacaoManutencaoSolicitacao solicitacao, CancellationToken ct)
    {
        var contexto = await AutenticarAsync(ct);
        if (contexto is null) return Unauthorized();
        var autorizado = await repositorio.AutorizarManutencaoAsync(contexto, solicitacao.Codigo, solicitacao.Operacao, ct);
        return autorizado ? Ok(new { autorizado = true, operacao = solicitacao.Operacao, servidorUtc = DateTimeOffset.UtcNow }) : Unauthorized(new { erro = "Código inválido, expirado, revogado ou já utilizado." });
    }

    [HttpPost("atualizacao/status")]
    public async Task<IActionResult> StatusAtualizacao(StatusAtualizacaoAgente status, CancellationToken ct)
    {
        var contexto = await AutenticarAsync(ct);
        if (contexto is null) return Unauthorized();
        await repositorio.RegistrarStatusAtualizacaoAsync(contexto, status, ct);
        return Ok();
    }

    private string? ObterIpOrigem()
    {
        var encaminhado = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(encaminhado))
            return encaminhado.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private async Task<ContextoAgente?> AutenticarAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(Request.Headers["X-Clara-Agente"].FirstOrDefault(), out var identificador)) return null;
        var token = Request.Headers["X-Clara-Token"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(token) ? null : await repositorio.AutenticarAsync(identificador, token, ct);
    }
}
