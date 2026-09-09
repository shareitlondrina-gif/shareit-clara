using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShareIT.Clara.Dados.Repositorios;

namespace ShareIT.Clara.Web.Controllers;

/// <summary>
/// Objetivo: controlar login e logout do painel administrativo.
/// Origem: formulário Web.
/// Destino: cookie autenticado com usuário, tenant, perfil e módulos permitidos.
/// Dependências: RepositorioAutenticacao e ASP.NET Core Cookie Authentication.
/// Impacto: protege dashboard, gestão Master e módulos administrativos.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class AutenticacaoController(RepositorioAutenticacao repositorio) : Controller
{
    [HttpGet("entrar")]
    public IActionResult Entrar() => View();

    [HttpPost("entrar")]
    [EnableRateLimiting("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(string email, string senha, CancellationToken ct)
    {
        var usuario = await repositorio.AutenticarAsync(email, senha, ct);
        if (usuario is null)
        {
            ViewBag.Erro = "E-mail ou senha inválidos.";
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Nome),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Role, usuario.Perfil),
            new("tenant_id", usuario.TenantId.ToString())
        };
        if (usuario.EmpresaId is not null) claims.Add(new Claim("empresa_id", usuario.EmpresaId.Value.ToString()));
        claims.AddRange(usuario.Modulos.Select(m => new Claim("modulo", m)));

        var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));
        return usuario.Perfil.Equals("Master", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction("Empresas", "Master")
            : RedirectToAction("Index", "Painel");
    }

    [HttpGet("acesso-negado")]
    public IActionResult AcessoNegado() => View();

    [HttpPost("sair")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Entrar));
    }
}
