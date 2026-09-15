using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using ShareIT.Clara.Comum.Configuracao;
using ShareIT.Clara.Dados.Banco;
using ShareIT.Clara.Dados.Repositorios;
using ShareIT.Clara.Web.Servicos;
using ShareIT.Clara.Web.Seguranca;
using System.Reflection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var configuracaoClara = CarregadorConfiguracaoClara.Carregar();

builder.Services.AddSingleton(configuracaoClara);
builder.Services.AddSingleton<FabricaConexoesClara>();
builder.Services.AddSingleton<PreparadorBancoClara>();
builder.Services.AddSingleton<MigradorBancoClara>();
builder.Services.AddSingleton<EstadoInicializacaoBanco>();
builder.Services.AddScoped<RepositorioAutenticacao>();
builder.Services.AddScoped<RepositorioBootstrap>();
builder.Services.AddScoped<RepositorioAgentes>();
builder.Services.AddScoped<RepositorioAdministracao>();
builder.Services.AddScoped<RepositorioDashboard>();
builder.Services.AddScoped<RepositorioContexto>();
builder.Services.AddScoped<RepositorioMaster>();
builder.Services.AddScoped<RepositorioUsuariosAcesso>();
builder.Services.AddScoped<RepositorioInteligencia>();
builder.Services.AddScoped<RepositorioGovernanca>();
builder.Services.AddScoped<RepositorioGestaoExecutiva>();
builder.Services.AddHostedService<ServicoFilaEmail>();
builder.Services.AddHostedService<ServicoAlertas>();
builder.Services.AddHostedService<ServicoRetencao>();
builder.Services.AddHostedService<ServicoRelatoriosAgendados>();
builder.Services.AddScoped<ServicoEmpresaDemonstracao>();
builder.Services.AddScoped<ServicoRelogioDemonstracao>();

builder.Services.AddSingleton<IXmlRepository, RepositorioChavesProtecaoDados>();
builder.Services.AddDataProtection().SetApplicationName("ShareIT.Clara");
builder.Services.AddOptions<KeyManagementOptions>()
    .Configure<IXmlRepository>((opcoes, repositorio) => opcoes.XmlRepository = repositorio);

