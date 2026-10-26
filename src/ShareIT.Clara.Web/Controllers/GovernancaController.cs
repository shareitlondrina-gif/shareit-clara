using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShareIT.Clara.Dados.Repositorios;

namespace ShareIT.Clara.Web.Controllers;

/// <summary>
/// Objetivo: expor histórico/relatórios, preferências, alertas, notificações e governança.
/// Segurança: recursos administrativos respeitam módulos; aprovação jurídica usa link de uso único lógico com token secreto.
/// Desenvolvedor: Paulo Andrade
/// </summary>
[Authorize]
public sealed class GovernancaController(
    RepositorioGovernanca governanca,
    RepositorioGestaoExecutiva gestao,
    RepositorioAdministracao administracao,
    RepositorioUsuariosAcesso usuarios,
    IConfiguration configuracao) : Controller
{
    [HttpGet("relatorios")]
    public async Task<IActionResult> Relatorios(int dias = 7, DateOnly? inicio = null, DateOnly? fim = null, CancellationToken ct = default)
    {
        if (!TemModulo("relatorios")) return Forbid();
        ViewBag.Dias = Math.Clamp(dias, 1, 365);
        ViewBag.DataInicio = inicio;
        ViewBag.DataFim = fim;
        ViewBag.RelatoriosAgendados = await gestao.ListarRelatoriosAgendadosAsync(ObterTenant(), ct);
        ViewBag.FiltrosSalvos = await gestao.ListarFiltrosSalvosAsync(ObterTenant(), ObterUsuario(), ct);
        return View(await governanca.ObterRelatorioHistoricoAsync(ObterTenant(), dias, ct, inicio, fim));
    }


    [HttpPost("relatorios/agendar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgendarRelatorio(string nome,string periodicidade,string horaLocal,int? diaSemana,int? diaMes,string fusoHorario,string destinatarios,int dias=7,CancellationToken ct=default)
    {
        if(!TemModulo("relatorios"))return Forbid();
        if(string.IsNullOrWhiteSpace(destinatarios)){TempData["Erro"]="Informe ao menos um destinatário.";return RedirectToAction(nameof(Relatorios),new{dias});}
        await gestao.CriarRelatorioAgendadoAsync(ObterTenant(),ObterUsuario(),nome,periodicidade,horaLocal,diaSemana,diaMes,fusoHorario,destinatarios,dias,ct);
        TempData["Mensagem"]="Relatório agendado. O envio utilizará a fila oficial de e-mail da .Clara.";
        return RedirectToAction(nameof(Relatorios),new{dias});
    }

    [HttpPost("relatorios/agendados/{id:guid}/alternar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlternarRelatorio(Guid id,bool ativo,int dias=7,CancellationToken ct=default)
    {
        if(!TemModulo("relatorios"))return Forbid();
        await gestao.AlternarRelatorioAgendadoAsync(ObterTenant(),id,ativo,ct);
        return RedirectToAction(nameof(Relatorios),new{dias});
    }

    [HttpPost("relatorios/filtros/salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarFiltroRelatorio(string nome,int dias=7,DateOnly? inicio=null,DateOnly? fim=null,bool compartilhado=false,CancellationToken ct=default)
    {
        if(!TemModulo("relatorios"))return Forbid();
        await gestao.SalvarFiltroAsync(ObterTenant(),ObterUsuario(),nome,dias,compartilhado,inicio,fim,ct);
        TempData["Mensagem"]="Filtro de relatório salvo.";
        return RedirectToAction(nameof(Relatorios),new{dias,inicio,fim});
    }

    [HttpGet("perfil")]
    public async Task<IActionResult> Perfil(CancellationToken ct)
    {
        ViewBag.Tema = await governanca.ObterTemaUsuarioAsync(ObterUsuario(), ct);
        return View(await usuarios.ObterPerfilUsuarioAsync(ObterUsuario(), ct));
    }

    [HttpPost("perfil/dados")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3_000_000)]
    public async Task<IActionResult> SalvarPerfil(string nome, string? telefone, string? cargo, IFormFile? foto, bool removerFoto = false, CancellationToken ct = default)
    {
        try
        {
            await usuarios.SalvarPerfilUsuarioAsync(ObterUsuario(), nome, telefone, cargo, ct);
            if (removerFoto)
            {
                await usuarios.RemoverFotoPerfilAsync(ObterUsuario(), ct);
            }
            else if (foto is not null && foto.Length > 0)
            {
                if (foto.Length > 2 * 1024 * 1024) throw new ArgumentException("A foto deve possuir no máximo 2 MB.");
                await using var memoria = new MemoryStream();
                await foto.CopyToAsync(memoria, ct);
                var bytes = memoria.ToArray();
                var mime = DetectarMimeImagem(bytes) ?? throw new ArgumentException("Use uma imagem JPG, PNG ou WebP válida.");
                await usuarios.SalvarFotoPerfilAsync(ObterUsuario(), bytes, mime, ct);
            }

            var claims = User.Claims.Where(c => c.Type != ClaimTypes.Name).ToList();
            claims.Add(new Claim(ClaimTypes.Name, nome.Trim()));
            var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));
            TempData["Mensagem"] = "Perfil atualizado.";
        }
        catch (Exception ex)
        {
            TempData["Erro"] = ex.Message;
        }
        return RedirectToAction(nameof(Perfil));
    }

    [HttpGet("perfil/foto")]
    public async Task<IActionResult> FotoPerfil(CancellationToken ct)
    {
        Response.Headers["Cache-Control"] = "private, no-store, max-age=0";
        var foto = await usuarios.ObterFotoPerfilAsync(ObterUsuario(), ct);
        if (foto is not null) return File(foto.Conteudo, foto.MimeType);

        var nome = User.Identity?.Name ?? "Usuário";
        var iniciais = string.Concat(nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])));
        if (string.IsNullOrWhiteSpace(iniciais)) iniciais = "U";
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"160\" height=\"160\" viewBox=\"0 0 160 160\"><rect width=\"160\" height=\"160\" rx=\"80\" fill=\"#d9ecff\"/><text x=\"80\" y=\"96\" text-anchor=\"middle\" font-family=\"Segoe UI,Arial,sans-serif\" font-size=\"54\" font-weight=\"700\" fill=\"#0b4e90\">{iniciais}</text></svg>";
        return File(Encoding.UTF8.GetBytes(svg), "image/svg+xml; charset=utf-8");
    }

    [HttpPost("perfil/tema")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarTema(string tema, CancellationToken ct)
    {
        tema = tema is "Claro" or "Escuro" or "Sistema" ? tema : "Sistema";
        await governanca.SalvarTemaUsuarioAsync(ObterUsuario(), tema, ct);
        Response.Cookies.Append("clara_tema", tema, new CookieOptions
        {
            HttpOnly = false,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddYears(2)
        });
        TempData["Mensagem"] = "Preferência de aparência atualizada.";
        return RedirectToAction(nameof(Perfil));
    }

    [HttpGet("notificacoes/contagem")]
    public async Task<IActionResult> ContagemNotificacoes(CancellationToken ct)
    {
        if (User.IsInRole("Master")) return Json(new { naoLidas = 0 });
        await governanca.SincronizarNotificacoesAsync(ObterTenant(), ObterUsuario(), ct);
        return Json(new { naoLidas = await governanca.ContarNotificacoesNaoLidasAsync(ObterTenant(), ObterUsuario(), ct) });
    }

    [HttpGet("notificacoes/painel")]
    public async Task<IActionResult> PainelNotificacoes(CancellationToken ct)
    {
        if (User.IsInRole("Master")) return Forbid();
        await governanca.SincronizarNotificacoesAsync(ObterTenant(), ObterUsuario(), ct);
        return PartialView("_Notificacoes", await governanca.ObterNotificacoesAsync(ObterTenant(), ObterUsuario(), 40, ct));
    }

    [HttpPost("notificacoes/{id:guid}/lida")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarNotificacaoLida(Guid id, string? retorno, CancellationToken ct)
    {
        if (User.IsInRole("Master")) return Forbid();
        await governanca.MarcarNotificacaoLidaAsync(ObterTenant(), ObterUsuario(), id, ct);
        return LocalRedirect(string.IsNullOrWhiteSpace(retorno) || !Url.IsLocalUrl(retorno) ? "/" : retorno);
    }

    [HttpPost("notificacoes/todas-lidas")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarTodasLidas(CancellationToken ct)
    {
        if (User.IsInRole("Master")) return Forbid();
        await governanca.MarcarTodasNotificacoesLidasAsync(ObterTenant(), ObterUsuario(), ct);
        return RedirectToAction("Index", "Painel");
    }

    [HttpGet("administracao/alertas")]
    public async Task<IActionResult> Alertas(CancellationToken ct)
    {
        if (!User.IsInRole("Administrador") || !TemModulo("alertas")) return Forbid();
        var tenant = ObterTenant();
        ViewBag.Departamentos = await administracao.ListarDepartamentosAsync(tenant, ct);
        ViewBag.Estacoes = (await administracao.ListarEstacoesAsync(tenant, ct)).Estacoes;
        ViewBag.HistoricoAlertas = await governanca.ListarHistoricoAlertasAsync(tenant, 100, ct);
        return View(await governanca.ListarRegrasAlertasAsync(tenant, ct));
    }

    [HttpPost("administracao/alertas/criar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarAlerta(
        string nome, string tipo, int limiteMinutos, string escopoTipo, Guid? departamentoId, Guid? ativoId,
        string severidade, bool canalPlataforma, bool canalEmail, string? destinatariosEmail, string? destinatariosPerfis,
        bool gerarEvidencia, bool gerarOportunidade, CancellationToken ct)
    {
        if (!User.IsInRole("Administrador") || !TemModulo("alertas")) return Forbid();
        await governanca.CriarRegraAlertaAsync(ObterTenant(), nome, tipo, limiteMinutos, escopoTipo, departamentoId, ativoId,
            severidade, canalPlataforma, canalEmail, destinatariosEmail, destinatariosPerfis, gerarEvidencia, gerarOportunidade, ct);
        TempData["Mensagem"] = "Regra de alerta criada.";
        return RedirectToAction(nameof(Alertas));
    }

    [HttpPost("administracao/alertas/{id:guid}/alternar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlternarAlerta(Guid id, bool ativo, CancellationToken ct)
    {
        if (!User.IsInRole("Administrador") || !TemModulo("alertas")) return Forbid();
        await governanca.AlternarRegraAlertaAsync(ObterTenant(), id, ativo, ct);
        return RedirectToAction(nameof(Alertas));
    }

    [HttpPost("estacoes/{ativoId:guid}/jornada")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarJornadaEstacao(Guid ativoId, string jornadaInicio, string jornadaFim, int cargaHorariaDiariaMinutos,
        int margemAntesMinutos, int margemDepoisMinutos, string diasJornada, string monitoramentoInicio, string monitoramentoFim,
        string diasMonitorados, string fusoHorario, CancellationToken ct)
    {
        if (!User.IsInRole("Administrador") || !TemModulo("estacoes")) return Forbid();
        await governanca.SalvarJornadaEstacaoAsync(ObterTenant(), ativoId, ObterUsuario(), jornadaInicio, jornadaFim,
            cargaHorariaDiariaMinutos, margemAntesMinutos, margemDepoisMinutos, diasJornada, monitoramentoInicio,
            monitoramentoFim, diasMonitorados, fusoHorario, ct);
        TempData["Mensagem"] = "Jornada e período de monitoramento da estação atualizados.";
        return RedirectToAction("Estacao", "Painel", new { ativoId });
    }

    [HttpPost("estacoes/{ativoId:guid}/jornada/herdar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HerdarJornadaEstacao(Guid ativoId, CancellationToken ct)
    {
        if (!User.IsInRole("Administrador") || !TemModulo("estacoes")) return Forbid();
        await governanca.RemoverSobrescritaJornadaAsync(ObterTenant(), ativoId, ct);
        TempData["Mensagem"] = "A estação voltou a herdar a jornada padrão da empresa.";
        return RedirectToAction("Estacao", "Painel", new { ativoId });
    }

    [HttpGet("administracao/governanca")]
    public async Task<IActionResult> Governanca(CancellationToken ct)
    {
        if (!User.IsInRole("Administrador") || !TemModulo("governanca")) return Forbid();
        ViewBag.EmailTransporteConfigurado = !string.IsNullOrWhiteSpace(configuracao["CLARA_EMAIL_SMTP_HOST"]);
        ViewBag.EmailRemetenteAmbiente = configuracao["CLARA_EMAIL_FROM"] ?? "monitoramento.clara@shareitbrasil.com.br";
        return View(await governanca.ObterPainelGovernancaAsync(ObterTenant(), ct));
    }

    [HttpPost("administracao/governanca/documentos")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11_000_000)]
    public async Task<IActionResult> EnviarDocumento(string tipo, string titulo, string? versao, DateOnly? validadeAte, IFormFile arquivo, CancellationToken ct)
    {
        if (!User.IsInRole("Administrador") || !TemModulo("governanca")) return Forbid();
        if (arquivo is null || arquivo.Length == 0) { TempData["Erro"] = "Selecione um documento."; return RedirectToAction(nameof(Governanca)); }
        await using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, ct);
        await governanca.SalvarDocumentoGovernancaAsync(ObterTenant(), ObterUsuario(), tipo, titulo, versao,
            Path.GetFileName(arquivo.FileName), arquivo.ContentType, ms.ToArray(), validadeAte, ct);
        TempData["Mensagem"] = "Documento registrado com hash SHA-256 e trilha de auditoria.";
        return RedirectToAction(nameof(Governanca));
    }

    [HttpGet("administracao/governanca/documentos/{id:guid}/download")]
    public async Task<IActionResult> BaixarDocumento(Guid id, CancellationToken ct)
    {
        if (!User.IsInRole("Administrador") || !TemModulo("governanca")) return Forbid();
        var doc = await governanca.ObterDocumentoAsync(ObterTenant(), id, ct);
        return doc is null ? NotFound() : File(doc.Value.Conteudo, doc.Value.Mime, doc.Value.Nome);
    }

    [HttpPost("administracao/governanca/solicitar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SolicitarAprovacao(Guid? documentoId, string recurso, string? escopoDescricao, string emailJuridico, DateOnly validadeAte, CancellationToken ct)
    {
        if (!User.IsInRole("Administrador") || !TemModulo("governanca")) return Forbid();
        var resultado = await governanca.SolicitarAprovacaoJuridicaAsync(ObterTenant(), ObterUsuario(), documentoId, recurso, escopoDescricao, emailJuridico, validadeAte, ct);
        TempData["Mensagem"] = "Solicitação criada e colocada na fila de e-mail do .Clara. Se o transporte de e-mail ainda não estiver configurado, use o link abaixo.";
        TempData["LinkAprovacao"] = $"{Request.Scheme}://{Request.Host}/governanca/aprovacao/{resultado.Token}";
        return RedirectToAction(nameof(Governanca));
    }

    [HttpPost("administracao/governanca/ciencia")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarCiencia(Guid documentoId, string colaboradorNome, string? colaboradorEmail, string formaCiencia, string? observacao, CancellationToken ct)
    {
        if (!User.IsInRole("Administrador") || !TemModulo("governanca")) return Forbid();
        await governanca.RegistrarCienciaAsync(ObterTenant(), ObterUsuario(), documentoId, colaboradorNome, colaboradorEmail, formaCiencia, observacao, ct);
        TempData["Mensagem"] = "Ciência da política registrada.";
        return RedirectToAction(nameof(Governanca));
    }

    [HttpPost("administracao/governanca/email")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarEmail(string? replyTo, string? destinatariosAdicionais, CancellationToken ct)
    {
        if (!User.IsInRole("Administrador") || !TemModulo("governanca")) return Forbid();
        await governanca.SalvarEmailTenantAsync(ObterTenant(), replyTo, destinatariosAdicionais, ct);
        TempData["Mensagem"] = "Configuração de e-mail do tenant atualizada.";
        return RedirectToAction(nameof(Governanca));
    }

    [HttpPost("administracao/governanca/email/teste")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestarEmail(string destinatarioTeste, CancellationToken ct)
    {
        if (!User.IsInRole("Administrador") || !TemModulo("governanca")) return Forbid();
        try
        {
            await governanca.EnfileirarEmailTesteAsync(ObterTenant(), destinatarioTeste, ct);
            TempData["Mensagem"] = "E-mail de teste colocado na fila. O envio ocorrerá quando o transporte SMTP estiver configurado e saudável.";
        }
        catch (Exception ex) { TempData["Erro"] = ex.Message; }
        return RedirectToAction(nameof(Governanca));
    }

    [AllowAnonymous]
    [HttpGet("governanca/aprovacao/{token}")]
    public async Task<IActionResult> AprovacaoPublica(string token, CancellationToken ct)
    {
        var solicitacao = await governanca.ObterSolicitacaoPublicaAsync(token, ct);
        if (solicitacao is null) return NotFound();
        ViewBag.Token = token;
        return View(solicitacao);
    }

    [AllowAnonymous]
    [HttpPost("governanca/aprovacao/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DecidirAprovacao(string token, string decisao, string nome, string cargo, CancellationToken ct)
    {
        try
        {
            await governanca.RegistrarDecisaoJuridicaAsync(token, decisao, nome, cargo, ct);
            ViewBag.Resultado = decisao.Equals("Aprovar", StringComparison.OrdinalIgnoreCase) ? "Aprovação registrada." : "Rejeição registrada.";
        }
        catch (Exception ex) { ViewBag.Erro = ex.Message; }
        var solicitacao = await governanca.ObterSolicitacaoPublicaAsync(token, ct);
        if (solicitacao is null) return NotFound();
        ViewBag.Token = token;
        return View("AprovacaoPublica", solicitacao);
    }

    private static string? DetectarMimeImagem(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return "image/webp";
        return null;
    }

    private bool TemModulo(string codigo) => !User.IsInRole("Master") && User.HasClaim("modulo", codigo);
    private Guid ObterTenant() => Guid.Parse(User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException());
    private Guid ObterUsuario() => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());
}
