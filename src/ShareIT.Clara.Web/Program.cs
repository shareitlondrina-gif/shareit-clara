using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using ShareIT.Clara.Comum.Configuracao;
using ShareIT.Clara.Dados.Banco;
using ShareIT.Clara.Dados.Repositorios;
using System.Reflection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var configuracaoClara = CarregadorConfiguracaoClara.Carregar();

builder.Services.AddSingleton(configuracaoClara);
builder.Services.AddSingleton<FabricaConexoesClara>();
builder.Services.AddSingleton<PreparadorBancoClara>();
builder.Services.AddSingleton<MigradorBancoClara>();
builder.Services.AddScoped<RepositorioAutenticacao>();
builder.Services.AddScoped<RepositorioBootstrap>();
builder.Services.AddScoped<RepositorioAgentes>();
builder.Services.AddScoped<RepositorioAdministracao>();
builder.Services.AddScoped<RepositorioDashboard>();

builder.Services.AddControllersWithViews();
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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (configuracaoClara.Servidor.ExecutarMigracoesAutomaticamente)
{
    using var escopo = app.Services.CreateScope();
    await escopo.ServiceProvider.GetRequiredService<PreparadorBancoClara>().GarantirAsync();
    await escopo.ServiceProvider.GetRequiredService<MigradorBancoClara>().AplicarAsync();
    await escopo.ServiceProvider.GetRequiredService<RepositorioBootstrap>().GarantirAsync();
}

// Rotas técnicas são mapeadas diretamente para não depender de descoberta de controllers.
// Isso permite ao Render e ao PUBLICAR-CLARA.cmd distinguir aplicação online, versão publicada e saúde do banco.
app.MapGet("/api/versao", () => Results.Ok(new
{
    produto = "Share IT .Clara",
    versao = ObterVersaoAplicacao(),
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/health/live", () => Results.Ok(new
{
    produto = "Share IT .Clara",
    versao = ObterVersaoAplicacao(),
    aplicacao = "ok",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/health", async (FabricaConexoesClara fabrica, CancellationToken ct) =>
{
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
            aplicacao = "ok",
            banco = "ok",
            nomeBanco,
            migracoes,
            utc = DateTimeOffset.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            produto = "Share IT .Clara",
            versao = ObterVersaoAplicacao(),
            aplicacao = "ok",
            banco = "erro",
            erro = ex.Message,
            utc = DateTimeOffset.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllers();
app.MapControllerRoute(name: "padrao", pattern: "{controller=Painel}/{action=Index}/{id?}");
await app.RunAsync();

static string ObterVersaoAplicacao()
    => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.2";