builder.Services.AddControllersWithViews();
builder.Services.Configure<ForwardedHeadersOptions>(opcoes =>
{
    opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opcoes.KnownIPNetworks.Clear();
    opcoes.KnownProxies.Clear();
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opcoes =>
    {
        opcoes.LoginPath = "/entrar";
        opcoes.AccessDeniedPath = "/acesso-negado";
        opcoes.Cookie.Name = ".ShareIT.Clara.Autenticacao";
        opcoes.Cookie.HttpOnly = true;
        opcoes.Cookie.SameSite = SameSiteMode.Lax;
        opcoes.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        opcoes.SlidingExpiration = true;
        opcoes.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opcoes.AddPolicy("api-agente", contexto =>
    {
        var chave = contexto.Request.Headers["X-Clara-Agente"].FirstOrDefault()
                    ?? contexto.Connection.RemoteIpAddress?.ToString()
                    ?? "desconhecido";
        return RateLimitPartition.GetFixedWindowLimiter(chave, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });
    opcoes.AddPolicy("login", contexto =>
    {
        var chave = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
        return RateLimitPartition.GetFixedWindowLimiter(chave, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/erro");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Rotas tecnicas ficam disponiveis antes da inicializacao do banco.
// Assim o Render consegue confirmar que o processo Web esta vivo mesmo enquanto migrations/bootstrap executam.
app.MapGet("/api/versao", () => Results.Ok(new
{
    produto = "Share IT .Clara",
    versao = ObterVersaoAplicacao(),
    buildId = ObterIdentificadorBuild(),
    commit = ObterCommitImplantado(),
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/health/live", (EstadoInicializacaoBanco estado, IConfiguration cfg) => Results.Ok(new
{
    produto = "Share IT .Clara",
    versao = ObterVersaoAplicacao(),
    buildId = ObterIdentificadorBuild(),
    commit = ObterCommitImplantado(),
    aplicacao = "ok",
    inicializacaoBanco = estado.Situacao,
    emailConfigurado = !string.IsNullOrWhiteSpace(cfg["CLARA_EMAIL_SMTP_HOST"]),
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/health", async (FabricaConexoesClara fabrica, EstadoInicializacaoBanco estado, CancellationToken ct) =>
{
    if (estado.Falhou)
    {
        return Results.Json(new
        {
            produto = "Share IT .Clara",
            versao = ObterVersaoAplicacao(),
            buildId = ObterIdentificadorBuild(),
            commit = ObterCommitImplantado(),
            aplicacao = "ok",
            banco = "erro",
            inicializacaoBanco = estado.Situacao,
            erro = "Falha na inicializacao do banco. Consulte os logs do Render.",
            utc = DateTimeOffset.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (!estado.ConcluidaComSucesso)
    {
        return Results.Json(new
        {
            produto = "Share IT .Clara",
            versao = ObterVersaoAplicacao(),
            buildId = ObterIdentificadorBuild(),
            commit = ObterCommitImplantado(),
            aplicacao = "ok",
            banco = "inicializando",
            inicializacaoBanco = estado.Situacao,
            utc = DateTimeOffset.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);

        await using var comandoBanco = new NpgsqlCommand("SELECT current_database()", conexao);
        var nomeBanco = Convert.ToString(await comandoBanco.ExecuteScalarAsync(ct)) ?? string.Empty;
        if (!nomeBanco.Equals("shareit_clara", StringComparison.Ordinal))
            throw new InvalidOperationException($"Banco inesperado no health check: {nomeBanco}");

        await using var comandoMigracoes = new NpgsqlCommand("SELECT COUNT(*) FROM migracoes_aplicadas", conexao);
        var migracoes = Convert.ToInt32(await comandoMigracoes.ExecuteScalarAsync(ct));

        return Results.Ok(new
        {
            produto = "Share IT .Clara",
            versao = ObterVersaoAplicacao(),
            buildId = ObterIdentificadorBuild(),
            commit = ObterCommitImplantado(),
            aplicacao = "ok",
            banco = "ok",
            inicializacaoBanco = estado.Situacao,
            nomeBanco,
            migracoes,
            utc = DateTimeOffset.UtcNow
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Health de readiness encontrou falha no banco do Share IT .Clara.");
        return Results.Json(new
        {
            produto = "Share IT .Clara",
            versao = ObterVersaoAplicacao(),
            buildId = ObterIdentificadorBuild(),
            commit = ObterCommitImplantado(),
            aplicacao = "ok",
            banco = "erro",
            inicializacaoBanco = estado.Situacao,
            erro = "Banco indisponivel. Consulte os logs do Render.",
            utc = DateTimeOffset.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllers();
app.MapControllerRoute(name: "padrao", pattern: "{controller=Painel}/{action=Index}/{id?}");

// Primeiro coloca a Web/API no ar; depois prepara banco/migrations/bootstrap.
// Isso evita que uma falha de banco deixe o processo totalmente invisivel ao health de liveness.
await app.StartAsync();

var estadoInicializacao = app.Services.GetRequiredService<EstadoInicializacaoBanco>();
if (configuracaoClara.Servidor.ExecutarMigracoesAutomaticamente)
{
    estadoInicializacao.Iniciar();
    try
    {
        using var escopo = app.Services.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<PreparadorBancoClara>().GarantirAsync();
        await escopo.ServiceProvider.GetRequiredService<MigradorBancoClara>().AplicarAsync();
        await escopo.ServiceProvider.GetRequiredService<RepositorioBootstrap>().GarantirAsync();
        estadoInicializacao.Concluir();
        app.Logger.LogInformation("Inicializacao do banco Share IT .Clara concluida com sucesso.");
    }
    catch (Exception ex)
    {
        estadoInicializacao.Falhar(ex);
        app.Logger.LogError(ex, "Falha na inicializacao do banco Share IT .Clara. A Web continuara online para diagnostico via /api/health/live.");
    }
}
else
{
    estadoInicializacao.MarcarDesabilitada();
    app.Logger.LogWarning("Migrations automaticas do Share IT .Clara estao desabilitadas por configuracao.");
}

await app.WaitForShutdownAsync();

static string ObterVersaoAplicacao()
    => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.5.8";

static string ObterIdentificadorBuild()
{
    try
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "BUILD.txt");
        return File.Exists(caminho) ? File.ReadAllText(caminho).Trim() : "nao-informado";
    }
    catch
    {
        return "indisponivel";
    }
}

static string ObterCommitImplantado()
    => Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT")
       ?? Environment.GetEnvironmentVariable("CLARA_GIT_COMMIT")
       ?? string.Empty;
