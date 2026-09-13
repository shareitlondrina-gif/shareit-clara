using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareIT.Clara.Dados.Repositorios;
using ShareIT.Clara.Web.Servicos;

namespace ShareIT.Clara.Web.Controllers;

/// <summary>
/// Objetivo: administrar empresas/clientes do SaaS a partir do perfil Master.
/// Origem: usuário Master da Share IT.
/// Destino: tenants, empresas, administradores iniciais e empresa demonstração.
/// Dependências: RepositorioMaster e ServicoEmpresaDemonstracao.
/// Impacto: centraliza o onboarding multiempresa sem expor dados entre tenants.
/// Desenvolvedor: Paulo Andrade
/// </summary>
[Authorize(Roles = "Master")]
public sealed class MasterController(RepositorioMaster repositorio, ServicoEmpresaDemonstracao demonstracao) : Controller
{
    [HttpGet("master/empresas")]
    public async Task<IActionResult> Empresas(CancellationToken ct)
        => View(await repositorio.ListarEmpresasAsync(ct));

    [HttpPost("master/empresas/criar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarEmpresa(
        string nomeEmpresa,
        string? documento,
        string nomeAdministrador,
        string emailAdministrador,
        string senhaAdministrador,
        CancellationToken ct)
    {
        try
        {
            await repositorio.CriarEmpresaAsync(nomeEmpresa, documento, nomeAdministrador, emailAdministrador, senhaAdministrador, ct);
            TempData["Mensagem"] = "Empresa e administrador criados com sucesso.";
        }
        catch (Exception ex)
        {
            TempData["Erro"] = ex.Message;
        }
        return RedirectToAction(nameof(Empresas));
    }


    [HttpPost("master/empresas/{tenantId:guid}/assinatura")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtualizarAssinatura(
        Guid tenantId,
        string plano,
        string status,
        int limiteEstacoes,
        int retencaoDias,
        int armazenamentoGb,
        DateOnly? vencimento,
        string? observacao,
        CancellationToken ct)
    {
        try
        {
            await repositorio.AtualizarAssinaturaAsync(tenantId, plano, status, limiteEstacoes, retencaoDias, armazenamentoGb, vencimento, observacao, ct);
            TempData["Mensagem"] = "Plano e limites do tenant atualizados.";
        }
        catch (Exception ex)
        {
            TempData["Erro"] = ex.Message;
        }
        return RedirectToAction(nameof(Empresas));
    }

    [HttpPost("master/demonstracao/criar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarDemonstracao(CancellationToken ct)
    {
        try
        {
            await demonstracao.CriarOuAtualizarAsync(ct);
            TempData["Mensagem"] = "Empresa demonstração criada/atualizada. Login: demonstracao@clara.local | senha: demonstracao";
        }
        catch (Exception ex)
        {
            TempData["Erro"] = $"Não foi possível preparar a demonstração: {ex.Message}";
        }
        return RedirectToAction(nameof(Empresas));
    }
}
