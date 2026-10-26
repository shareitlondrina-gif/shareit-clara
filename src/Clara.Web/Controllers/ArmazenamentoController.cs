// Clara
// Programador: Paulo Andrade
// Objetivo: controlar uma entrada HTTP da Clara.
// Origem: requisições autenticadas do navegador/API.
// Destino: serviços, repositórios e resposta HTTP/View.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Clara.Web.Servicos;

namespace Clara.Web.Controllers;

[Authorize(Roles="Administrador")]
public sealed class ArmazenamentoController(ServicoArmazenamentoEvidencias armazenamento) : Controller
{
    [HttpGet("administracao/armazenamento")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await armazenamento.ObterConfiguracaoAsync(Tenant(),ct));

    [HttpPost("administracao/armazenamento/salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salvar(string provedor,string? endpoint,string? containerBucket,string? pastaBase,string? regiao,string? accessKeyId,string? secretAccessKey,CancellationToken ct)
    {
        try{await armazenamento.SalvarConfiguracaoAsync(Tenant(),provedor,endpoint,containerBucket,pastaBase,regiao,accessKeyId,secretAccessKey,ct);TempData["Mensagem"]="Configuração de armazenamento salva. Execute o teste antes de liberar evidências reais.";}
        catch(Exception ex){TempData["Erro"]=ex.Message;}
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("administracao/armazenamento/testar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Testar(CancellationToken ct)
    {
        try{await armazenamento.TestarAsync(Tenant(),ct);TempData["Mensagem"]="Armazenamento validado com gravação e exclusão de arquivo de teste.";}
        catch(Exception ex){TempData["Erro"]=$"Falha no teste: {ex.Message}";}
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("evidencias/arquivo/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Arquivo(Guid id,CancellationToken ct)
    {
        var item=await armazenamento.LerEvidenciaAsync(Tenant(),id,ct);return item is null?NotFound():File(item.Value.Conteudo,item.Value.MimeType,enableRangeProcessing:false);
    }

    private Guid Tenant()=>Guid.TryParse(User.FindFirstValue("tenant_id"),out var id)?id:throw new UnauthorizedAccessException("Tenant ausente.");
}
