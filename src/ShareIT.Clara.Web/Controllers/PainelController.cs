using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareIT.Clara.Dados.Repositorios;

namespace ShareIT.Clara.Web.Controllers;

/// <summary>
/// Objetivo: entregar a primeira experiência visual operacional do Share IT .Clara.
/// Origem: usuário administrativo autenticado.
/// Destino: visão “Agora”, criação de ativo e token de instalação.
/// Dependências: RepositorioDashboard e RepositorioAdministracao.
/// Impacto: valida ponta a ponta servidor, banco, agente e telemetria.
/// Desenvolvedor: Paulo Andrade
/// </summary>
[Authorize]
public sealed class PainelController(RepositorioDashboard dashboard, RepositorioAdministracao administracao) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var tenantId = ObterGuid("tenant_id");
        ViewBag.Empresas = await administracao.ListarEmpresasAsync(tenantId, ct);
        return View(await dashboard.ObterAgoraAsync(tenantId, ct));
    }



    [HttpGet("timeline/{agenteId:guid}")]
    public async Task<IActionResult> Timeline(Guid agenteId, CancellationToken ct)
    {
        var modelo = await dashboard.ObterTimelineAsync(ObterGuid("tenant_id"), agenteId, DateTimeOffset.UtcNow.AddHours(-24), ct);
        return modelo is null ? NotFound() : View(modelo);
    }

    [HttpPost("administracao/gerar-token")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GerarToken(Guid empresaId, string hostname, string? responsavel, string? patrimonio, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return BadRequest("Hostname é obrigatório.");
        var token = await administracao.CriarAtivoETokenAsync(
            ObterGuid("tenant_id"), ObterGuid(System.Security.Claims.ClaimTypes.NameIdentifier), empresaId, hostname, responsavel, patrimonio, ct);
        TempData["TokenInstalacao"] = token.Token;
        TempData["TokenExpira"] = token.ExpiraEmUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        return RedirectToAction(nameof(Index));
    }

    private Guid ObterGuid(string tipoClaim)
    {
        var valor = User.FindFirst(tipoClaim)?.Value ?? throw new UnauthorizedAccessException("Claim ausente.");
        return Guid.Parse(valor);
    }
}
