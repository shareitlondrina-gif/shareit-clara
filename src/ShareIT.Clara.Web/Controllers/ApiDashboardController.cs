using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareIT.Clara.Dados.Repositorios;

namespace ShareIT.Clara.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class ApiDashboardController(RepositorioDashboard repositorio) : ControllerBase
{
    [HttpGet("agora")]
    public async Task<IActionResult> Agora(CancellationToken ct)
    {
        var tenant = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenant, out var tenantId)) return Unauthorized();
        return Ok(await repositorio.ObterAgoraAsync(tenantId, ct));
    }
}
