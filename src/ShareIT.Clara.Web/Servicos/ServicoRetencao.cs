using System.Text.Json;
using Npgsql;
using ShareIT.Clara.Dados.Banco;

namespace ShareIT.Clara.Web.Servicos;

/// <summary>
/// Objetivo: aplicar de forma automática a retenção configurada pelo tenant.
/// Origem: configuracoes_monitoramento + dados históricos da Clara.
/// Destino: exclusão controlada de telemetria/evidências/notificações/e-mails/auditoria vencidos.
/// Segurança: documentos jurídicos e autorizações não são apagados por esta rotina automática.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServicoRetencao(
    FabricaConexoesClara fabrica,
    EstadoInicializacaoBanco estadoBanco,
    ServicoArmazenamentoEvidencias armazenamento,
    ILogger<ServicoRetencao> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Evita competir com migrations/bootstrap e com o pico de inicialização do Web Service.
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (estadoBanco.ConcluidaComSucesso)
                    await ExecutarTodosTenantsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha no ciclo de retenção automática da .Clara.");
            }

            try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ExecutarTodosTenantsAsync(CancellationToken ct)
    {
        var tenants = new List<Guid>();
        await using (var conexao = fabrica.Criar())
        {
            await conexao.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT id FROM tenants WHERE ativo=true ORDER BY criado_em_utc", conexao);
            await using var leitor = await cmd.ExecuteReaderAsync(ct);
            while (await leitor.ReadAsync(ct)) tenants.Add(leitor.GetGuid(0));
        }

        foreach (var tenant in tenants)
        {
            if (ct.IsCancellationRequested) break;
            try { await ExecutarTenantAsync(tenant, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Falha ao aplicar retenção do tenant {TenantId}.", tenant); }
        }
    }

    private async Task ExecutarTenantAsync(Guid tenantId, CancellationToken ct)
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        int telemetria = 180, evidencias = 90, notificacoes = 180, emails = 90, auditoria = 365;
        await using (var cfg = new NpgsqlCommand("""
            SELECT retencao_telemetria_dias,retencao_evidencias_dias,retencao_notificacoes_dias,retencao_emails_dias,retencao_auditoria_dias
            FROM configuracoes_monitoramento WHERE tenant_id=@tenant
            """, conexao, transacao))
        {
            cfg.Parameters.AddWithValue("tenant", tenantId);
            await using var r = await cfg.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                telemetria = r.GetInt32(0); evidencias = r.GetInt32(1); notificacoes = r.GetInt32(2); emails = r.GetInt32(3); auditoria = r.GetInt32(4);
            }
        }

        var evidenciasVisuaisExternas = 0;
        try { evidenciasVisuaisExternas = await armazenamento.ExcluirExpiradasAsync(tenantId, evidencias, ct); }
        catch (InvalidOperationException) { /* tenant sem armazenamento externo configurado: segue com metadados legados */ }

        long total = evidenciasVisuaisExternas;
        var detalhe = new Dictionary<string, int>();
        if (evidenciasVisuaisExternas > 0) detalhe["evidencias_visuais_armazenamento"] = evidenciasVisuaisExternas;
        async Task Apagar(string nome, string sql, int dias)
        {
            await using var cmd = new NpgsqlCommand(sql, conexao, transacao);
            cmd.Parameters.AddWithValue("tenant", tenantId);
            cmd.Parameters.AddWithValue("dias", dias);
            var n = await cmd.ExecuteNonQueryAsync(ct);
            detalhe[nome] = n;
            total += n;
        }

        await Apagar("periodos_atividade", "DELETE FROM periodos_atividade WHERE tenant_id=@tenant AND fim_utc < now()-(@dias*interval '1 day')", telemetria);
        await Apagar("sites_utilizados", "DELETE FROM sites_utilizados WHERE tenant_id=@tenant AND fim_utc < now()-(@dias*interval '1 day')", telemetria);
        await Apagar("sessoes_usuario", "DELETE FROM sessoes_usuario WHERE tenant_id=@tenant AND COALESCE(fim_utc,inicio_utc) < now()-(@dias*interval '1 day')", telemetria);
        await Apagar("contexto_tecnico_visao", "DELETE FROM contexto_tecnico_visao WHERE tenant_id=@tenant AND observado_em_utc < now()-(@dias*interval '1 day')", telemetria);
        await Apagar("evidencias_visuais_legadas", "DELETE FROM evidencias_visuais WHERE tenant_id=@tenant AND demonstrativa=false AND capturada_em_utc < now()-(@dias*interval '1 day') AND chave_objeto IS NULL", evidencias);
        await Apagar("evidencias_contextuais", "DELETE FROM evidencias_contextuais WHERE tenant_id=@tenant AND observada_em_utc < now()-(@dias*interval '1 day')", evidencias);
        await Apagar("oportunidades_concluidas", "DELETE FROM oportunidades WHERE tenant_id=@tenant AND status='Concluida' AND atualizada_em_utc < now()-(@dias*interval '1 day')", evidencias);
        await Apagar("notificacoes", "DELETE FROM notificacoes WHERE tenant_id=@tenant AND criada_em_utc < now()-(@dias*interval '1 day')", notificacoes);
        await Apagar("fila_emails", "DELETE FROM fila_emails WHERE tenant_id=@tenant AND status IN ('Enviado','Erro') AND criado_em_utc < now()-(@dias*interval '1 day')", emails);
        await Apagar("atualizacoes_agente", "DELETE FROM atualizacoes_agente WHERE tenant_id=@tenant AND criado_em_utc < now()-(@dias*interval '1 day')", auditoria);
        await Apagar("auditoria", "DELETE FROM auditoria WHERE tenant_id=@tenant AND criado_em_utc < now()-(@dias*interval '1 day')", auditoria);

        await using (var log = new NpgsqlCommand("""
            INSERT INTO execucoes_retencao(tenant_id,inicio_utc,fim_utc,registros_excluidos,detalhe,status)
            VALUES(@tenant,now(),now(),@total,CAST(@detalhe AS jsonb),'Concluida')
            """, conexao, transacao))
        {
            log.Parameters.AddWithValue("tenant", tenantId);
            log.Parameters.AddWithValue("total", total);
            log.Parameters.AddWithValue("detalhe", JsonSerializer.Serialize(detalhe));
            await log.ExecuteNonQueryAsync(ct);
        }

        await transacao.CommitAsync(ct);
        if (total > 0) logger.LogInformation("Retenção do tenant {TenantId}: {Total} registros eliminados conforme política.", tenantId, total);
    }
}
