using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Reflection;
using System.Text;
using ShareIT.Clara.Comum.Configuracao;
using ShareIT.Clara.Dados.Repositorios;
using ShareIT.Clara.Dominio.Contratos;
using ShareIT.Clara.Web.Servicos;
using ShareIT.Clara.Web.Modelos;

namespace ShareIT.Clara.Web.Controllers;

/// <summary>
/// Objetivo: entregar as experiências operacionais e administrativas do Share IT .Clara.
/// Origem: usuário autenticado pertencente ao tenant.
/// Destino: Agora, estações, inteligência, contexto e configurações.
/// Dependências: repositórios do tenant e relógio isolado da empresa demonstração.
/// Impacto: concentra navegação e drill-down sem expor dados entre empresas.
/// Desenvolvedor: Paulo Andrade
/// </summary>
[Authorize]
public sealed class PainelController(
    RepositorioDashboard dashboard,
    RepositorioAdministracao administracao,
    RepositorioContexto contexto,
    RepositorioInteligencia inteligencia,
    ServicoRelogioDemonstracao relogioDemonstracao,
    ConfiguracaoClara configuracao,
    IWebHostEnvironment ambiente) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!TemModulo("agora")) return Forbid();
        var tenant = ObterTenant();
        await PrepararDemonstracaoAsync(tenant, ct);

        ViewBag.Empresas = await administracao.ListarEmpresasAsync(tenant, ct);
        var locais = await inteligencia.ObterPainelLocaisAsync(tenant, ct);
        ViewBag.Locais = locais.Resumos;
        ViewBag.InsightLocais = locais.InsightPrincipal;
        ViewBag.PainelOportunidades = await inteligencia.ObterOportunidadesAsync(tenant, 7, null, null, null, ct);
        ViewBag.OportunidadesRecentes = await inteligencia.ListarOportunidadesAsync(tenant, 30, null, null, null, 5, ct);
        ViewBag.EvidenciasRecentes = await inteligencia.ListarEvidenciasAsync(tenant, 30, null, null, null, 5, ct);
        return View(await dashboard.ObterAgoraAsync(tenant, ct));
    }

    [HttpGet("timeline/{agenteId:guid}")]
    public async Task<IActionResult> Timeline(Guid agenteId, CancellationToken ct)
    {
        if (!TemModulo("estacoes")) return Forbid();
        var tenant = ObterTenant();
        await PrepararDemonstracaoAsync(tenant, ct);
        var modelo = await dashboard.ObterTimelineAsync(tenant, agenteId, DateTimeOffset.UtcNow.AddHours(-24), ct);
        return modelo is null ? NotFound() : View(modelo);
    }

    [HttpPost("administracao/gerar-token")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GerarToken(Guid empresaId, CancellationToken ct)
    {
        // Compatibilidade com o formulário das versões anteriores: mantém a rota,
        // mas centraliza a experiência no novo portal de instalação.
        if (!TemModulo("estacoes") || !User.IsInRole("Administrador")) return Forbid();
        try
        {
            var token = await administracao.CriarTokenInstalacaoAsync(
                ObterTenant(), ObterGuid(System.Security.Claims.ClaimTypes.NameIdentifier), empresaId, ct);
            TempData["TokenInstalacao"] = token.Token;
            TempData["TokenExpira"] = token.ExpiraEmUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
            TempData["TokenServidor"] = token.UrlServidor;
            TempData["Mensagem"] = "Token temporário gerado. Ele pode ser usado uma única vez e expira em 30 minutos.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Erro"] = ex.Message;
        }
        return TemModulo("instalacao-agente")
            ? RedirectToAction(nameof(InstalacaoAgente))
            : RedirectToAction(nameof(Index));
    }

    [HttpGet("administracao/instalacao")]
    public async Task<IActionResult> InstalacaoAgente(CancellationToken ct)
    {
        if (!TemModulo("instalacao-agente") || !User.IsInRole("Administrador")) return Forbid();
        var tenant = ObterTenant();
        var empresas = await administracao.ListarEmpresasAsync(tenant, ct);
        var (assinatura, estacoes) = await administracao.ObterAssinaturaAsync(tenant, ct);
        return View(new PainelInstalacaoAgente(empresas, assinatura, estacoes, configuracao.Servidor.UrlPublica.TrimEnd('/'), ObterVersaoAplicacao()));
    }

    [HttpPost("administracao/instalacao/gerar-token")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GerarTokenInstalacao(Guid empresaId, CancellationToken ct)
    {
        if (!TemModulo("instalacao-agente") || !User.IsInRole("Administrador")) return Forbid();
        try
        {
            var token = await administracao.CriarTokenInstalacaoAsync(ObterTenant(), ObterGuid(System.Security.Claims.ClaimTypes.NameIdentifier), empresaId, ct);
            TempData["TokenInstalacao"] = token.Token;
            TempData["TokenExpira"] = token.ExpiraEmUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
            TempData["TokenServidor"] = token.UrlServidor;
            TempData["Mensagem"] = "Token temporário gerado. Ele pode ser usado uma única vez e expira em 30 minutos.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Erro"] = ex.Message;
        }
        return RedirectToAction(nameof(InstalacaoAgente));
    }

    [HttpGet("administracao/instalacao/download")]
    public IActionResult BaixarInstalador()
    {
        if (!TemModulo("instalacao-agente") || !User.IsInRole("Administrador")) return Forbid();
        var versao = ObterVersaoAplicacao();
        var nome = $"Clara-Instalador-v{versao}.zip";
        var arquivo = Path.Combine(ambiente.ContentRootPath, "instaladores", "agente", nome);
        if (!System.IO.File.Exists(arquivo))
            return NotFound("O pacote de instalação ainda não foi publicado para esta versão.");
        return PhysicalFile(arquivo, "application/zip", nome, enableRangeProcessing: true);
    }

    [HttpGet("administracao/instalacao/qrcode")]
    public IActionResult QrCodeInstalador()
    {
        if (!TemModulo("instalacao-agente") || !User.IsInRole("Administrador")) return Forbid();
        var url = $"{Request.Scheme}://{Request.Host}/administracao/instalacao/download";
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var qr = new SvgQRCode(dados);
        return Content(qr.GetGraphic(4), "image/svg+xml");
    }

    [HttpGet("estacoes")]
    public async Task<IActionResult> Estacoes(string? filtro = null, string? busca = null, CancellationToken ct = default)
    {
        if (!TemModulo("estacoes")) return Forbid();
        var tenant = ObterTenant();
        await PrepararDemonstracaoAsync(tenant, ct);
        ViewBag.Filtro = filtro;
        ViewBag.Busca = busca;
        return View(await administracao.ListarEstacoesAsync(tenant, ct));
    }

    [HttpGet("estacoes/{ativoId:guid}")]
    public async Task<IActionResult> Estacao(Guid ativoId, CancellationToken ct)
    {
        if (!TemModulo("estacoes")) return Forbid();
        var tenant = ObterTenant();
        await PrepararDemonstracaoAsync(tenant, ct);
        var estacao = await administracao.ObterEstacaoAsync(tenant, ativoId, ct);
        if (estacao is null) return NotFound();
        var gerencial = await inteligencia.ObterContextoEstacaoAsync(tenant, ativoId, ct);
        if (gerencial is null) return NotFound();

        ViewBag.Empresas = await administracao.ListarEmpresasAsync(tenant, ct);
        ViewBag.Unidades = await administracao.ListarUnidadesAsync(tenant, ct);
        ViewBag.Departamentos = await administracao.ListarDepartamentosAsync(tenant, ct);
        return View(new PainelDetalheEstacao(
            estacao, gerencial.AgenteId, gerencial.Unidade, gerencial.Departamento, gerencial.StatusAtual,
            gerencial.EstadoAtual, gerencial.ProcessoAtual, gerencial.LocalTrabalho,
            gerencial.SegundosProdutivosHoje, gerencial.SegundosOciososHoje, gerencial.AplicativoPrincipal,
            gerencial.IndiceFoco, gerencial.TimelineHoje, gerencial.Oportunidades, gerencial.Evidencias));
    }

    [HttpPost("estacoes/{ativoId:guid}/salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarEstacao(Guid ativoId, Guid empresaId, Guid? unidadeId, Guid? departamentoId, string? patrimonio, string? responsavel, CancellationToken ct)
    {
        if (!TemModulo("estacoes")) return Forbid();
        await administracao.AtualizarCadastroEstacaoAsync(ObterTenant(), ativoId, empresaId, unidadeId, departamentoId, patrimonio, responsavel, ct);
        TempData["Mensagem"] = "Cadastro da estação atualizado.";
        return RedirectToAction(nameof(Estacao), new { ativoId });
    }

    [HttpGet("aplicativos")]
    public async Task<IActionResult> Aplicativos(int dias = 7, CancellationToken ct = default)
    {
        if (!TemModulo("aplicativos")) return Forbid();
        var tenant = ObterTenant();
        await PrepararDemonstracaoAsync(tenant, ct);
        ViewBag.Categorias = await contexto.ListarCategoriasAsync(tenant, ct);
        ViewBag.Dias = Math.Clamp(dias, 1, 90);
        return View(await dashboard.ObterAplicativosAsync(tenant, dias, ct));
    }

    [HttpPost("aplicativos/classificar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClassificarAplicativo(string processo, string classificacao, Guid? categoriaId, int dias = 7, CancellationToken ct = default)
    {
        if (!TemModulo("aplicativos")) return Forbid();
        await contexto.ClassificarAplicativoAsync(ObterTenant(), processo, classificacao, categoriaId, ct);
        return RedirectToAction(nameof(Aplicativos), new { dias });
    }

    [HttpGet("navegacao-web")]
    public async Task<IActionResult> NavegacaoWeb(int dias = 7, CancellationToken ct = default)
    {
        if (!TemModulo("aplicativos")) return Forbid();
        var tenant = ObterTenant();
        await PrepararDemonstracaoAsync(tenant, ct);
        ViewBag.Dias = Math.Clamp(dias, 1, 90);
        return View(await dashboard.ObterNavegacaoWebAsync(tenant, dias, ct));
    }

    [HttpPost("navegacao-web/classificar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClassificarSite(string dominio, string classificacao, string? categoria, int dias = 7, CancellationToken ct = default)
    {
        if (!TemModulo("aplicativos")) return Forbid();
        await contexto.ClassificarSiteAsync(ObterTenant(), dominio, classificacao, categoria, ct);
        TempData["Mensagem"] = $"Regra de {dominio} atualizada.";
        return RedirectToAction(nameof(NavegacaoWeb), new { dias = Math.Clamp(dias, 1, 90) });
    }

    [HttpPost("navegacao-web/regras/{id:guid}/remover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoverRegraSite(Guid id, int dias = 7, CancellationToken ct = default)
    {
        if (!TemModulo("aplicativos")) return Forbid();
        await contexto.RemoverRegraSiteAsync(ObterTenant(), id, ct);
        TempData["Mensagem"] = "Regra de site removida.";
        return RedirectToAction(nameof(NavegacaoWeb), new { dias = Math.Clamp(dias, 1, 90) });
    }

    [HttpGet("indice-geral")]
    public async Task<IActionResult> IndiceGeral(CancellationToken ct)
    {
        if (!TemModulo("indice-geral")) return Forbid();
        var tenant = ObterTenant();
        await PrepararDemonstracaoAsync(tenant, ct);
        return View(await dashboard.ObterIndiceGeralAsync(tenant, ct));
    }

    [HttpGet("integracoes")]
    public async Task<IActionResult> Integracoes(CancellationToken ct)
    {
        if (!TemModulo("integracoes")) return Forbid();
        return View(await dashboard.ObterIntegracoesAsync(ObterTenant(), ct));
    }

    [HttpGet("locais")]
    public async Task<IActionResult> Locais(CancellationToken ct)
    {
        if (!TemModulo("locais")) return Forbid();
        var tenant = ObterTenant();
        await PrepararDemonstracaoAsync(tenant, ct);
        return View(await inteligencia.ObterPainelLocaisAsync(tenant, ct));
    }

    [HttpPost("locais/adicionar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdicionarLocal(string nome, string faixaIp, string tipo, CancellationToken ct)
    {
        if (!TemModulo("locais")) return Forbid();
        await contexto.AdicionarLocalAsync(ObterTenant(), nome, faixaIp, tipo, ct);
        TempData["Mensagem"] = "Regra de local adicionada.";
        return RedirectToAction(nameof(Locais));
    }

    [HttpGet("oportunidades")]
    public async Task<IActionResult> Oportunidades(int dias = 7, string? categoria = null, string? pessoa = null, string? impacto = null, CancellationToken ct = default)
    {
        if (!TemModulo("oportunidades")) return Forbid();
        var tenant = ObterTenant();
        await PrepararDemonstracaoAsync(tenant, ct);
        ViewBag.Dias = Math.Clamp(dias, 1, 365);
        ViewBag.Categoria = categoria;
        ViewBag.Pessoa = pessoa;
        ViewBag.Impacto = impacto;
        return View(await inteligencia.ObterOportunidadesAsync(tenant, dias, categoria, pessoa, impacto, ct));
    }

    [HttpGet("oportunidades/exportar")]
    public async Task<IActionResult> ExportarOportunidades(int dias = 7, string? categoria = null, string? pessoa = null, string? impacto = null, CancellationToken ct = default)
    {
        if (!TemModulo("oportunidades")) return Forbid();
        var itens = await inteligencia.ListarOportunidadesAsync(ObterTenant(), dias, categoria, pessoa, impacto, 2000, ct);
        var csv = new StringBuilder("Pessoa;Equipe;Estação;Categoria;Evidência;Impacto;Recomendação;Status;Detectada em\r\n");
        foreach (var item in itens)
        {
            csv.AppendJoin(';', new[]
            {
                CsvCampo(item.Pessoa), CsvCampo(item.Equipe), CsvCampo(item.Hostname), CsvCampo(item.Categoria),
                CsvCampo(item.EvidenciaResumida), CsvCampo(item.Impacto), CsvCampo(item.Recomendacao), CsvCampo(item.Status),
                CsvCampo(item.DetectadaEmUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"))
            }).Append("\r\n");
        }
        return File(Utf8Bom(csv.ToString()), "text/csv; charset=utf-8", $"Clara-Oportunidades-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    [HttpPost("oportunidades/{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtualizarOportunidade(Guid id, string status, int dias = 7, CancellationToken ct = default)
    {
        if (!TemModulo("oportunidades")) return Forbid();
        await inteligencia.AtualizarStatusOportunidadeAsync(ObterTenant(), id, status, ct);
        TempData["Mensagem"] = "Status da oportunidade atualizado.";
        return RedirectToAction(nameof(Oportunidades), new { dias });
    }

    [HttpGet("evidencias")]
    public async Task<IActionResult> Evidencias(int dias = 1, string? pessoa = null, string? categoria = null, string? gravidade = null, CancellationToken ct = default)
    {
        if (!TemModulo("evidencias")) return Forbid();
        var tenant = ObterTenant();
        await PrepararDemonstracaoAsync(tenant, ct);
        ViewBag.Dias = Math.Clamp(dias, 1, 365);
        ViewBag.Pessoa = pessoa;
        ViewBag.Categoria = categoria;
        ViewBag.Gravidade = gravidade;
        return View(await inteligencia.ObterEvidenciasAsync(tenant, dias, pessoa, categoria, gravidade, ct));
    }

    [HttpGet("evidencias/exportar")]
    public async Task<IActionResult> ExportarEvidencias(int dias = 1, string? pessoa = null, string? categoria = null, string? gravidade = null, CancellationToken ct = default)
    {
        if (!TemModulo("evidencias")) return Forbid();
        var itens = await inteligencia.ListarEvidenciasAsync(ObterTenant(), dias, pessoa, categoria, gravidade, 2000, ct);
        var csv = new StringBuilder("Data/Hora;Pessoa;Equipe;Estação;Categoria;Gravidade;Evidência;Detalhe;Fonte\r\n");
        foreach (var item in itens)
        {
            csv.AppendJoin(';', new[]
            {
                CsvCampo(item.ObservadaEmUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")), CsvCampo(item.Pessoa),
                CsvCampo(item.Equipe), CsvCampo(item.Hostname), CsvCampo(item.Categoria), CsvCampo(item.Gravidade),
                CsvCampo(item.Resumo), CsvCampo(item.Detalhe), CsvCampo(item.Fonte)
            }).Append("\r\n");
        }
        return File(Utf8Bom(csv.ToString()), "text/csv; charset=utf-8", $"Clara-Evidencias-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    [HttpGet("administracao/configuracoes")]
    public async Task<IActionResult> Configuracoes(CancellationToken ct)
    {
        if (!TemModulo("configuracoes") || !User.IsInRole("Administrador")) return Forbid();
        return View(await inteligencia.ObterConfiguracoesAsync(ObterTenant(), ct));
    }

    [HttpPost("administracao/configuracoes/salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarConfiguracoes(
        bool permitirClassificacaoWeb,
        bool monitorarUrlsDominios,
        bool usarCategoriasWeb,
        int tempoMinimoOciosidadeMinutos,
        int baixaContinuidadeMinutos,
        int limiarAlternanciaAplicativos,
        string? redesConfiaveisCidr,
        bool considerarVpnEscritorio,
        bool classificarIpPublico,
        int intervaloCapturaMinutos,
        int retencaoEvidenciasDias,
        bool anonimizacaoParcial,
        string horarioInicio,
        string horarioFim,
        string diasMonitorados,
        int intervaloHeartbeatSegundos,
        int tempoOfflineMinutos,
        bool alertaEstacaoOffline,
        bool notificarPorEmail,
        string? destinatariosAlertas,
        CancellationToken ct)
    {
        if (!TemModulo("configuracoes") || !User.IsInRole("Administrador")) return Forbid();
        var atual = await inteligencia.ObterConfiguracoesAsync(ObterTenant(), ct);
        var configuracoes = new ConfiguracoesMonitoramento(
            permitirClassificacaoWeb, monitorarUrlsDominios, usarCategoriasWeb,
            tempoMinimoOciosidadeMinutos, baixaContinuidadeMinutos, limiarAlternanciaAplicativos,
            redesConfiaveisCidr, considerarVpnEscritorio, classificarIpPublico,
            false, // captura de tela segue estruturalmente preparada, mas desabilitada nesta baseline.
            intervaloCapturaMinutos, retencaoEvidenciasDias, anonimizacaoParcial,
            horarioInicio, horarioFim, diasMonitorados, intervaloHeartbeatSegundos, tempoOfflineMinutos,
            alertaEstacaoOffline, notificarPorEmail, destinatariosAlertas, atual.AtualizadoEmUtc);
        await inteligencia.SalvarConfiguracoesAsync(ObterTenant(), configuracoes, ct);
        TempData["Mensagem"] = "Configurações salvas. Os agentes receberão a política na próxima sincronização.";
        return RedirectToAction(nameof(Configuracoes));
    }

    [HttpPost("administracao/configuracoes/restaurar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestaurarConfiguracoes(CancellationToken ct)
    {
        if (!TemModulo("configuracoes") || !User.IsInRole("Administrador")) return Forbid();
        await inteligencia.RestaurarConfiguracoesPadraoAsync(ObterTenant(), ct);
        TempData["Mensagem"] = "Configurações padrão restauradas.";
        return RedirectToAction(nameof(Configuracoes));
    }

    [HttpGet("indicadores/detalhes")]
    public async Task<IActionResult> DetalhesIndicador(string tipo, string? valor = null, int dias = 30, CancellationToken ct = default)
    {
        if (User.IsInRole("Master")) return Forbid();
        var tenant = ObterTenant();
        await PrepararDemonstracaoAsync(tenant, ct);
        return PartialView("_DetalhesIndicador", await inteligencia.ObterDrilldownAsync(tenant, tipo, valor, dias, ct));
    }

    private static byte[] Utf8Bom(string texto)
    {
        var conteudo = Encoding.UTF8.GetBytes(texto);
        var preambulo = Encoding.UTF8.GetPreamble();
        var resultado = new byte[preambulo.Length + conteudo.Length];
        Buffer.BlockCopy(preambulo, 0, resultado, 0, preambulo.Length);
        Buffer.BlockCopy(conteudo, 0, resultado, preambulo.Length, conteudo.Length);
        return resultado;
    }

    private static string CsvCampo(string? valor)
    {
        var texto = valor ?? string.Empty;
        // Impede que conteúdo cadastrado seja interpretado como fórmula ao abrir no Excel.
        if (texto.Length > 0 && texto[0] is '=' or '+' or '-' or '@') texto = "'" + texto;
        return "\"" + texto.Replace("\"", "\"\"") + "\"";
    }

    private static string ObterVersaoAplicacao()
        => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.5.2";

    private async Task PrepararDemonstracaoAsync(Guid tenant, CancellationToken ct)
        => await relogioDemonstracao.AtualizarSeNecessarioAsync(tenant, ct);

    private bool TemModulo(string codigo)
        => !User.IsInRole("Master") && User.HasClaim("modulo", codigo);

    private Guid ObterTenant() => ObterGuid("tenant_id");

    private Guid ObterGuid(string tipoClaim)
    {
        var valor = User.FindFirst(tipoClaim)?.Value ?? throw new UnauthorizedAccessException("Claim ausente.");
        return Guid.Parse(valor);
    }
}
