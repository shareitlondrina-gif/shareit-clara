// Clara
// Programador: Paulo Andrade
// Objetivo: executar uma regra de aplicação da Clara.
// Origem: controladores, configuração e serviços hospedados.
// Destino: repositórios, integrações ou eventos da aplicação.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Clara.Dados.Banco;
using Clara.Dados.Repositorios;

namespace Clara.Web.Servicos;

/// <summary>
/// Objetivo: avaliar regras de alerta mesmo quando nenhum gestor estiver logado na plataforma.
/// Origem: regras_alerta e telemetria persistida por tenant.
/// Destino: Central de Notificações, Evidências/Oportunidades e fila de e-mails.
/// Segurança: não interpreta intenção; apenas compara fatos observados com parâmetros definidos pelo cliente.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServicoAlertas(
    FabricaConexoesClara fabrica,
    IServiceScopeFactory escopos,
    EstadoInicializacaoBanco estadoBanco,
    ILogger<ServicoAlertas> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (estadoBanco.ConcluidaComSucesso)
                    await AvaliarTenantsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha temporária no ciclo de avaliação de alertas da Clara.");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task AvaliarTenantsAsync(CancellationToken ct)
    {
        var tenants = new List<Guid>();
        await using (var conexao = fabrica.Criar())
        {
            await conexao.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT id FROM tenants WHERE ativo=true ORDER BY criado_em_utc", conexao);
            await using var leitor = await cmd.ExecuteReaderAsync(ct);
            while (await leitor.ReadAsync(ct)) tenants.Add(leitor.GetGuid(0));
        }

        if (tenants.Count == 0) return;
        using var escopo = escopos.CreateScope();
        var repositorio = escopo.ServiceProvider.GetRequiredService<RepositorioGovernanca>();
        foreach (var tenantId in tenants)
        {
            if (ct.IsCancellationRequested) break;
            try { await repositorio.SincronizarNotificacoesAsync(tenantId, null, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Falha ao avaliar alertas do tenant {TenantId}.", tenantId); }
        }
    }
}
