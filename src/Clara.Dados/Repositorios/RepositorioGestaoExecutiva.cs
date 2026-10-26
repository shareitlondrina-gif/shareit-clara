// Clara
// Programador: Paulo Andrade
// Objetivo: persistir e recuperar dados da Clara.
// Origem: camada de aplicação/domínio.
// Destino: PostgreSQL e contratos de domínio.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using Clara.Dados.Banco;
using Clara.Dominio.Contratos;

namespace Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: consolidar funções executivas da Clara e a integração opcional com outros produtos Clara.
/// Origem: telemetria agregada do tenant, oportunidades/evidências e contexto técnico recebido do VISÃO quando contratado.
/// Destino: Pulso da Operação, Contexto TI, relatórios agendados e filtros salvos.
/// Regra: a Clara funciona integralmente sem VISÃO; nenhum menu/dado de TI é exigido para o produto principal.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioGestaoExecutiva(FabricaConexoesClara fabrica, RepositorioGovernanca governanca)
{
    public async Task<IntegracaoProdutoResumo> ObterIntegracaoVisaoAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string garantir = "INSERT INTO integracoes_produtos_clara(tenant_id,produto) VALUES(@tenant,'Visao') ON CONFLICT DO NOTHING";
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using (var g = new NpgsqlCommand(garantir, conexao)) { g.Parameters.AddWithValue("tenant", tenantId); await g.ExecuteNonQueryAsync(ct); }
        await using var cmd = new NpgsqlCommand("SELECT produto,contratado,integracao_ativa,url_base,ultimo_sucesso_utc,ultima_falha FROM integracoes_produtos_clara WHERE tenant_id=@tenant AND produto='Visao'", conexao);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return new("Visao", false, false, null, null, null);
        return new(r.GetString(0), r.GetBoolean(1), r.GetBoolean(2), r.IsDBNull(3)?null:r.GetString(3), r.IsDBNull(4)?null:ComoDataHora(r,4), r.IsDBNull(5)?null:r.GetString(5));
    }

    public async Task<bool> VisaoDisponivelAsync(Guid tenantId, CancellationToken ct = default)
    {
        var i = await ObterIntegracaoVisaoAsync(tenantId, ct);
        return i.Contratado && i.IntegracaoAtiva;
    }

    public async Task<string?> ConfigurarVisaoAsync(Guid tenantId, bool contratado, bool integracaoAtiva, string? urlBase, bool gerarNovoToken, CancellationToken ct = default)
    {
        string? token = gerarNovoToken ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant() : null;
        var hash = token is null ? null : Sha256(token);
        const string sql = """
            INSERT INTO integracoes_produtos_clara(tenant_id,produto,contratado,integracao_ativa,url_base,token_hash,atualizado_em_utc)
            VALUES(@tenant,'Visao',@contratado,@ativa,@url,@hash,now())
            ON CONFLICT(tenant_id,produto) DO UPDATE SET
                contratado=EXCLUDED.contratado,integracao_ativa=EXCLUDED.integracao_ativa,url_base=EXCLUDED.url_base,
                token_hash=COALESCE(EXCLUDED.token_hash,integracoes_produtos_clara.token_hash),atualizado_em_utc=now();
            """;
        await using var conexao = fabrica.Criar(); await conexao.OpenAsync(ct); await using var cmd = new NpgsqlCommand(sql, conexao);
        cmd.Parameters.AddWithValue("tenant", tenantId); cmd.Parameters.AddWithValue("contratado", contratado); cmd.Parameters.AddWithValue("ativa", contratado && integracaoAtiva);
        cmd.Parameters.Add("url",NpgsqlDbType.Varchar).Value=(object?)Limpar(urlBase)??DBNull.Value; cmd.Parameters.Add("hash",NpgsqlDbType.Char).Value=(object?)hash??DBNull.Value;
        await cmd.ExecuteNonQueryAsync(ct);
        return token;
    }

    public async Task<bool> ValidarTokenVisaoAsync(Guid tenantId, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);
        await using var cmd=new NpgsqlCommand("SELECT token_hash FROM integracoes_produtos_clara WHERE tenant_id=@tenant AND produto='Visao' AND contratado=true AND integracao_ativa=true",conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);var esperado=Convert.ToString(await cmd.ExecuteScalarAsync(ct));
        if(string.IsNullOrWhiteSpace(esperado))return false;
        var a=Encoding.UTF8.GetBytes(esperado);var b=Encoding.UTF8.GetBytes(Sha256(token.Trim()));
        return a.Length==b.Length && CryptographicOperations.FixedTimeEquals(a,b);
    }

    public async Task RegistrarContextoVisaoAsync(Guid tenantId, Guid? ativoId, string? hostname, string categoria, string severidade, string resumo, string? detalhe, string? referenciaExterna, DateTimeOffset observadoEmUtc, CancellationToken ct=default)
    {
        severidade = severidade is "Informativa" or "Atencao" or "Alta" or "Critica" ? severidade : "Informativa";
        const string sql="""
            INSERT INTO contexto_tecnico_visao(tenant_id,ativo_id,hostname,categoria,severidade,resumo,detalhe,referencia_externa,observado_em_utc)
            SELECT @tenant,a.id,COALESCE(@hostname,a.hostname),@categoria,@severidade,@resumo,@detalhe,@referencia,@observado
            FROM (SELECT 1) x
            LEFT JOIN LATERAL (
                SELECT id,hostname FROM ativos WHERE tenant_id=@tenant AND ativo=true
                  AND ((@ativo IS NOT NULL AND id=@ativo) OR (@ativo IS NULL AND @hostname IS NOT NULL AND lower(hostname)=lower(@hostname)))
                LIMIT 1
            ) a ON true;
            UPDATE integracoes_produtos_clara SET ultimo_sucesso_utc=now(),ultima_falha=NULL WHERE tenant_id=@tenant AND produto='Visao';
            """;
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.Add("ativo",NpgsqlDbType.Uuid).Value=(object?)ativoId??DBNull.Value;
        cmd.Parameters.Add("hostname",NpgsqlDbType.Varchar).Value=(object?)Limpar(hostname)??DBNull.Value;cmd.Parameters.AddWithValue("categoria",string.IsNullOrWhiteSpace(categoria)?"Tecnologia":categoria.Trim());
        cmd.Parameters.AddWithValue("severidade",severidade);cmd.Parameters.AddWithValue("resumo",resumo.Trim());cmd.Parameters.Add("detalhe",NpgsqlDbType.Text).Value=(object?)Limpar(detalhe)??DBNull.Value;
        cmd.Parameters.Add("referencia",NpgsqlDbType.Varchar).Value=(object?)Limpar(referenciaExterna)??DBNull.Value;cmd.Parameters.AddWithValue("observado",observadoEmUtc);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyCollection<ContextoTecnicoVisaoResumo>> ListarContextoVisaoAsync(Guid tenantId,int dias=7,int limite=100,CancellationToken ct=default)
    {
        const string sql="""
            SELECT id,ativo_id,hostname,categoria,severidade,resumo,detalhe,referencia_externa,observado_em_utc
            FROM contexto_tecnico_visao WHERE tenant_id=@tenant AND observado_em_utc>=now()-(@dias*interval '1 day')
            ORDER BY observado_em_utc DESC LIMIT @limite;
            """;
        var lista=new List<ContextoTecnicoVisaoResumo>();await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("dias",Math.Clamp(dias,1,365));cmd.Parameters.AddWithValue("limite",Math.Clamp(limite,1,500));
        await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))lista.Add(new(r.GetInt64(0),r.IsDBNull(1)?null:r.GetGuid(1),r.IsDBNull(2)?null:r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.IsDBNull(6)?null:r.GetString(6),r.IsDBNull(7)?null:r.GetString(7),ComoDataHora(r,8)));
        return lista;
    }

    public async Task<PainelPulsoOperacao> ObterPulsoAsync(Guid tenantId,int dias=7,CancellationToken ct=default)
    {
        var relatorio=await governanca.ObterRelatorioHistoricoAsync(tenantId,Math.Clamp(dias,1,90),ct);
        long prod=relatorio.SegundosProdutivosJornada,ocioso=relatorio.SegundosOciososJornada,nao=relatorio.SegundosNaoProdutivosJornada,fora=relatorio.SegundosProdutivosForaJornada;
        var baseClassificada=Math.Max(1,prod+ocioso+nao);var produtividade=(int)Math.Round(prod*100d/baseClassificada);var ociosidade=(int)Math.Round(ocioso*100d/baseClassificada);var naoProd=(int)Math.Round(nao*100d/baseClassificada);
        int total=0,online=0,oppAlta=0,evidAlta=0;
        await using(var conexao=fabrica.Criar())
        {await conexao.OpenAsync(ct);
            await using(var cmd=new NpgsqlCommand("""
                SELECT count(*)::int, count(*) FILTER(WHERE g.ultima_comunicacao_utc>=now()-(COALESCE(cm.tempo_offline_minutos,15)*interval '1 minute'))::int
                FROM ativos a LEFT JOIN agentes g ON g.ativo_id=a.id AND g.tenant_id=a.tenant_id AND g.ativo=true
                LEFT JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id WHERE a.tenant_id=@tenant AND a.ativo=true
                """,conexao)){cmd.Parameters.AddWithValue("tenant",tenantId);await using var r=await cmd.ExecuteReaderAsync(ct);if(await r.ReadAsync(ct)){total=r.GetInt32(0);online=r.GetInt32(1);}}
            await using(var cmd=new NpgsqlCommand("SELECT count(*)::int FROM oportunidades WHERE tenant_id=@tenant AND status<>'Concluida' AND impacto='Alto'",conexao)){cmd.Parameters.AddWithValue("tenant",tenantId);oppAlta=Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));}
            await using(var cmd=new NpgsqlCommand("SELECT count(*)::int FROM evidencias_contextuais WHERE tenant_id=@tenant AND gravidade='Alta' AND observada_em_utc>=now()-(@dias*interval '1 day')",conexao)){cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("dias",Math.Clamp(dias,1,90));evidAlta=Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));}
        }
        var disponibilidade=total==0?0:(int)Math.Round(online*100d/total);var foraPct=prod==0?0:(int)Math.Round(fora*100d/prod);
        var indice=(int)Math.Round(produtividade*.55+disponibilidade*.30+Math.Max(0,100-ociosidade-naoProd)*.15-Math.Min(15,foraPct/2d)-Math.Min(15,(oppAlta+evidAlta)*2d));
        indice=Math.Clamp(indice,0,100);
        var visao=await ObterIntegracaoVisaoAsync(tenantId,ct);var contextoTi=visao.Contratado&&visao.IntegracaoAtiva;
        string? resumoTi=null;if(contextoTi){var eventos=await ListarContextoVisaoAsync(tenantId,dias,30,ct);var altos=eventos.Count(x=>x.Severidade is "Alta" or "Critica");resumoTi=eventos.Count==0?"VISÃO contratado e integrado; sem eventos técnicos recebidos no período.":$"{eventos.Count} eventos técnicos recebidos do VISÃO no período; {altos} de alta/crítica severidade.";}
        var indicadores=new List<ItemPulsoOperacao>{
            new("produtividade","Produtividade observada",$"{produtividade}%","período","Informativa","Tempo produtivo entre os períodos classificados dentro da jornada."),
            new("disponibilidade","Estações disponíveis",$"{online}/{total}","agora",disponibilidade>=90?"Boa":"Observar","Estações com heartbeat dentro do limite configurado."),
            new("ociosidade","Ociosidade na jornada",$"{ociosidade}%","período",ociosidade<=15?"Boa":"Observar","Tempo ocioso observado dentro da jornada de referência."),
            new("naoprodutivo","Não produtivo na jornada",$"{naoProd}%","período",naoProd<=10?"Boa":"Observar","Uso classificado pelo administrador como Atenção/não produtivo."),
            new("forajornada","Produtivo fora da jornada",FormatarTempo(fora),"período",fora>0?"Observar":"Boa","Atividade produtiva antes/depois da jornada e margens configuradas."),
            new("prioridades","Oportunidades de alto impacto",oppAlta.ToString(),"abertas",oppAlta==0?"Boa":"Prioridade","Oportunidades ainda não concluídas com impacto alto."),
            new("evidencias","Evidências altas",evidAlta.ToString(),"período",evidAlta==0?"Boa":"Observar","Fatos observados com gravidade alta no período.")
        };
        var positivos=new List<string>();var observacoes=new List<string>();var prioridades=new List<string>();var acoes=new List<string>();
        if(disponibilidade>=90)positivos.Add($"{disponibilidade}% das estações estão disponíveis.");else observacoes.Add($"Disponibilidade atual em {disponibilidade}% — revisar estações sem comunicação.");
        if(ociosidade<=15)positivos.Add("Ociosidade dentro de uma faixa operacional baixa no período.");else observacoes.Add($"Ociosidade observada em {ociosidade}% do tempo classificado na jornada.");
        if(naoProd>10)observacoes.Add($"{naoProd}% do tempo classificado na jornada está em itens marcados como Atenção pelo cliente.");
        if(fora>0)observacoes.Add($"Foram observados {FormatarTempo(fora)} de atividade produtiva fora da jornada/margens.");
        if(oppAlta>0){prioridades.Add($"{oppAlta} oportunidade(s) de alto impacto aguardam ação.");acoes.Add("Revisar as oportunidades de alto impacto e registrar responsável/status.");}
        if(evidAlta>0){prioridades.Add($"{evidAlta} evidência(s) de alta gravidade no período.");acoes.Add("Abrir as evidências de alta gravidade e confirmar o contexto antes de decidir qualquer ação.");}
        if(contextoTi){acoes.Add("Cruzar quedas de desempenho com o Contexto TI do VISÃO antes de atribuir causa comportamental.");}
        if(acoes.Count==0)acoes.Add("Manter acompanhamento periódico e comparar a tendência com a semana anterior.");
        var confianca=relatorio.Linhas.Count>=Math.Max(1,total/2)?"Boa":"Parcial";
        return new(indice,confianca,indicadores,positivos,observacoes,prioridades,acoes,contextoTi,resumoTi);
    }

    public async Task<IReadOnlyCollection<RelatorioAgendadoResumo>> ListarRelatoriosAgendadosAsync(Guid tenantId,CancellationToken ct=default)
    {
        const string sql="SELECT id,nome,tipo_relatorio,periodicidade,hora_local,dia_semana,dia_mes,fuso_horario,destinatarios,ativo,ultima_execucao_utc,proxima_execucao_utc,ultimo_status FROM relatorios_agendados WHERE tenant_id=@tenant ORDER BY ativo DESC,nome";
        var lista=new List<RelatorioAgendadoResumo>();await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))lista.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetFieldValue<TimeOnly>(4).ToString("HH:mm"),r.IsDBNull(5)?null:r.GetInt32(5),r.IsDBNull(6)?null:r.GetInt32(6),r.GetString(7),r.GetString(8),r.GetBoolean(9),r.IsDBNull(10)?null:ComoDataHora(r,10),r.IsDBNull(11)?null:ComoDataHora(r,11),r.IsDBNull(12)?null:r.GetString(12)));
        return lista;
    }

    public async Task CriarRelatorioAgendadoAsync(Guid tenantId,Guid usuarioId,string nome,string periodicidade,string horaLocal,int? diaSemana,int? diaMes,string fuso,string destinatarios,int dias,CancellationToken ct=default)
    {
        periodicidade=periodicidade is "Diario" or "Semanal" or "Mensal"?periodicidade:"Semanal";var hora=TimeOnly.TryParse(horaLocal,out var h)?h:new TimeOnly(8,0);fuso=string.IsNullOrWhiteSpace(fuso)?"America/Sao_Paulo":fuso.Trim();
        var proxima=CalcularProximaExecucao(periodicidade,hora,diaSemana,diaMes,fuso,DateTimeOffset.UtcNow);
        var parametros=$"{{\"dias\":{Math.Clamp(dias,1,90)}}}";
        const string sql="""INSERT INTO relatorios_agendados(tenant_id,criado_por_usuario_id,nome,tipo_relatorio,periodicidade,hora_local,dia_semana,dia_mes,fuso_horario,destinatarios,parametros,proxima_execucao_utc) VALUES(@tenant,@usuario,@nome,'ResumoExecutivo',@periodicidade,@hora,@diaSemana,@diaMes,@fuso,@dest,CAST(@parametros AS jsonb),@proxima)""";
        await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("usuario",usuarioId);cmd.Parameters.AddWithValue("nome",string.IsNullOrWhiteSpace(nome)?"Resumo executivo":nome.Trim());cmd.Parameters.AddWithValue("periodicidade",periodicidade);cmd.Parameters.AddWithValue("hora",hora);cmd.Parameters.Add("diaSemana",NpgsqlDbType.Integer).Value=(object?)diaSemana??DBNull.Value;cmd.Parameters.Add("diaMes",NpgsqlDbType.Integer).Value=(object?)diaMes??DBNull.Value;cmd.Parameters.AddWithValue("fuso",fuso);cmd.Parameters.AddWithValue("dest",destinatarios.Trim());cmd.Parameters.AddWithValue("parametros",parametros);cmd.Parameters.AddWithValue("proxima",proxima);await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AlternarRelatorioAgendadoAsync(Guid tenantId,Guid id,bool ativo,CancellationToken ct=default)
    {await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand("UPDATE relatorios_agendados SET ativo=@ativo,atualizado_em_utc=now() WHERE tenant_id=@tenant AND id=@id",c);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("ativo",ativo);await cmd.ExecuteNonQueryAsync(ct);}

    public async Task<IReadOnlyCollection<FiltroSalvoRelatorioResumo>> ListarFiltrosSalvosAsync(Guid tenantId,Guid usuarioId,CancellationToken ct=default)
    {const string sql="SELECT id,nome,tipo_relatorio,parametros::text,compartilhado,atualizado_em_utc FROM filtros_salvos_relatorio WHERE tenant_id=@tenant AND (usuario_id=@usuario OR compartilhado=true) ORDER BY atualizado_em_utc DESC";var lista=new List<FiltroSalvoRelatorioResumo>();await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("usuario",usuarioId);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))lista.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetBoolean(4),ComoDataHora(r,5)));return lista;}

    public async Task SalvarFiltroAsync(Guid tenantId,Guid usuarioId,string nome,int dias,bool compartilhado,DateOnly? inicio=null,DateOnly? fim=null,CancellationToken ct=default)
    {
        dias=Math.Clamp(dias,1,365);
        if(inicio.HasValue || fim.HasValue)
        {
            fim ??= DateOnly.FromDateTime(DateTime.UtcNow);
            inicio ??= fim.Value.AddDays(-(dias-1));
            if(inicio.Value>fim.Value)(inicio,fim)=(fim,inicio);
            if(fim.Value.DayNumber-inicio.Value.DayNumber>365)inicio=fim.Value.AddDays(-365);
            dias=fim.Value.DayNumber-inicio.Value.DayNumber+1;
        }
        var parametros=inicio.HasValue&&fim.HasValue
            ? $"{{\"dias\":{dias},\"inicio\":\"{inicio:yyyy-MM-dd}\",\"fim\":\"{fim:yyyy-MM-dd}\"}}"
            : $"{{\"dias\":{dias}}}";
        await using var c=fabrica.Criar();await c.OpenAsync(ct);
        await using var cmd=new NpgsqlCommand("INSERT INTO filtros_salvos_relatorio(tenant_id,usuario_id,nome,tipo_relatorio,parametros,compartilhado) VALUES(@tenant,@usuario,@nome,'JornadaAtividade',CAST(@parametros AS jsonb),@comp)",c);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("usuario",usuarioId);
        cmd.Parameters.AddWithValue("nome",string.IsNullOrWhiteSpace(nome)?(inicio.HasValue?$"Período {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}":$"Período {dias} dias"):nome.Trim());
        cmd.Parameters.AddWithValue("parametros",parametros);cmd.Parameters.AddWithValue("comp",compartilhado);await cmd.ExecuteNonQueryAsync(ct);
    }


    public async Task<ConfiguracaoRetencaoResumo> ObterRetencaoAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT retencao_telemetria_dias,retencao_evidencias_dias,retencao_notificacoes_dias,retencao_emails_dias,retencao_auditoria_dias
            FROM configuracoes_monitoramento WHERE tenant_id=@tenant;
            """;
        await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new(r.GetInt32(0),r.GetInt32(1),r.GetInt32(2),r.GetInt32(3),r.GetInt32(4)) : new(180,90,180,90,365);
    }

    public async Task SalvarRetencaoAsync(Guid tenantId,int telemetriaDias,int evidenciasDias,int notificacoesDias,int emailsDias,int auditoriaDias,CancellationToken ct=default)
    {
        const string sql="""
            UPDATE configuracoes_monitoramento SET
              retencao_telemetria_dias=@t,retencao_evidencias_dias=@e,retencao_notificacoes_dias=@n,
              retencao_emails_dias=@m,retencao_auditoria_dias=@a,atualizado_em_utc=now()
            WHERE tenant_id=@tenant;
            """;
        await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("t",Math.Clamp(telemetriaDias,7,3650));cmd.Parameters.AddWithValue("e",Math.Clamp(evidenciasDias,7,3650));
        cmd.Parameters.AddWithValue("n",Math.Clamp(notificacoesDias,7,3650));cmd.Parameters.AddWithValue("m",Math.Clamp(emailsDias,7,3650));cmd.Parameters.AddWithValue("a",Math.Clamp(auditoriaDias,30,3650));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyCollection<ExclusaoMonitoramentoResumo>> ListarExclusoesAsync(Guid tenantId,CancellationToken ct=default)
    {
        const string sql="""
            SELECT x.id,x.departamento_id,d.nome,x.tipo,x.valor,x.motivo,x.ativo
            FROM exclusoes_monitoramento x LEFT JOIN departamentos d ON d.id=x.departamento_id AND d.tenant_id=x.tenant_id
            WHERE x.tenant_id=@tenant ORDER BY x.ativo DESC,x.tipo,x.valor;
            """;
        var lista=new List<ExclusaoMonitoramentoResumo>();await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);
        await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))lista.Add(new(r.GetGuid(0),r.IsDBNull(1)?null:r.GetGuid(1),r.IsDBNull(2)?null:r.GetString(2),r.GetString(3),r.GetString(4),r.IsDBNull(5)?null:r.GetString(5),r.GetBoolean(6)));return lista;
    }

    public async Task CriarExclusaoAsync(Guid tenantId,Guid? departamentoId,string tipo,string valor,string? motivo,CancellationToken ct=default)
    {
        tipo=tipo=="Dominio"?"Dominio":"Aplicativo";valor=(valor??string.Empty).Trim();if(string.IsNullOrWhiteSpace(valor))throw new InvalidOperationException("Informe o aplicativo ou domínio a excluir do monitoramento.");
        const string sql="""
            INSERT INTO exclusoes_monitoramento(tenant_id,departamento_id,tipo,valor,motivo)
            VALUES(@tenant,@departamento,@tipo,@valor,@motivo)
            ON CONFLICT(tenant_id,(COALESCE(departamento_id,'00000000-0000-0000-0000-000000000000'::uuid)),tipo,(lower(valor)))
            DO UPDATE SET motivo=EXCLUDED.motivo,ativo=true,atualizado_em_utc=now();
            """;
        await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.Add("departamento",NpgsqlDbType.Uuid).Value=(object?)departamentoId??DBNull.Value;cmd.Parameters.AddWithValue("tipo",tipo);cmd.Parameters.AddWithValue("valor",valor);cmd.Parameters.Add("motivo",NpgsqlDbType.Varchar).Value=(object?)Limpar(motivo)??DBNull.Value;await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AlternarExclusaoAsync(Guid tenantId,Guid id,bool ativo,CancellationToken ct=default)
    {await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand("UPDATE exclusoes_monitoramento SET ativo=@ativo,atualizado_em_utc=now() WHERE tenant_id=@tenant AND id=@id",c);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("ativo",ativo);await cmd.ExecuteNonQueryAsync(ct);}

    public async Task<IReadOnlyCollection<CustoSoftwareResumo>> ListarCustosSoftwareAsync(Guid tenantId,CancellationToken ct=default)
    {
        const string sql="""
            SELECT c.id,c.processo_ou_produto,c.quantidade_licencas,c.custo_mensal_unitario,c.observacao,c.ativo,
                   COUNT(DISTINCT NULLIF(p.usuario_windows,''))::int usuarios
            FROM custos_software c
            LEFT JOIN periodos_atividade p ON p.tenant_id=c.tenant_id AND p.inicio_utc>=now()-interval '30 days'
              AND lower(COALESCE(p.processo_executavel,'')) LIKE ('%'||lower(c.processo_ou_produto)||'%')
            WHERE c.tenant_id=@tenant GROUP BY c.id ORDER BY c.ativo DESC,c.processo_ou_produto;
            """;
        var lista=new List<CustoSoftwareResumo>();await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct)){var qtd=r.GetInt32(2);var unit=r.GetDecimal(3);var usuarios=r.GetInt32(6);lista.Add(new(r.GetGuid(0),r.GetString(1),qtd,unit,r.IsDBNull(4)?null:r.GetString(4),r.GetBoolean(5),usuarios,qtd*unit,Math.Max(0,qtd-usuarios)*unit));}return lista;
    }

    public async Task SalvarCustoSoftwareAsync(Guid tenantId,string processoOuProduto,int quantidade,decimal custoUnitario,string? observacao,CancellationToken ct=default)
    {
        processoOuProduto=(processoOuProduto??string.Empty).Trim();if(string.IsNullOrWhiteSpace(processoOuProduto))throw new InvalidOperationException("Informe o processo/produto de software.");
        const string sql="""
            INSERT INTO custos_software(tenant_id,processo_ou_produto,quantidade_licencas,custo_mensal_unitario,observacao)
            VALUES(@tenant,@produto,@qtd,@custo,@obs)
            ON CONFLICT(tenant_id,(lower(processo_ou_produto))) DO UPDATE SET quantidade_licencas=EXCLUDED.quantidade_licencas,custo_mensal_unitario=EXCLUDED.custo_mensal_unitario,observacao=EXCLUDED.observacao,ativo=true,atualizado_em_utc=now();
            """;
        await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("produto",processoOuProduto);cmd.Parameters.AddWithValue("qtd",Math.Clamp(quantidade,1,1000000));cmd.Parameters.AddWithValue("custo",Math.Max(0,custoUnitario));cmd.Parameters.Add("obs",NpgsqlDbType.Text).Value=(object?)Limpar(observacao)??DBNull.Value;await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AlternarCustoSoftwareAsync(Guid tenantId,Guid id,bool ativo,CancellationToken ct=default)
    {await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand("UPDATE custos_software SET ativo=@ativo,atualizado_em_utc=now() WHERE tenant_id=@tenant AND id=@id",c);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("ativo",ativo);await cmd.ExecuteNonQueryAsync(ct);}

    public async Task<IReadOnlyCollection<ReferenciaOperacionalResumo>> ListarReferenciasOperacionaisAsync(Guid tenantId,CancellationToken ct=default)
    {
        const string sql="""SELECT r.id,r.departamento_id,d.nome,r.nome,r.metrica,r.valor_referencia,r.unidade,r.ativo FROM referencias_operacionais r LEFT JOIN departamentos d ON d.id=r.departamento_id AND d.tenant_id=r.tenant_id WHERE r.tenant_id=@tenant ORDER BY r.ativo DESC,r.nome""";
        var lista=new List<ReferenciaOperacionalResumo>();await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))lista.Add(new(r.GetGuid(0),r.IsDBNull(1)?null:r.GetGuid(1),r.IsDBNull(2)?null:r.GetString(2),r.GetString(3),r.GetString(4),r.GetDecimal(5),r.GetString(6),r.GetBoolean(7)));return lista;
    }

    public async Task SalvarReferenciaOperacionalAsync(Guid tenantId,Guid? departamentoId,string nome,string metrica,decimal valor,string unidade,CancellationToken ct=default)
    {
        var validas=new[]{"FocoPercentual","OciosidadePercentual","ProdutivoMinutos","UsoSistemaPrincipalMinutos","CargaUtilizacaoPercentual"};if(!validas.Contains(metrica))throw new InvalidOperationException("Métrica de referência inválida.");
        const string sql="INSERT INTO referencias_operacionais(tenant_id,departamento_id,nome,metrica,valor_referencia,unidade) VALUES(@tenant,@departamento,@nome,@metrica,@valor,@unidade)";await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.Add("departamento",NpgsqlDbType.Uuid).Value=(object?)departamentoId??DBNull.Value;cmd.Parameters.AddWithValue("nome",string.IsNullOrWhiteSpace(nome)?metrica:nome.Trim());cmd.Parameters.AddWithValue("metrica",metrica);cmd.Parameters.AddWithValue("valor",Math.Max(0,valor));cmd.Parameters.AddWithValue("unidade",string.IsNullOrWhiteSpace(unidade)?"valor":unidade.Trim());await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ResumoCapacidadeOperacional> ObterCapacidadeOperacionalAsync(Guid tenantId,int dias=7,CancellationToken ct=default)
    {
        dias=Math.Clamp(dias,1,90);
        const string sql="""
            WITH base AS (
              SELECT a.id,a.hostname,a.responsavel,COALESCE(d.nome,'') equipe,
                     COALESCE(j.carga_horaria_diaria_minutos,cm.carga_horaria_diaria_minutos,480) carga,
                     (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario,'America/Sao_Paulo'))::date dia,
                     SUM(CASE WHEN p.estado IN (1,5) THEN EXTRACT(EPOCH FROM(p.fim_utc-p.inicio_utc)) ELSE 0 END)/60 minutos_engajados
              FROM ativos a
              JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id
              LEFT JOIN configuracoes_jornada_ativo j ON j.tenant_id=a.tenant_id AND j.ativo_id=a.id
              LEFT JOIN departamentos d ON d.tenant_id=a.tenant_id AND d.id=a.departamento_id
              LEFT JOIN periodos_atividade p ON p.tenant_id=a.tenant_id AND p.ativo_id=a.id AND p.inicio_utc>=now()-(@dias*interval '1 day')
              WHERE a.tenant_id=@tenant AND a.ativo=true
              GROUP BY a.id,a.hostname,a.responsavel,d.nome,j.carga_horaria_diaria_minutos,cm.carga_horaria_diaria_minutos,
                       (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario,'America/Sao_Paulo'))::date
            ), resumo AS (
              SELECT id,hostname,responsavel,equipe,MAX(carga)::int carga,
                     COALESCE(AVG(minutos_engajados) FILTER(WHERE dia IS NOT NULL),0)::int media
              FROM base GROUP BY id,hostname,responsavel,equipe
            )
            SELECT id,hostname,responsavel,equipe,media,carga,
                   CASE WHEN carga<=0 THEN 0 ELSE ROUND(media*100.0/carga)::int END percentual
            FROM resumo ORDER BY percentual DESC,hostname;
            """;
        var itens=new List<ItemCapacidadeOperacional>();
        await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("dias",dias);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))
        {
            var pct=r.GetInt32(6);
            // Faixas são referências gerenciais e podem ser aprofundadas com as Referências Operacionais do tenant.
            var situacao=pct>115?"Sobrecarregado":pct>0&&pct<70?"Subutilizado":"Equilibrado";
            itens.Add(new(r.GetGuid(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),r.GetInt32(4),r.GetInt32(5),pct,situacao));
        }
        return new(itens.Count(x=>x.Situacao=="Subutilizado"),itens.Count(x=>x.Situacao=="Equilibrado"),itens.Count(x=>x.Situacao=="Sobrecarregado"),itens);
    }

    public async Task<ResumoPadroesRepetitivos> ObterPadroesRepetitivosAsync(Guid tenantId,int dias=7,CancellationToken ct=default)
    {
        dias=Math.Clamp(dias,1,90);
        const string sql="""
            WITH base AS (
              SELECT COALESCE(NULLIF(a.responsavel,''),NULLIF(p.usuario_windows,'')) pessoa,COALESCE(d.nome,'') equipe,
                     lower(COALESCE(p.processo_executavel,'')) processo,
                     COUNT(*)::int segmentos,COUNT(DISTINCT (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario,'America/Sao_Paulo'))::date)::int dias_uso,
                     COALESCE(AVG(EXTRACT(EPOCH FROM(p.fim_utc-p.inicio_utc))/60),0)::int media_minutos
              FROM periodos_atividade p
              JOIN ativos a ON a.id=p.ativo_id AND a.tenant_id=p.tenant_id
              JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id
              LEFT JOIN configuracoes_jornada_ativo j ON j.tenant_id=a.tenant_id AND j.ativo_id=a.id
              LEFT JOIN departamentos d ON d.id=a.departamento_id AND d.tenant_id=a.tenant_id
              WHERE p.tenant_id=@tenant AND p.estado=1 AND p.inicio_utc>=now()-(@dias*interval '1 day')
                AND p.processo_executavel IS NOT NULL AND length(trim(p.processo_executavel))>0
              GROUP BY COALESCE(NULLIF(a.responsavel,''),NULLIF(p.usuario_windows,'')),d.nome,lower(COALESCE(p.processo_executavel,''))
            )
            SELECT pessoa,equipe,processo,segmentos,dias_uso,
                   CASE WHEN dias_uso=0 THEN 0 ELSE ROUND(segmentos::numeric/dias_uso)::int END media_dia,
                   media_minutos
            FROM base
            WHERE dias_uso>0 AND segmentos>=GREATEST(20,dias_uso*8) AND media_minutos<=10
            ORDER BY media_dia DESC,segmentos DESC LIMIT 15;
            """;
        var itens=new List<PadraoRepetitivoOperacional>();
        await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("dias",dias);await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))
        {
            var mediaDia=r.GetInt32(5);var mediaDuracao=r.GetInt32(6);
            var leitura=$"Padrão recorrente observado: cerca de {mediaDia} segmentos/dia, com duração média de {mediaDuracao} min. Avaliar se há etapa repetitiva passível de simplificação/automação.";
            itens.Add(new(r.GetString(2),r.IsDBNull(0)?null:r.GetString(0),r.IsDBNull(1)?null:r.GetString(1),r.GetInt32(3),r.GetInt32(4),mediaDia,mediaDuracao,leitura));
        }
        return new(itens.Count,itens);
    }

    public async Task<ResumoAdocaoIa> ObterAdocaoIaAsync(Guid tenantId,int dias=30,CancellationToken ct=default)
    {
        const string sql="""
            WITH uso AS (
              SELECT p.ativo_id,p.usuario_windows,lower(COALESCE(p.processo_executavel,'')) item,EXTRACT(EPOCH FROM(p.fim_utc-p.inicio_utc)) segundos
              FROM periodos_atividade p WHERE p.tenant_id=@tenant AND p.inicio_utc>=now()-(@dias*interval '1 day')
                AND (lower(COALESCE(p.processo_executavel,'')) LIKE ANY(ARRAY['%chatgpt%','%copilot%','%claude%','%cursor%','%gemini%']))
              UNION ALL
              SELECT s.ativo_id,s.usuario_windows,lower(s.dominio),EXTRACT(EPOCH FROM(s.fim_utc-s.inicio_utc))
              FROM sites_utilizados s WHERE s.tenant_id=@tenant AND s.inicio_utc>=now()-(@dias*interval '1 day')
                AND (lower(s.dominio) LIKE ANY(ARRAY['%chatgpt.com%','%copilot.microsoft.com%','%gemini.google.com%','%claude.ai%']))
            )
            SELECT COUNT(DISTINCT NULLIF(usuario_windows,''))::int,COUNT(DISTINCT ativo_id)::int,COALESCE(SUM(segundos),0)::bigint,
                   COALESCE(string_agg(DISTINCT CASE WHEN item LIKE '%chatgpt%' THEN 'ChatGPT' WHEN item LIKE '%copilot%' THEN 'Copilot' WHEN item LIKE '%gemini%' THEN 'Gemini' WHEN item LIKE '%claude%' THEN 'Claude' WHEN item LIKE '%cursor%' THEN 'Cursor' ELSE item END,', '),'')
            FROM uso;
            """;
        await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("dias",Math.Clamp(dias,1,365));await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return new(0,0,0,Array.Empty<string>());var ferramentas=r.GetString(3).Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);return new(r.GetInt32(0),r.GetInt32(1),r.GetInt64(2),ferramentas);
    }

    public static DateTimeOffset CalcularProximaExecucao(string periodicidade,TimeOnly hora,int? diaSemana,int? diaMes,string fuso,DateTimeOffset referenciaUtc)
    {
        TimeZoneInfo zona;try{zona=TimeZoneInfo.FindSystemTimeZoneById(fuso);}catch{zona=TimeZoneInfo.Utc;}
        var local=TimeZoneInfo.ConvertTime(referenciaUtc,zona);var data=DateOnly.FromDateTime(local.DateTime);DateOnly alvo=data;
        if(periodicidade=="Semanal"){var desejado=Math.Clamp(diaSemana??1,1,7);var atual=((int)local.DayOfWeek+6)%7+1;var add=(desejado-atual+7)%7;alvo=data.AddDays(add);if(add==0&&local.TimeOfDay>=hora.ToTimeSpan())alvo=alvo.AddDays(7);}
        else if(periodicidade=="Mensal"){var d=Math.Clamp(diaMes??1,1,28);alvo=new DateOnly(data.Year,data.Month,d);if(alvo<data || (alvo==data&&local.TimeOfDay>=hora.ToTimeSpan()))alvo=alvo.AddMonths(1);}
        else if(local.TimeOfDay>=hora.ToTimeSpan())alvo=data.AddDays(1);
        var localNaoEspecificado=new DateTime(alvo.Year,alvo.Month,alvo.Day,hora.Hour,hora.Minute,0,DateTimeKind.Unspecified);var utc=TimeZoneInfo.ConvertTimeToUtc(localNaoEspecificado,zona);return new DateTimeOffset(utc,TimeSpan.Zero);
    }

    private static string Sha256(string valor)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor))).ToLowerInvariant();
    private static string? Limpar(string? valor)=>string.IsNullOrWhiteSpace(valor)?null:valor.Trim();
    private static DateTimeOffset ComoDataHora(NpgsqlDataReader r,int i)=>new(DateTime.SpecifyKind(r.GetDateTime(i),DateTimeKind.Utc));
    private static string FormatarTempo(long segundos){var t=TimeSpan.FromSeconds(Math.Max(0,segundos));return t.TotalHours>=1?$"{(int)t.TotalHours}h {t.Minutes:00}m":$"{t.Minutes}m";}
}
