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
/// Destino: cookie autenticado com usuário, tenant e perfil.
/// Dependências: RepositorioAutenticacao e ASP.NET Core Cookie Authentication.
/// Impacto: protege dashboard e operações administrativas.
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

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(ClaimTypes.Role, usuario.Perfil),
            new Claim("tenant_id", usuario.TenantId.ToString())
        };
        var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));
        return RedirectToAction("Index", "Painel");
    }

    [HttpPost("sair")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Entrar));
    }
}
