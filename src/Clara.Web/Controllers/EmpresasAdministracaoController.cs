// Clara
// Programador: Paulo Andrade
// Objetivo: controlar uma entrada HTTP da Clara.
// Origem: requisições autenticadas do navegador/API.
// Destino: serviços, repositórios e resposta HTTP/View.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Clara.Dados.Repositorios;

namespace Clara.Web.Controllers;

[Authorize(Roles="Administrador")]
public sealed class EmpresasAdministracaoController(RepositorioAdministracao repositorio) : Controller
{
    [HttpGet("administracao/empresas")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await repositorio.ListarEmpresasDetalhadasAsync(Tenant(),ct));

    [HttpPost("administracao/empresas/criar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Criar(string nome,string? documento,CancellationToken ct)
    {
        try{await repositorio.CriarEmpresaClienteAsync(Tenant(),nome,documento,ct);TempData["Mensagem"]="Empresa cadastrada. Agora ela já pode receber códigos de registro de dispositivos.";}
        catch(Exception ex){TempData["Erro"]=ex.Message;}
        return RedirectToAction(nameof(Index));
    }
    private Guid Tenant()=>Guid.TryParse(User.FindFirstValue("tenant_id"),out var id)?id:throw new UnauthorizedAccessException();
}
