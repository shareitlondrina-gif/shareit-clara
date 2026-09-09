using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareIT.Clara.Dados.Repositorios;

namespace ShareIT.Clara.Web.Controllers;

/// <summary>
/// Objetivo: entregar as experiências operacionais e administrativas do Share IT .Clara.
/// Origem: usuário administrativo autenticado.
/// Destino: Agora, estações, timeline, aplicativos, locais e integrações.
/// Dependências: RepositorioDashboard, RepositorioAdministracao e RepositorioContexto.
/// Impacto: concentra a navegação inicial da plataforma sem incorporar Service Desk, projetos ou suporte remoto.
/// Desenvolvedor: Paulo Andrade
/// </summary>
[Authorize]
public sealed class PainelController(
    RepositorioDashboard dashboard,
    RepositorioAdministracao administracao,
    RepositorioContexto contexto) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!TemModulo("agora")) return Forbid();
        var tenantId = ObterGuid("tenant_id");
        ViewBag.Empresas = await administracao.ListarEmpresasAsync(tenantId, ct);
        return View(await dashboard.ObterAgoraAsync(tenantId, ct));
    }

    [HttpGet("timeline/{agenteId:guid}")]
    public async Task<IActionResult> Timeline(Guid agenteId, CancellationToken ct)
    {
        if (!TemModulo("estacoes")) return Forbid();
        var modelo = await dashboard.ObterTimelineAsync(ObterGuid("tenant_id"), agenteId, DateTimeOffset.UtcNow.AddHours(-24), ct);
        return modelo is null ? NotFound() : View(modelo);
    }

    [HttpPost("administracao/gerar-token")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GerarToken(Guid empresaId, CancellationToken ct)
    {
        if (!TemModulo("estacoes")) return Forbid();
        var token = await administracao.CriarTokenInstalacaoAsync(
            ObterGuid("tenant_id"), ObterGuid(System.Security.Claims.ClaimTypes.NameIdentifier), empresaId, ct);
        TempData["TokenInstalacao"] = token.Token;
        TempData["TokenExpira"] = token.ExpiraEmUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        TempData["TokenServidor"] = token.UrlServidor;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("estacoes")]
    public async Task<IActionResult> Estacoes(CancellationToken ct)
    {
        if (!TemModulo("estacoes")) return Forbid();
        return View(await administracao.ListarEstacoesAsync(ObterGuid("tenant_id"), ct));
    }

    [HttpGet("estacoes/{ativoId:guid}")]
    public async Task<IActionResult> Estacao(Guid ativoId, CancellationToken ct)
    {
        if (!TemModulo("estacoes")) return Forbid();
        var tenant = ObterGuid("tenant_id");
        var modelo = await administracao.ObterEstacaoAsync(tenant, ativoId, ct);
        if (modelo is null) return NotFound();
        ViewBag.Empresas = await administracao.ListarEmpresasAsync(tenant, ct);
        ViewBag.Unidades = await administracao.ListarUnidadesAsync(tenant, ct);
        ViewBag.Departamentos = await administracao.ListarDepartamentosAsync(tenant, ct);
        return View(modelo);
    }

    [HttpPost("estacoes/{ativoId:guid}/salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarEstacao(Guid ativoId, Guid empresaId, Guid? unidadeId, Guid? departamentoId, string? patrimonio, string? responsavel, CancellationToken ct)
    {
        if (!TemModulo("estacoes")) return Forbid();
        await administracao.AtualizarCadastroEstacaoAsync(ObterGuid("tenant_id"), ativoId, empresaId, unidadeId, departamentoId, patrimonio, responsavel, ct);
        TempData["Mensagem"] = "Cadastro da estação atualizado.";
        return RedirectToAction(nameof(Estacao), new { ativoId });
    }

    [HttpGet("aplicativos")]
    public async Task<IActionResult> Aplicativos(int dias = 7, CancellationToken ct = default)
    {
        if (!TemModulo("aplicativos")) return Forbid();
        var tenant = ObterGuid("tenant_id");
        ViewBag.Categorias = await contexto.ListarCategoriasAsync(tenant, ct);
        ViewBag.Dias = Math.Clamp(dias, 1, 90);
        return View(await dashboard.ObterAplicativosAsync(tenant, dias, ct));
    }

    [HttpPost("aplicativos/classificar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClassificarAplicativo(string processo, string classificacao, Guid? categoriaId, int dias = 7, CancellationToken ct = default)
    {
        if (!TemModulo("aplicativos")) return Forbid();
        await contexto.ClassificarAplicativoAsync(ObterGuid("tenant_id"), processo, classificacao, categoriaId, ct);
        return RedirectToAction(nameof(Aplicativos), new { dias });
    }

    [HttpGet("indice-geral")]
    public async Task<IActionResult> IndiceGeral(CancellationToken ct)
    {
        if (!TemModulo("indice-geral")) return Forbid();
        return View(await dashboard.ObterIndiceGeralAsync(ObterGuid("tenant_id"), ct));
    }

    [HttpGet("integracoes")]
    public async Task<IActionResult> Integracoes(CancellationToken ct)
    {
        if (!TemModulo("integracoes")) return Forbid();
        return View(await dashboard.ObterIntegracoesAsync(ObterGuid("tenant_id"), ct));
    }

    [HttpGet("locais")]
    public async Task<IActionResult> Locais(CancellationToken ct)
    {
        if (!TemModulo("locais")) return Forbid();
        return View(await contexto.ListarLocaisAsync(ObterGuid("tenant_id"), ct));
    }

    [HttpPost("locais/adicionar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdicionarLocal(string nome, string faixaIp, string tipo, CancellationToken ct)
    {
        if (!TemModulo("locais")) return Forbid();
        await contexto.AdicionarLocalAsync(ObterGuid("tenant_id"), nome, faixaIp, tipo, ct);
        return RedirectToAction(nameof(Locais));
    }

    private bool TemModulo(string codigo) => User.IsInRole("Master") || User.HasClaim("modulo", codigo);

    private Guid ObterGuid(string tipoClaim)
    {
        var valor = User.FindFirst(tipoClaim)?.Value ?? throw new UnauthorizedAccessException("Claim ausente.");
        return Guid.Parse(valor);
    }
}
