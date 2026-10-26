// Clara
// Programador: Paulo Andrade
// Objetivo: controlar uma entrada HTTP da Clara.
// Origem: requisições autenticadas do navegador/API.
// Destino: serviços, repositórios e resposta HTTP/View.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Clara.Dados.Repositorios;

namespace Clara.Web.Controllers;

/// <summary>
/// Objetivo: permitir ao administrador da empresa criar usuários e controlar módulos do perfil padrão.
/// Origem: área Administração do tenant.
/// Destino: usuarios, perfis_acesso e perfil_modulos.
/// Dependências: RepositorioUsuariosAcesso.
/// Impacto: separa credenciais individuais e aplica autorização por módulo.
/// Desenvolvedor: Paulo Andrade
/// </summary>
[Authorize(Roles = "Administrador")]
public sealed class AdministracaoAcessoController(RepositorioUsuariosAcesso repositorio) : Controller
{
    [HttpGet("administracao/usuarios")]
    public async Task<IActionResult> Usuarios(CancellationToken ct)
    {
        if (!TemModulo("usuarios")) return Forbid();
        return View(await repositorio.ListarUsuariosAsync(ObterTenant(), ct));
    }

    [HttpPost("administracao/usuarios/criar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarUsuario(string nome, string email, string senha, string perfilCodigo, CancellationToken ct)
    {
        if (!TemModulo("usuarios")) return Forbid();
        try
        {
            await repositorio.CriarUsuarioAsync(ObterTenant(), nome, email, senha, perfilCodigo, ct);
            TempData["Mensagem"] = $"Usuário criado com o perfil {(perfilCodigo?.Equals("SUPERVISOR", StringComparison.OrdinalIgnoreCase) == true ? "Supervisor" : "Padrão")}.";
        }
        catch (Exception ex)
        {
            TempData["Erro"] = ex.Message;
        }
        return RedirectToAction(nameof(Usuarios));
    }

    [HttpPost("administracao/usuarios/{usuarioId:guid}/perfil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtualizarPerfilUsuario(Guid usuarioId, string perfilCodigo, CancellationToken ct)
    {
        if (!TemModulo("usuarios")) return Forbid();
        try
        {
            await repositorio.AtualizarPerfilUsuarioAsync(ObterTenant(), usuarioId, perfilCodigo, ct);
            TempData["Mensagem"] = "Perfil do usuário atualizado. A nova permissão será aplicada no próximo login.";
        }
        catch (Exception ex)
        {
            TempData["Erro"] = ex.Message;
        }
        return RedirectToAction(nameof(Usuarios));
    }

    [HttpGet("administracao/perfis")]
    public async Task<IActionResult> Perfis(CancellationToken ct)
    {
        if (!TemModulo("perfis")) return Forbid();
        return View(await repositorio.ListarPerfisAsync(ObterTenant(), ct));
    }

    [HttpPost("administracao/perfis/{perfilId:guid}/modulos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarModulos(Guid perfilId, string[]? modulos, CancellationToken ct)
    {
        if (!TemModulo("perfis")) return Forbid();
        try
        {
            await repositorio.AtualizarModulosPerfilAsync(ObterTenant(), perfilId, modulos ?? Array.Empty<string>(), ct);
            TempData["Mensagem"] = "Permissões do perfil atualizadas. Usuários desse perfil recebem as alterações no próximo login.";
        }
        catch (Exception ex)
        {
            TempData["Erro"] = ex.Message;
        }
        return RedirectToAction(nameof(Perfis));
    }

    private bool TemModulo(string codigo) => User.HasClaim("modulo", codigo);
    private Guid ObterTenant() => Guid.Parse(User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException());
}
