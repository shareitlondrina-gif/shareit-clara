using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using ShareIT.Clara.Dados.Banco;
using ShareIT.Clara.Dados.Repositorios;

namespace ShareIT.Clara.Web.Servicos;

/// <summary>
/// Objetivo: enviar resumos executivos recorrentes mesmo para destinatários que não possuam login na plataforma.
/// Origem: relatorios_agendados + histórico consolidado do tenant.
/// Destino: fila_emails, usando o transporte oficial da Clara.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServicoRelatoriosAgendados(
    FabricaConexoesClara fabrica,
    IServiceScopeFactory escopos,
    EstadoInicializacaoBanco estadoBanco,
    ILogger<ServicoRelatoriosAgendados> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (estadoBanco.ConcluidaComSucesso) await ProcessarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogWarning(ex, "Falha no ciclo de relatórios agendados da .Clara."); }

            try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessarAsync(CancellationToken ct)
    {
        var pendentes = new List<(Guid Id,Guid Tenant,string Nome,string Periodicidade,TimeOnly Hora,int? DiaSemana,int? DiaMes,string Fuso,string Destinatarios,string Parametros)>();
        await using (var conexao=fabrica.Criar())
        {
            await conexao.OpenAsync(ct);
            const string sql="""
                SELECT id,tenant_id,nome,periodicidade,hora_local,dia_semana,dia_mes,fuso_horario,destinatarios,parametros::text
                FROM relatorios_agendados
                WHERE ativo=true AND proxima_execucao_utc IS NOT NULL AND proxima_execucao_utc<=now()
                ORDER BY proxima_execucao_utc LIMIT 50
                """;
            await using var cmd=new NpgsqlCommand(sql,conexao);await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))pendentes.Add((r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetFieldValue<TimeOnly>(4),r.IsDBNull(5)?null:r.GetInt32(5),r.IsDBNull(6)?null:r.GetInt32(6),r.GetString(7),r.GetString(8),r.GetString(9)));
        }

        foreach(var item in pendentes)
        {
            try
            {
                int dias=7;
                try{using var doc=JsonDocument.Parse(item.Parametros);if(doc.RootElement.TryGetProperty("dias",out var d)&&d.TryGetInt32(out var v))dias=Math.Clamp(v,1,90);}catch{}
                using var escopo=escopos.CreateScope();var governanca=escopo.ServiceProvider.GetRequiredService<RepositorioGovernanca>();var gestao=escopo.ServiceProvider.GetRequiredService<RepositorioGestaoExecutiva>();
                var rel=await governanca.ObterRelatorioHistoricoAsync(item.Tenant,dias,ct);var pulso=await gestao.ObterPulsoAsync(item.Tenant,dias,ct);
                var assunto=$"Share IT .Clara — {item.Nome}";
                string T(long s){var t=TimeSpan.FromSeconds(Math.Max(0,s));return t.TotalHours>=1?$"{(int)t.TotalHours}h {t.Minutes:00}m":$"{t.Minutes}m";}
                var destaques=string.Join("\n",pulso.Prioridades.Take(3).Select(x=>"- "+x));
                if(string.IsNullOrWhiteSpace(destaques))destaques="- Nenhuma prioridade alta aberta no período.";
                var corpo=$"""Share IT .Clara — Resumo executivo / Ontem em 60 segundos\nPeríodo: últimos {dias} dia(s)\nPulso da Operação: {pulso.Indice}/100 · confiança {pulso.Confianca}\n\nProdutivo na jornada: {T(rel.SegundosProdutivosJornada)}\nOciosidade na jornada: {T(rel.SegundosOciososJornada)}\nNão produtivo na jornada: {T(rel.SegundosNaoProdutivosJornada)}\nProdutivo fora da jornada/margens: {T(rel.SegundosProdutivosForaJornada)}\n\nPrioridades\n{destaques}\n\nInsight: {rel.InsightPrincipal}\n\nA Clara apresenta fatos observados. A interpretação e as decisões de gestão pertencem à empresa contratante.""";
                await EnfileirarAsync(item.Tenant,item.Destinatarios,assunto,corpo,ct);
                var proxima=RepositorioGestaoExecutiva.CalcularProximaExecucao(item.Periodicidade,item.Hora,item.DiaSemana,item.DiaMes,item.Fuso,DateTimeOffset.UtcNow.AddMinutes(1));
                await AtualizarStatusAsync(item.Id,"Enfileirado",null,proxima,ct);
            }
            catch(Exception ex)
            {
                logger.LogWarning(ex,"Falha ao gerar relatório agendado {RelatorioId}.",item.Id);
                await AtualizarStatusAsync(item.Id,"Erro",ex.Message,DateTimeOffset.UtcNow.AddHours(1),ct);
            }
        }
    }

    private async Task EnfileirarAsync(Guid tenantId,string destinos,string assunto,string corpo,CancellationToken ct)
    {
        var emails=(destinos??string.Empty).Split(new[]{',',';','\n'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(x=>x.Contains('@')).Select(x=>x.ToLowerInvariant()).Distinct().ToArray();
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);
        foreach(var email in emails)
        {await using var cmd=new NpgsqlCommand("INSERT INTO fila_emails(tenant_id,destinatario,assunto,corpo_texto) VALUES(@tenant,@dest,@assunto,@corpo)",conexao);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("dest",email);cmd.Parameters.AddWithValue("assunto",assunto);cmd.Parameters.AddWithValue("corpo",corpo);await cmd.ExecuteNonQueryAsync(ct);}
    }

    private async Task AtualizarStatusAsync(Guid id,string status,string? erro,DateTimeOffset proxima,CancellationToken ct)
    {await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand("UPDATE relatorios_agendados SET ultima_execucao_utc=now(),proxima_execucao_utc=@proxima,ultimo_status=@status,ultimo_erro=@erro,atualizado_em_utc=now() WHERE id=@id",c);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("proxima",proxima);cmd.Parameters.AddWithValue("status",status);cmd.Parameters.Add("erro",NpgsqlDbType.Text).Value=(object?)erro??DBNull.Value;await cmd.ExecuteNonQueryAsync(ct);}
}
