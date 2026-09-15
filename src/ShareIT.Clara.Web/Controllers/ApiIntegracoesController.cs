using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShareIT.Clara.Dados.Repositorios;

namespace ShareIT.Clara.Web.Controllers;

/// <summary>
/// Objetivo: receber contexto técnico opcional de outros produtos Share IT sem tornar a Clara dependente deles.
/// Origem: .VISÃO contratado e explicitamente integrado ao mesmo tenant.
/// Destino: Contexto TI e Pulso da Operação.
/// Segurança: autenticação por token dedicado armazenado somente como SHA-256; a Clara funciona normalmente sem este endpoint.
/// Desenvolvedor: Paulo Andrade
/// </summary>
[ApiController]
[Route("api/v1/integracoes")]
[EnableRateLimiting("api-agente")]
public sealed class ApiIntegracoesController(RepositorioGestaoExecutiva gestao) : ControllerBase
{
    [HttpPost("visao/contexto")]
    public async Task<IActionResult> ReceberContextoVisao(ContextoVisaoEntrada entrada, CancellationToken ct)
    {
        if (!Guid.TryParse(Request.Headers["X-Clara-Tenant"].FirstOrDefault(), out var tenantId)) return Unauthorized();
        var token = Request.Headers["X-Clara-Visao-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token) || !await gestao.ValidarTokenVisaoAsync(tenantId, token, ct)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(entrada.Resumo)) return BadRequest(new { erro = "Resumo é obrigatório." });

        await gestao.RegistrarContextoVisaoAsync(
            tenantId, entrada.AtivoId, entrada.Hostname, entrada.Categoria ?? "Tecnologia", entrada.Severidade ?? "Informativa",
            entrada.Resumo, entrada.Detalhe, entrada.ReferenciaExterna, entrada.ObservadoEmUtc ?? DateTimeOffset.UtcNow, ct);
        return Ok(new { recebido = true, utc = DateTimeOffset.UtcNow });
    }
}

public sealed record ContextoVisaoEntrada(
    Guid? AtivoId,
    string? Hostname,
    string? Categoria,
    string? Severidade,
    string Resumo,
    string? Detalhe,
    string? ReferenciaExterna,
    DateTimeOffset? ObservadoEmUtc);
