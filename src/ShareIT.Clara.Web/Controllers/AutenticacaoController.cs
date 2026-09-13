using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShareIT.Clara.Dados.Repositorios;
using ShareIT.Clara.Web.Servicos;

namespace ShareIT.Clara.Web.Controllers;

/// <summary>
/// Objetivo: controlar login e logout do painel administrativo.
/// Origem: formulário Web.
/// Destino: cookie autenticado com usuário, tenant, perfil e módulos permitidos.
/// Dependências: RepositorioAutenticacao, EstadoInicializacaoBanco e ASP.NET Core Cookie Authentication.
/// Impacto: protege dashboard, gestão Master e módulos administrativos, sem expor erro técnico ao usuário.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class AutenticacaoController(
    RepositorioAutenticacao repositorio,
    EstadoInicializacaoBanco estadoBanco,
    ILogger<AutenticacaoController> logger) : Controller
{
    [HttpGet("entrar")]
    public IActionResult Entrar()
    {
        if (estadoBanco.Falhou)
            ViewBag.Erro = "O banco do .Clara não concluiu a atualização. Acesse /api/health ou consulte o log de publicação.";
        else if (!estadoBanco.ConcluidaComSucesso)
            ViewBag.Aviso = "O .Clara ainda está concluindo a inicialização do banco. Aguarde alguns segundos e tente novamente.";

        return View();
    }

    [HttpPost("entrar")]
    [EnableRateLimiting("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(string email, string senha, CancellationToken ct)
    {
        if (estadoBanco.Falhou)
        {
            ViewBag.Erro = "O banco do .Clara não concluiu a atualização. A autenticação foi bloqueada para evitar erro HTTP 500. Consulte /api/health e o log de publicação.";
            return View();
        }

        if (!estadoBanco.ConcluidaComSucesso)
        {
            ViewBag.Erro = "O .Clara ainda está inicializando o banco. Aguarde alguns segundos e tente entrar novamente.";
            return View();
        }

        try
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
                new("tenant_id", usuario.TenantId.ToString()),
                new("tenant_nome", usuario.TenantNome)
            };
            if (usuario.EmpresaId is not null) claims.Add(new Claim("empresa_id", usuario.EmpresaId.Value.ToString()));
            if (!string.IsNullOrWhiteSpace(usuario.EmpresaNome)) claims.Add(new Claim("empresa_nome", usuario.EmpresaNome));
            claims.AddRange(usuario.Modulos.Select(m => new Claim("modulo", m)));

            var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));

            return usuario.Perfil.Equals("Master", StringComparison.OrdinalIgnoreCase)
                ? RedirectToAction("Empresas", "Master")
                : RedirectToAction("Index", "Painel");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha durante a autenticação Web do Share IT .Clara para o e-mail {Email}.", email);
            ViewBag.Erro = "Não foi possível validar o acesso neste momento. O sistema registrou o erro técnico; confirme /api/health e tente novamente.";
            return View();
        }
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
