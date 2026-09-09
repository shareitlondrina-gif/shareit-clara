using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ShareIT.Clara.Web.Controllers;

/// <summary>
/// Objetivo: apresentar uma página controlada quando uma exceção não tratada alcançar a camada Web.
/// Origem: middleware UseExceptionHandler.
/// Destino: tela de erro amigável e log estruturado no servidor.
/// Dependências: ASP.NET Core Diagnostics.
/// Impacto: evita página HTTP 500 vazia e mantém detalhes técnicos somente no log.
/// Desenvolvedor: Paulo Andrade
/// </summary>
[AllowAnonymous]
public sealed class ErroController(ILogger<ErroController> logger) : Controller
{
    [HttpGet("erro")]
    [HttpPost("erro")]
    public IActionResult Index()
    {
        var recurso = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (recurso?.Error is not null)
            logger.LogError(recurso.Error, "Erro não tratado no Share IT .Clara. Caminho original: {Caminho}", recurso.Path);

        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View();
    }
}
