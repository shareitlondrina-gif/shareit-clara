// Clara
// Programador: Paulo Andrade
// Objetivo: controlar uma entrada HTTP da Clara.
// Origem: requisições autenticadas do navegador/API.
// Destino: serviços, repositórios e resposta HTTP/View.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Clara.Dados.Repositorios;

namespace Clara.Web.Controllers;

/// <summary>
/// Objetivo: expor dados analíticos do tenant autenticado para clientes Web/API autorizados.
/// Origem: sessão autenticada da Clara.
/// Destino: contratos de dashboard Agora, Aplicativos e Integrações.
/// Dependências: RepositorioDashboard e claims de tenant.
/// Impacto: oferece leitura estruturada dos indicadores sem misturar dados entre empresas.
/// Desenvolvedor: Paulo Andrade
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class ApiDashboardController(RepositorioDashboard repositorio) : ControllerBase
{
    [HttpGet("agora")]
    public async Task<IActionResult> Agora(CancellationToken ct)
    {
        if (!TryTenant(out var tenantId)) return Unauthorized();
        return Ok(await repositorio.ObterAgoraAsync(tenantId, ct));
    }

    [HttpGet("aplicativos")]
    public async Task<IActionResult> Aplicativos([FromQuery] int dias = 7, CancellationToken ct = default)
    {
        if (!TryTenant(out var tenantId)) return Unauthorized();
        return Ok(await repositorio.ObterAplicativosAsync(tenantId, dias, ct));
    }

    [HttpGet("integracoes")]
    public async Task<IActionResult> Integracoes(CancellationToken ct)
    {
        if (!TryTenant(out var tenantId)) return Unauthorized();
        return Ok(await repositorio.ObterIntegracoesAsync(tenantId, ct));
    }

    private bool TryTenant(out Guid tenantId)
        => Guid.TryParse(User.FindFirst("tenant_id")?.Value, out tenantId);
}
