using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using ShareIT.Clara.Comum.Configuracao;
using ShareIT.Clara.Comum.Seguranca;
using ShareIT.Clara.Dados.Banco;
using ShareIT.Clara.Dominio.Contratos;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: centralizar jornada, alertas, notificações, relatórios, preferências e governança do tenant.
/// Origem: painel Web e regras de monitoramento.
/// Destino: PostgreSQL, central de notificações, caixa de saída de e-mails e políticas do agente.
/// Impacto: separa fato observado de interpretação gerencial e bloqueia recursos invasivos sem governança.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioGovernanca(FabricaConexoesClara fabrica, ConfiguracaoClara configuracao)
{
    public async Task<string> ObterTemaUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT tema_preferido FROM usuarios WHERE id=@id", conexao);
        cmd.Parameters.AddWithValue("id", usuarioId);
        var valor = Convert.ToString(await cmd.ExecuteScalarAsync(ct));
        return valor is "Claro" or "Escuro" or "Sistema" ? valor : "Sistema";
    }

    public async Task SalvarTemaUsuarioAsync(Guid usuarioId, string tema, CancellationToken ct = default)
    {
        tema = tema is "Claro" or "Escuro" or "Sistema" ? tema : "Sistema";
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("UPDATE usuarios SET tema_preferido=@tema WHERE id=@id", conexao);
        cmd.Parameters.AddWithValue("id", usuarioId);
        cmd.Parameters.AddWithValue("tema", tema);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ConfiguracaoJornadaEstacao> ObterJornadaEstacaoAsync(Guid tenantId, Guid ativoId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT j.ativo_id IS NOT NULL,
                   COALESCE(j.jornada_inicio,cm.jornada_inicio),COALESCE(j.jornada_fim,cm.jornada_fim),
                   COALESCE(j.carga_horaria_diaria_minutos,cm.carga_horaria_diaria_minutos),
                   COALESCE(j.margem_antes_minutos,cm.margem_antes_minutos),COALESCE(j.margem_depois_minutos,cm.margem_depois_minutos),
                   COALESCE(j.dias_jornada,cm.dias_jornada),
                   COALESCE(j.monitoramento_inicio,cm.horario_inicio),COALESCE(j.monitoramento_fim,cm.horario_fim),
                   COALESCE(j.dias_monitorados,cm.dias_monitorados),COALESCE(j.fuso_horario,cm.fuso_horario)
            FROM ativos a
            JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id
            LEFT JOIN configuracoes_jornada_ativo j ON j.ativo_id=a.id AND j.tenant_id=a.tenant_id
            WHERE a.tenant_id=@tenant AND a.id=@ativo AND a.ativo=true
            LIMIT 1;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conexao);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("ativo", ativoId);
        await using var leitor = await cmd.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct)) throw new InvalidOperationException("Estação não encontrada para este tenant.");
        return new ConfiguracaoJornadaEstacao(
            ativoId,
            leitor.GetBoolean(0),
            leitor.GetFieldValue<TimeOnly>(1).ToString("HH:mm"),
            leitor.GetFieldValue<TimeOnly>(2).ToString("HH:mm"),
            leitor.GetInt32(3), leitor.GetInt32(4), leitor.GetInt32(5), leitor.GetString(6),
            leitor.GetFieldValue<TimeOnly>(7).ToString("HH:mm"),
            leitor.GetFieldValue<TimeOnly>(8).ToString("HH:mm"), leitor.GetString(9), leitor.GetString(10));
    }

    public async Task SalvarJornadaEstacaoAsync(
        Guid tenantId, Guid ativoId, Guid usuarioId,
        string jornadaInicio, string jornadaFim, int cargaMinutos, int margemAntes, int margemDepois,
        string diasJornada, string monitoramentoInicio, string monitoramentoFim, string diasMonitorados, string fusoHorario,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO configuracoes_jornada_ativo(
                ativo_id,tenant_id,jornada_inicio,jornada_fim,carga_horaria_diaria_minutos,margem_antes_minutos,margem_depois_minutos,
                dias_jornada,monitoramento_inicio,monitoramento_fim,dias_monitorados,fuso_horario,atualizado_por_usuario_id,atualizado_em_utc)
            SELECT a.id,a.tenant_id,CAST(@ji AS time),CAST(@jf AS time),@carga,@antes,@depois,@dj,
                   CAST(@mi AS time),CAST(@mf AS time),@dm,@fuso,@usuario,now()
            FROM ativos a WHERE a.id=@ativo AND a.tenant_id=@tenant AND a.ativo=true
            ON CONFLICT(ativo_id) DO UPDATE SET
                jornada_inicio=EXCLUDED.jornada_inicio,jornada_fim=EXCLUDED.jornada_fim,
                carga_horaria_diaria_minutos=EXCLUDED.carga_horaria_diaria_minutos,
                margem_antes_minutos=EXCLUDED.margem_antes_minutos,margem_depois_minutos=EXCLUDED.margem_depois_minutos,
                dias_jornada=EXCLUDED.dias_jornada,monitoramento_inicio=EXCLUDED.monitoramento_inicio,
                monitoramento_fim=EXCLUDED.monitoramento_fim,dias_monitorados=EXCLUDED.dias_monitorados,
                fuso_horario=EXCLUDED.fuso_horario,atualizado_por_usuario_id=EXCLUDED.atualizado_por_usuario_id,atualizado_em_utc=now();
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conexao);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("ativo", ativoId);
        cmd.Parameters.AddWithValue("usuario", usuarioId);
        cmd.Parameters.AddWithValue("ji", NormalizarHorario(jornadaInicio, "08:00"));
        cmd.Parameters.AddWithValue("jf", NormalizarHorario(jornadaFim, "18:00"));
        cmd.Parameters.AddWithValue("carga", Math.Clamp(cargaMinutos, 1, 1440));
        cmd.Parameters.AddWithValue("antes", Math.Clamp(margemAntes, 0, 720));
        cmd.Parameters.AddWithValue("depois", Math.Clamp(margemDepois, 0, 720));
        cmd.Parameters.AddWithValue("dj", NormalizarDias(diasJornada));
        cmd.Parameters.AddWithValue("mi", NormalizarHorario(monitoramentoInicio, "08:00"));
        cmd.Parameters.AddWithValue("mf", NormalizarHorario(monitoramentoFim, "18:00"));
        cmd.Parameters.AddWithValue("dm", NormalizarDias(diasMonitorados));
        cmd.Parameters.AddWithValue("fuso", string.IsNullOrWhiteSpace(fusoHorario) ? "America/Sao_Paulo" : fusoHorario.Trim());
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new InvalidOperationException("Estação não encontrada para este tenant.");
    }

    public async Task RemoverSobrescritaJornadaAsync(Guid tenantId, Guid ativoId, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("DELETE FROM configuracoes_jornada_ativo WHERE tenant_id=@tenant AND ativo_id=@ativo", conexao);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("ativo", ativoId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<PainelRelatoriosHistorico> ObterRelatorioHistoricoAsync(Guid tenantId, int dias, CancellationToken ct = default, DateOnly? dataInicio = null, DateOnly? dataFim = null)
    {
        dias = Math.Clamp(dias, 1, 365);
        if (dataInicio.HasValue || dataFim.HasValue)
        {
            dataFim ??= DateOnly.FromDateTime(DateTime.UtcNow);
            dataInicio ??= dataFim.Value.AddDays(-(dias - 1));
            if (dataInicio.Value > dataFim.Value) (dataInicio, dataFim) = (dataFim, dataInicio);
            if (dataFim.Value.DayNumber - dataInicio.Value.DayNumber > 365) dataInicio = dataFim.Value.AddDays(-365);
            dias = dataFim.Value.DayNumber - dataInicio.Value.DayNumber + 1;
        }
        const string sql = """
            WITH base AS (
                SELECT at.id AS ativo_id,at.hostname,COALESCE(at.responsavel,p.usuario_windows) AS pessoa,d.nome AS equipe,
                       EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))::bigint AS segundos,
                       p.estado,p.processo_executavel,
                       COALESCE(ra.classificacao,'Neutro') AS classificacao,
                       (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::time AS hora_local,
                       EXTRACT(ISODOW FROM (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario)))::int AS dia_local,
                       COALESCE(j.jornada_inicio,cm.jornada_inicio) AS jornada_inicio,
                       COALESCE(j.jornada_fim,cm.jornada_fim) AS jornada_fim,
                       COALESCE(j.margem_antes_minutos,cm.margem_antes_minutos) AS margem_antes,
                       COALESCE(j.margem_depois_minutos,cm.margem_depois_minutos) AS margem_depois,
                       COALESCE(j.dias_jornada,cm.dias_jornada) AS dias_jornada,
                       COALESCE(j.carga_horaria_diaria_minutos,cm.carga_horaria_diaria_minutos) AS carga
                FROM periodos_atividade p
                JOIN ativos at ON at.id=p.ativo_id AND at.tenant_id=p.tenant_id
                LEFT JOIN departamentos d ON d.id=at.departamento_id AND d.tenant_id=at.tenant_id
                JOIN configuracoes_monitoramento cm ON cm.tenant_id=p.tenant_id
                LEFT JOIN configuracoes_jornada_ativo j ON j.ativo_id=at.id AND j.tenant_id=at.tenant_id
                LEFT JOIN LATERAL (
                    SELECT r.classificacao FROM regras_aplicativos r
                    WHERE r.tenant_id=p.tenant_id AND lower(r.processo_executavel)=lower(COALESCE(p.processo_executavel,''))
                      AND (r.departamento_id IS NULL OR r.departamento_id=at.departamento_id)
                    ORDER BY (r.departamento_id IS NOT NULL) DESC LIMIT 1
                ) ra ON true
                WHERE p.tenant_id=@tenant
                  AND (
                       (@dataInicio IS NULL AND p.inicio_utc >= now() - (@dias * interval '1 day'))
                       OR
                       (@dataInicio IS NOT NULL AND (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date BETWEEN @dataInicio AND @dataFim)
                  )
            ), marcado AS (
                SELECT *,
                       (dia_local = ANY(string_to_array(dias_jornada,',')::int[])
                        AND CASE WHEN jornada_fim>=jornada_inicio
                                 THEN hora_local BETWEEN jornada_inicio AND jornada_fim
                                 ELSE hora_local>=jornada_inicio OR hora_local<=jornada_fim END) AS dentro_jornada,
                       (dia_local = ANY(string_to_array(dias_jornada,',')::int[])
                        AND CASE WHEN (jornada_fim + (margem_depois * interval '1 minute'))::time >= (jornada_inicio - (margem_antes * interval '1 minute'))::time
                                 THEN hora_local BETWEEN (jornada_inicio - (margem_antes * interval '1 minute'))::time AND (jornada_fim + (margem_depois * interval '1 minute'))::time
                                 ELSE hora_local >= (jornada_inicio - (margem_antes * interval '1 minute'))::time OR hora_local <= (jornada_fim + (margem_depois * interval '1 minute'))::time END) AS dentro_margem
                FROM base
            )
            SELECT ativo_id,hostname,pessoa,equipe,
                   COALESCE(SUM(segundos) FILTER (WHERE dentro_jornada AND estado=1 AND classificacao='Produtivo'),0)::bigint AS produtivo_jornada,
                   COALESCE(SUM(segundos) FILTER (WHERE dentro_jornada AND estado=2),0)::bigint AS ocioso_jornada,
                   COALESCE(SUM(segundos) FILTER (WHERE dentro_jornada AND estado=1 AND classificacao='Atencao'),0)::bigint AS nao_produtivo_jornada,
                   COALESCE(SUM(segundos) FILTER (WHERE NOT dentro_margem AND estado=1 AND classificacao='Produtivo'),0)::bigint AS produtivo_fora,
                   COALESCE(SUM(segundos),0)::bigint AS monitorado,
                   MAX(carga)::int AS carga,
                   MIN(jornada_inicio)::time AS jornada_inicio,MAX(jornada_fim)::time AS jornada_fim
            FROM marcado
            GROUP BY ativo_id,hostname,pessoa,equipe
            ORDER BY pessoa NULLS LAST,hostname;
            """;
        var linhas = new List<LinhaRelatorioHistorico>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conexao);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("dias", dias);
        cmd.Parameters.Add("dataInicio", NpgsqlDbType.Date).Value = (object?)dataInicio ?? DBNull.Value;
        cmd.Parameters.Add("dataFim", NpgsqlDbType.Date).Value = (object?)dataFim ?? DBNull.Value;
        await using var leitor = await cmd.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
        {
            linhas.Add(new LinhaRelatorioHistorico(
                leitor.GetGuid(0), leitor.GetString(1), leitor.IsDBNull(2) ? null : leitor.GetString(2), leitor.IsDBNull(3) ? null : leitor.GetString(3),
                leitor.GetInt64(4), leitor.GetInt64(5), leitor.GetInt64(6), leitor.GetInt64(7), leitor.GetInt64(8), leitor.GetInt32(9),
                $"{leitor.GetFieldValue<TimeOnly>(10).ToString("HH:mm")}–{leitor.GetFieldValue<TimeOnly>(11).ToString("HH:mm")}"));
        }
        var prod = linhas.Sum(x => x.SegundosProdutivosJornada);
        var ocioso = linhas.Sum(x => x.SegundosOciososJornada);
        var naoProd = linhas.Sum(x => x.SegundosNaoProdutivosJornada);
        var fora = linhas.Sum(x => x.SegundosProdutivosForaJornada);
        var insight = fora > 0 && ocioso + naoProd > 0
            ? $"O período mostra {FormatarTempo(ocioso + naoProd)} de ociosidade/não produtividade dentro da jornada e {FormatarTempo(fora)} de atividade produtiva fora da jornada e das margens configuradas. A Clara apresenta os fatos; a avaliação cabe ao gestor."
            : "Use o histórico para comparar produtividade, ociosidade e atividade fora da jornada sem transformar telemetria em julgamento automático.";
        return new PainelRelatoriosHistorico(linhas, dias, prod, ocioso, naoProd, fora, insight);
    }

    public async Task<IReadOnlyCollection<RegraAlertaResumo>> ListarRegrasAlertasAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT r.id,r.nome,r.tipo,r.limite_minutos,r.escopo_tipo,r.departamento_id,d.nome,r.ativo_id,a.hostname,
                   r.severidade,r.canal_plataforma,r.canal_email,r.destinatarios_email,r.destinatarios_perfis,
                   r.gerar_evidencia,r.gerar_oportunidade,r.ativo
            FROM regras_alerta r
            LEFT JOIN departamentos d ON d.id=r.departamento_id AND d.tenant_id=r.tenant_id
            LEFT JOIN ativos a ON a.id=r.ativo_id AND a.tenant_id=r.tenant_id
            WHERE r.tenant_id=@tenant
            ORDER BY r.ativo DESC,r.nome;
            """;
        var lista = new List<RegraAlertaResumo>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conexao);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await cmd.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
            lista.Add(new RegraAlertaResumo(
                leitor.GetGuid(0),leitor.GetString(1),leitor.GetString(2),leitor.GetInt32(3),leitor.GetString(4),
                leitor.IsDBNull(5)?null:leitor.GetGuid(5),leitor.IsDBNull(6)?null:leitor.GetString(6),
                leitor.IsDBNull(7)?null:leitor.GetGuid(7),leitor.IsDBNull(8)?null:leitor.GetString(8),
                leitor.GetString(9),leitor.GetBoolean(10),leitor.GetBoolean(11),leitor.IsDBNull(12)?null:leitor.GetString(12),
                leitor.GetString(13),leitor.GetBoolean(14),leitor.GetBoolean(15),leitor.GetBoolean(16)));
        return lista;
    }

    public async Task<IReadOnlyCollection<NotificacaoResumo>> ListarHistoricoAlertasAsync(Guid tenantId,int limite=100,CancellationToken ct=default)
    {
        limite=Math.Clamp(limite,1,500);
        const string sql="""
            SELECT id,tipo,titulo,resumo,severidade,url_destino,false,criada_em_utc
            FROM notificacoes
            WHERE tenant_id=@tenant AND fonte_tipo='RegraAlerta'
            ORDER BY criada_em_utc DESC
            LIMIT @limite;
            """;
        var lista=new List<NotificacaoResumo>();
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("limite",limite);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))lista.Add(new NotificacaoResumo(r.GetGuid(0),r.GetString(1),r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),r.GetString(4),r.IsDBNull(5)?null:r.GetString(5),r.GetBoolean(6),ComoDataHora(r,7)));
        return lista;
    }

    public async Task CriarRegraAlertaAsync(Guid tenantId, string nome, string tipo, int limiteMinutos, string escopoTipo,
        Guid? departamentoId, Guid? ativoId, string severidade, bool canalPlataforma, bool canalEmail,
        string? destinatariosEmail, string? destinatariosPerfis, bool gerarEvidencia, bool gerarOportunidade,
        CancellationToken ct = default)
    {
        tipo = tipo is "UsoNaoProdutivo" or "Ociosidade" or "ProdutivoForaJornada" or "EstacaoOffline" or "InicioTardio" or "SaidaAntecipada" or "IntervaloLongo" or "AusenciaAtividade" or "CargaAbaixo" or "CargaAcima" ? tipo : "Ociosidade";
        escopoTipo = escopoTipo is "Todos" or "Departamento" or "Estacao" ? escopoTipo : "Todos";
        severidade = severidade is "Informativa" or "Atencao" or "Alta" or "Critica" ? severidade : "Atencao";
        const string sql = """
            INSERT INTO regras_alerta(tenant_id,nome,tipo,limite_minutos,escopo_tipo,departamento_id,ativo_id,severidade,
                canal_plataforma,canal_email,destinatarios_email,destinatarios_perfis,gerar_evidencia,gerar_oportunidade,ativo,atualizado_em_utc)
            VALUES(@tenant,@nome,@tipo,@limite,@escopo,@departamento,@ativo,@severidade,@plataforma,@email,@destinatarios,@perfis,@evidencia,@oportunidade,true,now());
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conexao);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("nome", string.IsNullOrWhiteSpace(nome) ? "Nova regra" : nome.Trim());
        cmd.Parameters.AddWithValue("tipo", tipo);
        cmd.Parameters.AddWithValue("limite", Math.Clamp(limiteMinutos, 1, 1440));
        cmd.Parameters.AddWithValue("escopo", escopoTipo);
        cmd.Parameters.Add("departamento", NpgsqlDbType.Uuid).Value=(object?)departamentoId??DBNull.Value;
        cmd.Parameters.Add("ativo", NpgsqlDbType.Uuid).Value=(object?)ativoId??DBNull.Value;
        cmd.Parameters.AddWithValue("severidade", severidade);
        cmd.Parameters.AddWithValue("plataforma", canalPlataforma);
        cmd.Parameters.AddWithValue("email", canalEmail);
        cmd.Parameters.Add("destinatarios",NpgsqlDbType.Text).Value=(object?)Limpar(destinatariosEmail)??DBNull.Value;
        cmd.Parameters.AddWithValue("perfis",string.IsNullOrWhiteSpace(destinatariosPerfis)?"Administrador,Supervisor":destinatariosPerfis.Trim());
        cmd.Parameters.AddWithValue("evidencia",gerarEvidencia);
        cmd.Parameters.AddWithValue("oportunidade",gerarOportunidade);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AlternarRegraAlertaAsync(Guid tenantId, Guid regraId, bool ativo, CancellationToken ct = default)
    {
        await using var conexao=fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var cmd=new NpgsqlCommand("UPDATE regras_alerta SET ativo=@ativo,atualizado_em_utc=now() WHERE tenant_id=@tenant AND id=@id",conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("id",regraId);cmd.Parameters.AddWithValue("ativo",ativo);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SincronizarNotificacoesAsync(Guid tenantId, Guid? usuarioId = null, CancellationToken ct = default)
    {
        var regras = await ListarRegrasAlertasAsync(tenantId, ct);
        foreach (var regra in regras.Where(x => x.Ativo))
            await AvaliarRegraAsync(tenantId, usuarioId, regra, ct);

        // Governança também expira automaticamente solicitações/documentos e avisa o Administrador
        // quando uma autorização restrita se aproxima do vencimento.
        const string sqlGovernanca = """
            UPDATE solicitacoes_aprovacao_juridica
            SET status='Expirada'
            WHERE tenant_id=@tenant AND status='Pendente' AND expira_em_utc<now();

            UPDATE documentos_governanca
            SET status_validacao='Expirado'
            WHERE tenant_id=@tenant AND validade_ate IS NOT NULL AND validade_ate<current_date
              AND status_validacao NOT IN ('Rejeitado','Expirado');

            INSERT INTO notificacoes(tenant_id,usuario_id,tipo,titulo,resumo,severidade,url_destino,destinatarios_perfis,visivel_plataforma,fonte_tipo,fonte_id,chave_deduplicacao)
            SELECT a.tenant_id,NULL,'Governanca','Autorização jurídica próxima do vencimento',
                   'O recurso '||a.recurso||' possui autorização válida até '||to_char(a.validade_ate,'DD/MM/YYYY')||'. Revise a necessidade e renove a documentação antes do vencimento.',
                   CASE WHEN a.validade_ate<=current_date+7 THEN 'Alta' ELSE 'Atencao' END,
                   '/administracao/governanca','Administrador',true,'AutorizacaoGovernanca',a.id::text,
                   'gov-exp:'||a.id::text||':'||a.validade_ate::text
            FROM autorizacoes_governanca a
            WHERE a.tenant_id=@tenant AND a.revogada_em_utc IS NULL
              AND a.validade_ate BETWEEN current_date AND current_date+30
            ON CONFLICT DO NOTHING;
            """;
        await using (var cg = fabrica.Criar())
        {
            await cg.OpenAsync(ct);
            await using var cmdg = new NpgsqlCommand(sqlGovernanca, cg);
            cmdg.Parameters.AddWithValue("tenant", tenantId);
            await cmdg.ExecuteNonQueryAsync(ct);
        }

        // Oportunidades/evidências de alta relevância também alimentam a central, independentemente de regra específica.
        const string sqlContexto = """
            INSERT INTO notificacoes(tenant_id,usuario_id,tipo,titulo,resumo,severidade,url_destino,destinatarios_perfis,visivel_plataforma,fonte_tipo,fonte_id,chave_deduplicacao)
            SELECT o.tenant_id,NULL,'Oportunidade','Oportunidade: '||o.categoria,o.evidencia_resumida,
                   CASE WHEN o.impacto='Alto' THEN 'Alta' ELSE 'Atencao' END,'/oportunidades','Administrador,Supervisor',true,'Oportunidade',o.id::text,'opp:'||o.id::text
            FROM oportunidades o
            WHERE o.tenant_id=@tenant AND o.detectada_em_utc>=now()-interval '24 hours'
            ON CONFLICT DO NOTHING;

            INSERT INTO notificacoes(tenant_id,usuario_id,tipo,titulo,resumo,severidade,url_destino,destinatarios_perfis,visivel_plataforma,fonte_tipo,fonte_id,chave_deduplicacao)
            SELECT e.tenant_id,NULL,'Evidencia','Evidência: '||e.categoria,e.resumo,
                   CASE WHEN e.gravidade='Alta' THEN 'Alta' WHEN e.gravidade='Atencao' THEN 'Atencao' ELSE 'Informativa' END,
                   '/evidencias','Administrador,Supervisor',true,'Evidencia',e.id::text,'evid:'||e.id::text
            FROM evidencias_contextuais e
            WHERE e.tenant_id=@tenant AND e.observada_em_utc>=now()-interval '24 hours' AND e.gravidade IN ('Alta','Atencao')
            ON CONFLICT DO NOTHING;
            """;
        await using var conexao=fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var cmd=new NpgsqlCommand(sqlContexto,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task AvaliarRegraAsync(Guid tenantId, Guid? usuarioId, RegraAlertaResumo regra, CancellationToken ct)
    {
        string sql;
        string titulo;
        string resumoPrefixo;
        switch (regra.Tipo)
        {
            case "Ociosidade":
                sql = """
                    SELECT a.id,a.hostname,COALESCE(a.responsavel,p.usuario_windows),COALESCE(d.nome,''),
                           COALESCE(SUM(EXTRACT(EPOCH FROM(p.fim_utc-p.inicio_utc))),0)::bigint/60 AS minutos
                    FROM ativos a
                    JOIN periodos_atividade p ON p.ativo_id=a.id AND p.tenant_id=a.tenant_id AND p.estado=2 AND p.inicio_utc>=now()-interval '24 hours'
                    LEFT JOIN departamentos d ON d.id=a.departamento_id AND d.tenant_id=a.tenant_id
                    JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id
                    LEFT JOIN configuracoes_jornada_ativo j ON j.ativo_id=a.id AND j.tenant_id=a.tenant_id
                    WHERE a.tenant_id=@tenant AND a.ativo=true
                      AND EXTRACT(ISODOW FROM(p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario)))::int = ANY(string_to_array(COALESCE(j.dias_jornada,cm.dias_jornada),',')::int[])
                      AND CASE WHEN COALESCE(j.jornada_fim,cm.jornada_fim)>=COALESCE(j.jornada_inicio,cm.jornada_inicio)
                               THEN (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::time BETWEEN COALESCE(j.jornada_inicio,cm.jornada_inicio) AND COALESCE(j.jornada_fim,cm.jornada_fim)
                               ELSE (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::time>=COALESCE(j.jornada_inicio,cm.jornada_inicio)
                                 OR (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::time<=COALESCE(j.jornada_fim,cm.jornada_fim) END
                      AND (@escopo<>'Departamento' OR a.departamento_id=@departamento)
                      AND (@escopo<>'Estacao' OR a.id=@ativo)
                    GROUP BY a.id,a.hostname,a.responsavel,p.usuario_windows,d.nome
                    HAVING COALESCE(SUM(EXTRACT(EPOCH FROM(p.fim_utc-p.inicio_utc))),0)/60>=@limite;
                    """;
                titulo="Ociosidade acima do limite";resumoPrefixo="Ociosidade observada";break;
            case "UsoNaoProdutivo":
                sql = """
                    SELECT a.id,a.hostname,COALESCE(a.responsavel,s.usuario_windows),COALESCE(d.nome,''),
                           COALESCE(SUM(EXTRACT(EPOCH FROM(s.fim_utc-s.inicio_utc))),0)::bigint/60 AS minutos
                    FROM ativos a
                    JOIN sites_utilizados s ON s.ativo_id=a.id AND s.tenant_id=a.tenant_id AND s.inicio_utc>=now()-interval '24 hours'
                    LEFT JOIN departamentos d ON d.id=a.departamento_id AND d.tenant_id=a.tenant_id
                    JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id
                    LEFT JOIN configuracoes_jornada_ativo j ON j.ativo_id=a.id AND j.tenant_id=a.tenant_id
                    LEFT JOIN LATERAL (
                        SELECT r.classificacao FROM regras_sites r
                        WHERE r.tenant_id=s.tenant_id AND lower(r.dominio)=lower(s.dominio)
                          AND (r.departamento_id IS NULL OR r.departamento_id=a.departamento_id)
                        ORDER BY (r.departamento_id IS NOT NULL) DESC LIMIT 1
                    ) rs ON true
                    WHERE a.tenant_id=@tenant AND a.ativo=true AND COALESCE(rs.classificacao,'Neutro')='Atencao'
                      AND EXTRACT(ISODOW FROM(s.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario)))::int = ANY(string_to_array(COALESCE(j.dias_jornada,cm.dias_jornada),',')::int[])
                      AND CASE WHEN COALESCE(j.jornada_fim,cm.jornada_fim)>=COALESCE(j.jornada_inicio,cm.jornada_inicio)
                               THEN (s.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::time BETWEEN COALESCE(j.jornada_inicio,cm.jornada_inicio) AND COALESCE(j.jornada_fim,cm.jornada_fim)
                               ELSE (s.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::time>=COALESCE(j.jornada_inicio,cm.jornada_inicio)
                                 OR (s.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::time<=COALESCE(j.jornada_fim,cm.jornada_fim) END
                      AND (@escopo<>'Departamento' OR a.departamento_id=@departamento)
                      AND (@escopo<>'Estacao' OR a.id=@ativo)
                    GROUP BY a.id,a.hostname,a.responsavel,s.usuario_windows,d.nome
                    HAVING COALESCE(SUM(EXTRACT(EPOCH FROM(s.fim_utc-s.inicio_utc))),0)/60>=@limite;
                    """;
                titulo="Uso não produtivo acima do limite";resumoPrefixo="Atividade classificada como não produtiva";break;
            case "ProdutivoForaJornada":
                sql = """
                    WITH base AS (
                        SELECT a.id,a.hostname,COALESCE(a.responsavel,p.usuario_windows) pessoa,COALESCE(d.nome,'') equipe,
                               EXTRACT(EPOCH FROM(p.fim_utc-p.inicio_utc)) segundos,
                               (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::time hora,
                               EXTRACT(ISODOW FROM(p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario)))::int dia,
                               COALESCE(j.jornada_inicio,cm.jornada_inicio) ji,COALESCE(j.jornada_fim,cm.jornada_fim) jf,
                               COALESCE(j.margem_antes_minutos,cm.margem_antes_minutos) margem_antes,
                               COALESCE(j.margem_depois_minutos,cm.margem_depois_minutos) margem_depois,
                               COALESCE(j.dias_jornada,cm.dias_jornada) dj,COALESCE(ra.classificacao,'Neutro') classificacao
                        FROM ativos a
                        JOIN periodos_atividade p ON p.ativo_id=a.id AND p.tenant_id=a.tenant_id AND p.estado=1 AND p.inicio_utc>=now()-interval '24 hours'
                        LEFT JOIN departamentos d ON d.id=a.departamento_id AND d.tenant_id=a.tenant_id
                        JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id
                        LEFT JOIN configuracoes_jornada_ativo j ON j.ativo_id=a.id AND j.tenant_id=a.tenant_id
                        LEFT JOIN LATERAL (
                            SELECT r.classificacao FROM regras_aplicativos r WHERE r.tenant_id=a.tenant_id
                              AND lower(r.processo_executavel)=lower(COALESCE(p.processo_executavel,''))
                              AND (r.departamento_id IS NULL OR r.departamento_id=a.departamento_id)
                            ORDER BY (r.departamento_id IS NOT NULL) DESC LIMIT 1
                        ) ra ON true
                        WHERE a.tenant_id=@tenant AND a.ativo=true
                          AND (@escopo<>'Departamento' OR a.departamento_id=@departamento)
                          AND (@escopo<>'Estacao' OR a.id=@ativo)
                    )
                    SELECT id,hostname,pessoa,equipe,COALESCE(SUM(segundos),0)::bigint/60 minutos
                    FROM base
                    WHERE classificacao='Produtivo' AND NOT (
                      dia = ANY(string_to_array(dj,',')::int[]) AND
                      CASE WHEN (jf + (margem_depois * interval '1 minute'))::time >= (ji - (margem_antes * interval '1 minute'))::time
                           THEN hora BETWEEN (ji - (margem_antes * interval '1 minute'))::time AND (jf + (margem_depois * interval '1 minute'))::time
                           ELSE hora >= (ji - (margem_antes * interval '1 minute'))::time OR hora <= (jf + (margem_depois * interval '1 minute'))::time END)
                    GROUP BY id,hostname,pessoa,equipe
                    HAVING COALESCE(SUM(segundos),0)/60>=@limite;
                    """;
                titulo="Atividade produtiva fora da jornada";resumoPrefixo="Atividade produtiva fora da jornada de referência";break;
            case "InicioTardio":
                sql = """
                    WITH base AS (
                      SELECT a.id,a.hostname,a.responsavel,COALESCE(d.nome,'') equipe,
                             COALESCE(j.jornada_inicio,cm.jornada_inicio) ji,
                             COALESCE(j.fuso_horario,cm.fuso_horario) fuso,
                             COALESCE(j.dias_jornada,cm.dias_jornada) dias,
                             MIN((p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::time) primeiro,
                             (now() AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date hoje_local
                      FROM ativos a
                      JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id
                      LEFT JOIN configuracoes_jornada_ativo j ON j.ativo_id=a.id AND j.tenant_id=a.tenant_id
                      LEFT JOIN departamentos d ON d.id=a.departamento_id AND d.tenant_id=a.tenant_id
                      LEFT JOIN periodos_atividade p ON p.ativo_id=a.id AND p.tenant_id=a.tenant_id
                         AND (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date=(now() AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date
                      WHERE a.tenant_id=@tenant AND a.ativo=true
                        AND (@escopo<>'Departamento' OR a.departamento_id=@departamento)
                        AND (@escopo<>'Estacao' OR a.id=@ativo)
                      GROUP BY a.id,a.hostname,a.responsavel,d.nome,j.jornada_inicio,cm.jornada_inicio,j.fuso_horario,cm.fuso_horario,j.dias_jornada,cm.dias_jornada
                    )
                    SELECT id,hostname,responsavel,equipe,
                           GREATEST(0,FLOOR(EXTRACT(EPOCH FROM(primeiro-ji))/60))::bigint minutos
                    FROM base
                    WHERE primeiro IS NOT NULL
                      AND EXTRACT(ISODOW FROM hoje_local)::int=ANY(string_to_array(dias,',')::int[])
                      AND primeiro > (ji + (@limite*interval '1 minute'))::time;
                    """;
                titulo="Início após a referência";resumoPrefixo="Primeira atividade observada após o início de referência";break;
            case "SaidaAntecipada":
                sql = """
                    WITH base AS (
                      SELECT a.id,a.hostname,a.responsavel,COALESCE(d.nome,'') equipe,
                             COALESCE(j.jornada_fim,cm.jornada_fim) jf,
                             COALESCE(j.fuso_horario,cm.fuso_horario) fuso,
                             COALESCE(j.dias_jornada,cm.dias_jornada) dias,
                             MAX((p.fim_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::time) ultimo,
                             (now() AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::time agora_local,
                             (now() AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date hoje_local
                      FROM ativos a JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id
                      LEFT JOIN configuracoes_jornada_ativo j ON j.ativo_id=a.id AND j.tenant_id=a.tenant_id
                      LEFT JOIN departamentos d ON d.id=a.departamento_id AND d.tenant_id=a.tenant_id
                      LEFT JOIN periodos_atividade p ON p.ativo_id=a.id AND p.tenant_id=a.tenant_id
                         AND (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date=(now() AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date
                      WHERE a.tenant_id=@tenant AND a.ativo=true
                        AND (@escopo<>'Departamento' OR a.departamento_id=@departamento)
                        AND (@escopo<>'Estacao' OR a.id=@ativo)
                      GROUP BY a.id,a.hostname,a.responsavel,d.nome,j.jornada_fim,cm.jornada_fim,j.fuso_horario,cm.fuso_horario,j.dias_jornada,cm.dias_jornada
                    )
                    SELECT id,hostname,responsavel,equipe,
                           GREATEST(0,FLOOR(EXTRACT(EPOCH FROM(jf-ultimo))/60))::bigint minutos
                    FROM base
                    WHERE ultimo IS NOT NULL AND agora_local>=jf
                      AND EXTRACT(ISODOW FROM hoje_local)::int=ANY(string_to_array(dias,',')::int[])
                      AND ultimo < (jf - (@limite*interval '1 minute'))::time;
                    """;
                titulo="Encerramento antes da referência";resumoPrefixo="Última atividade observada antes do fim de referência";break;
            case "IntervaloLongo":
                sql = """
                    SELECT a.id,a.hostname,COALESCE(a.responsavel,p.usuario_windows),COALESCE(d.nome,''),
                           MAX(EXTRACT(EPOCH FROM(p.fim_utc-p.inicio_utc))::bigint)/60 minutos
                    FROM ativos a JOIN periodos_atividade p ON p.ativo_id=a.id AND p.tenant_id=a.tenant_id AND p.estado=2 AND p.inicio_utc>=now()-interval '24 hours'
                    LEFT JOIN departamentos d ON d.id=a.departamento_id AND d.tenant_id=a.tenant_id
                    WHERE a.tenant_id=@tenant AND a.ativo=true
                      AND (@escopo<>'Departamento' OR a.departamento_id=@departamento)
                      AND (@escopo<>'Estacao' OR a.id=@ativo)
                    GROUP BY a.id,a.hostname,a.responsavel,p.usuario_windows,d.nome
                    HAVING MAX(EXTRACT(EPOCH FROM(p.fim_utc-p.inicio_utc)))/60>=@limite;
                    """;
                titulo="Intervalo sem atividade acima da referência";resumoPrefixo="Maior período contínuo de ociosidade";break;
            case "AusenciaAtividade":
                sql = """
                    SELECT a.id,a.hostname,a.responsavel,COALESCE(d.nome,''),@limite::bigint minutos
                    FROM ativos a JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id
                    LEFT JOIN configuracoes_jornada_ativo j ON j.ativo_id=a.id AND j.tenant_id=a.tenant_id
                    LEFT JOIN departamentos d ON d.id=a.departamento_id AND d.tenant_id=a.tenant_id
                    WHERE a.tenant_id=@tenant AND a.ativo=true
                      AND EXTRACT(ISODOW FROM(now() AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario)))::int=ANY(string_to_array(COALESCE(j.dias_jornada,cm.dias_jornada),',')::int[])
                      AND (now() AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::time > (COALESCE(j.jornada_inicio,cm.jornada_inicio)+(@limite*interval '1 minute'))::time
                      AND NOT EXISTS(SELECT 1 FROM periodos_atividade p WHERE p.tenant_id=a.tenant_id AND p.ativo_id=a.id
                          AND (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date=(now() AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date)
                      AND (@escopo<>'Departamento' OR a.departamento_id=@departamento)
                      AND (@escopo<>'Estacao' OR a.id=@ativo);
                    """;
                titulo="Sem atividade observada";resumoPrefixo="Nenhuma atividade de sessão observada após o início de referência";break;
            case "CargaAbaixo":
            case "CargaAcima":
                var abaixo = regra.Tipo == "CargaAbaixo";
                sql = abaixo ? """
                    WITH base AS (
                      SELECT a.id,a.hostname,a.responsavel,COALESCE(d.nome,'') equipe,COALESCE(j.carga_horaria_diaria_minutos,cm.carga_horaria_diaria_minutos) carga,
                             COALESCE(SUM(EXTRACT(EPOCH FROM(p.fim_utc-p.inicio_utc))),0)/60 minutos,
                             COALESCE(j.jornada_fim,cm.jornada_fim) jf,COALESCE(j.fuso_horario,cm.fuso_horario) fuso
                      FROM ativos a JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id LEFT JOIN configuracoes_jornada_ativo j ON j.ativo_id=a.id AND j.tenant_id=a.tenant_id
                      LEFT JOIN departamentos d ON d.id=a.departamento_id AND d.tenant_id=a.tenant_id
                      LEFT JOIN periodos_atividade p ON p.ativo_id=a.id AND p.tenant_id=a.tenant_id AND (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date=(now() AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date
                      WHERE a.tenant_id=@tenant AND a.ativo=true AND (@escopo<>'Departamento' OR a.departamento_id=@departamento) AND (@escopo<>'Estacao' OR a.id=@ativo)
                      GROUP BY a.id,a.hostname,a.responsavel,d.nome,j.carga_horaria_diaria_minutos,cm.carga_horaria_diaria_minutos,j.jornada_fim,cm.jornada_fim,j.fuso_horario,cm.fuso_horario)
                    SELECT id,hostname,responsavel,equipe,GREATEST(0,carga-FLOOR(minutos))::bigint FROM base
                    WHERE (now() AT TIME ZONE fuso)::time>=jf AND minutos < carga-@limite;
                    """ : """
                    WITH base AS (
                      SELECT a.id,a.hostname,a.responsavel,COALESCE(d.nome,'') equipe,COALESCE(j.carga_horaria_diaria_minutos,cm.carga_horaria_diaria_minutos) carga,
                             COALESCE(SUM(EXTRACT(EPOCH FROM(p.fim_utc-p.inicio_utc))),0)/60 minutos
                      FROM ativos a JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id LEFT JOIN configuracoes_jornada_ativo j ON j.ativo_id=a.id AND j.tenant_id=a.tenant_id
                      LEFT JOIN departamentos d ON d.id=a.departamento_id AND d.tenant_id=a.tenant_id
                      LEFT JOIN periodos_atividade p ON p.ativo_id=a.id AND p.tenant_id=a.tenant_id AND (p.inicio_utc AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date=(now() AT TIME ZONE COALESCE(j.fuso_horario,cm.fuso_horario))::date
                      WHERE a.tenant_id=@tenant AND a.ativo=true AND (@escopo<>'Departamento' OR a.departamento_id=@departamento) AND (@escopo<>'Estacao' OR a.id=@ativo)
                      GROUP BY a.id,a.hostname,a.responsavel,d.nome,j.carga_horaria_diaria_minutos,cm.carga_horaria_diaria_minutos)
                    SELECT id,hostname,responsavel,equipe,GREATEST(0,FLOOR(minutos)-carga)::bigint FROM base WHERE minutos > carga+@limite;
                    """;
                titulo=abaixo?"Carga observada abaixo da referência":"Carga observada acima da referência";
                resumoPrefixo=abaixo?"Diferença abaixo da carga diária de referência":"Diferença acima da carga diária de referência";break;
            case "EstacaoOffline":
                sql = """
                    SELECT a.id,a.hostname,a.responsavel,COALESCE(d.nome,''),
                           FLOOR(EXTRACT(EPOCH FROM(now()-COALESCE(g.ultima_comunicacao_utc,a.primeira_comunicacao_utc,now())))/60)::bigint minutos
                    FROM ativos a
                    LEFT JOIN agentes g ON g.ativo_id=a.id AND g.tenant_id=a.tenant_id AND g.ativo=true
                    LEFT JOIN departamentos d ON d.id=a.departamento_id AND d.tenant_id=a.tenant_id
                    WHERE a.tenant_id=@tenant AND a.ativo=true
                      AND COALESCE(g.ultima_comunicacao_utc,a.primeira_comunicacao_utc,now()) < now()-(@limite*interval '1 minute')
                      AND (@escopo<>'Departamento' OR a.departamento_id=@departamento)
                      AND (@escopo<>'Estacao' OR a.id=@ativo);
                    """;
                titulo="Estação sem comunicação";resumoPrefixo="Sem heartbeat";break;
            default:
                return;
        }

        var encontrados=new List<(Guid AtivoId,string Hostname,string? Pessoa,string Equipe,long Minutos)>();
        await using(var conexao=fabrica.Criar())
        {
            await conexao.OpenAsync(ct);
            await using var cmd=new NpgsqlCommand(sql,conexao);
            cmd.Parameters.AddWithValue("tenant",tenantId);
            cmd.Parameters.AddWithValue("limite",regra.LimiteMinutos);
            cmd.Parameters.AddWithValue("escopo",regra.EscopoTipo);
            cmd.Parameters.Add("departamento",NpgsqlDbType.Uuid).Value=(object?)regra.DepartamentoId??DBNull.Value;
            cmd.Parameters.Add("ativo",NpgsqlDbType.Uuid).Value=(object?)regra.AtivoId??DBNull.Value;
            await using var leitor=await cmd.ExecuteReaderAsync(ct);
            while(await leitor.ReadAsync(ct))
                encontrados.Add((leitor.GetGuid(0),leitor.GetString(1),leitor.IsDBNull(2)?null:leitor.GetString(2),leitor.IsDBNull(3)?string.Empty:leitor.GetString(3),leitor.GetInt64(4)));
        }

        foreach(var item in encontrados)
        {
            var dataLocal=DateTime.UtcNow.ToString("yyyyMMdd");
            var chave=$"regra:{regra.Id}:{item.AtivoId}:{dataLocal}";
            var resumo=$"{resumoPrefixo}: {item.Minutos} min (limite configurado: {regra.LimiteMinutos} min).";
            var url=$"/estacoes/{item.AtivoId}";
            var inseriu=await InserirNotificacaoAsync(tenantId,null,regra.Tipo,titulo,resumo,regra.Severidade,url,regra.DestinatariosPerfis,regra.CanalPlataforma,"RegraAlerta",regra.Id.ToString(),chave,ct);
            if(inseriu && regra.GerarEvidencia)
                await InserirEvidenciaAlertaAsync(tenantId,item,regra,resumo,ct);
            if(inseriu && regra.GerarOportunidade)
                await InserirOportunidadeAlertaAsync(tenantId,item,regra,resumo,ct);
            if(inseriu && regra.CanalEmail)
                await EnfileirarEmailsAlertaAsync(tenantId,regra,titulo,resumo,url,ct);
        }
    }

    private async Task<bool> InserirNotificacaoAsync(Guid tenantId,Guid? usuarioId,string tipo,string titulo,string resumo,string severidade,string url,string? destinatariosPerfis,bool visivelPlataforma,string fonteTipo,string fonteId,string chave,CancellationToken ct)
    {
        const string sql="""
            INSERT INTO notificacoes(tenant_id,usuario_id,tipo,titulo,resumo,severidade,url_destino,destinatarios_perfis,visivel_plataforma,fonte_tipo,fonte_id,chave_deduplicacao)
            VALUES(@tenant,@usuario,@tipo,@titulo,@resumo,@severidade,@url,@perfis,@visivel,@fonteTipo,@fonteId,@chave)
            ON CONFLICT DO NOTHING;
            """;
        await using var conexao=fabrica.Criar(); await conexao.OpenAsync(ct); await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.Add("usuario",NpgsqlDbType.Uuid).Value=(object?)usuarioId??DBNull.Value;
        cmd.Parameters.AddWithValue("tipo",tipo);cmd.Parameters.AddWithValue("titulo",titulo);cmd.Parameters.AddWithValue("resumo",resumo);
        cmd.Parameters.AddWithValue("severidade",severidade);cmd.Parameters.AddWithValue("url",url);
        cmd.Parameters.Add("perfis",NpgsqlDbType.Varchar).Value=(object?)Limpar(destinatariosPerfis)??DBNull.Value;cmd.Parameters.AddWithValue("visivel",visivelPlataforma);
        cmd.Parameters.AddWithValue("fonteTipo",fonteTipo);cmd.Parameters.AddWithValue("fonteId",fonteId);cmd.Parameters.AddWithValue("chave",chave);
        return await cmd.ExecuteNonQueryAsync(ct)>0;
    }

    private async Task InserirEvidenciaAlertaAsync(Guid tenantId,(Guid AtivoId,string Hostname,string? Pessoa,string Equipe,long Minutos) item,RegraAlertaResumo regra,string resumo,CancellationToken ct)
    {
        const string sql="""
            INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
            VALUES(@tenant,@ativo,@pessoa,@equipe,@categoria,@gravidade,@resumo,@detalhe,'Regra de alerta',now());
            """;
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("ativo",item.AtivoId);
        cmd.Parameters.Add("pessoa",NpgsqlDbType.Varchar).Value=(object?)item.Pessoa??DBNull.Value;cmd.Parameters.AddWithValue("equipe",item.Equipe);
        cmd.Parameters.AddWithValue("categoria",regra.Tipo);cmd.Parameters.AddWithValue("gravidade",regra.Severidade=="Critica"?"Alta":regra.Severidade);
        cmd.Parameters.AddWithValue("resumo",resumo);cmd.Parameters.AddWithValue("detalhe",$"Regra: {regra.Nome}. Estação: {item.Hostname}.");
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task InserirOportunidadeAlertaAsync(Guid tenantId,(Guid AtivoId,string Hostname,string? Pessoa,string Equipe,long Minutos) item,RegraAlertaResumo regra,string resumo,CancellationToken ct)
    {
        var categoria=regra.Tipo switch {"Ociosidade"=>"OciosidadeAlta","UsoNaoProdutivo"=>"SitesNaoProdutivos","BaixaContinuidade"=>"BaixaContinuidade",_=>regra.Tipo};
        const string sql="""
            INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc,atualizada_em_utc)
            VALUES(@tenant,@ativo,@pessoa,@equipe,@categoria,@evidencia,@impacto,@recomendacao,'Pendente','Regra de alerta',now(),now());
            """;
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("ativo",item.AtivoId);
        cmd.Parameters.Add("pessoa",NpgsqlDbType.Varchar).Value=(object?)item.Pessoa??DBNull.Value;cmd.Parameters.AddWithValue("equipe",item.Equipe);
        cmd.Parameters.AddWithValue("categoria",categoria);cmd.Parameters.AddWithValue("evidencia",resumo);
        cmd.Parameters.AddWithValue("impacto",regra.Severidade is "Alta" or "Critica"?"Alto":"Medio");
        cmd.Parameters.AddWithValue("recomendacao","Revisar o contexto com o responsável/gestor. A Clara apresenta fatos e não presume intenção.");
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task EnfileirarEmailsAlertaAsync(Guid tenantId,RegraAlertaResumo regra,string titulo,string resumo,string url,CancellationToken ct)
    {
        var destinos=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var e in SepararEmails(regra.DestinatariosEmail)) destinos.Add(e);

        // Perfis são resolvidos contra perfil legado e contra o perfil de acesso configurável.
        var perfis=(regra.DestinatariosPerfis??string.Empty)
            .Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();
        if(perfis.Length>0)
        {
            var perfisLower=perfis.Select(x=>x.ToLowerInvariant()).Distinct().ToArray();
            await using var cp=fabrica.Criar();await cp.OpenAsync(ct);
            const string sqlPerfis="""
                SELECT DISTINCT lower(u.email)
                FROM usuarios u
                LEFT JOIN perfis_acesso p ON p.id=u.perfil_acesso_id
                WHERE u.tenant_id=@tenant AND u.ativo=true
                  AND (lower(COALESCE(u.perfil,''))=ANY(@perfis)
                       OR lower(COALESCE(p.codigo,''))=ANY(@perfis)
                       OR lower(COALESCE(p.nome,''))=ANY(@perfis));
                """;
            await using var cmdp=new NpgsqlCommand(sqlPerfis,cp);cmdp.Parameters.AddWithValue("tenant",tenantId);cmdp.Parameters.AddWithValue("perfis",perfisLower);
            await using var rp=await cmdp.ExecuteReaderAsync(ct);while(await rp.ReadAsync(ct))destinos.Add(rp.GetString(0));
        }

        await using(var cc=fabrica.Criar())
        {
            await cc.OpenAsync(ct);
            const string sqlConfig="""
                SELECT cm.destinatarios_alertas,ce.destinatarios_adicionais
                FROM configuracoes_monitoramento cm
                LEFT JOIN configuracoes_email_tenant ce ON ce.tenant_id=cm.tenant_id
                WHERE cm.tenant_id=@tenant;
                """;
            await using var cmdc=new NpgsqlCommand(sqlConfig,cc);cmdc.Parameters.AddWithValue("tenant",tenantId);
            await using var rc=await cmdc.ExecuteReaderAsync(ct);
            if(await rc.ReadAsync(ct))
            {
                foreach(var e in SepararEmails(rc.IsDBNull(0)?null:rc.GetString(0))) destinos.Add(e);
                foreach(var e in SepararEmails(rc.IsDBNull(1)?null:rc.GetString(1))) destinos.Add(e);
            }
        }
        if(destinos.Count==0) return;
        var urlCompleta=configuracao.Servidor.UrlPublica.TrimEnd('/')+url;
        var assunto=$"[{regra.Severidade.ToUpperInvariant()}] {titulo} — Share IT .Clara";
        var corpo=$"{resumo}\n\nAbrir na .Clara: {urlCompleta}\n\nMensagem automática enviada por monitoramento.clara@shareitbrasil.com.br.";
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);
        foreach(var destino in destinos)
        {
            await using var cmd=new NpgsqlCommand("INSERT INTO fila_emails(tenant_id,destinatario,assunto,corpo_texto) VALUES(@tenant,@dest,@assunto,@corpo)",conexao);
            cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("dest",destino);cmd.Parameters.AddWithValue("assunto",assunto);cmd.Parameters.AddWithValue("corpo",corpo);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<PainelNotificacoes> ObterNotificacoesAsync(Guid tenantId, Guid usuarioId, int limite=30, CancellationToken ct=default)
    {
        limite=Math.Clamp(limite,1,100);
        const string sql="""
            WITH eu AS (
                SELECT u.id,lower(COALESCE(u.perfil,'')) perfil_legado,
                       lower(COALESCE(p.codigo,'')) perfil_codigo,lower(COALESCE(p.nome,'')) perfil_nome
                FROM usuarios u LEFT JOIN perfis_acesso p ON p.id=u.perfil_acesso_id
                WHERE u.id=@usuario AND u.tenant_id=@tenant AND u.ativo=true
            )
            SELECT n.id,n.tipo,n.titulo,n.resumo,n.severidade,n.url_destino,
                   EXISTS(SELECT 1 FROM notificacoes_leitura nl WHERE nl.notificacao_id=n.id AND nl.usuario_id=@usuario) AS lida,
                   n.criada_em_utc
            FROM notificacoes n CROSS JOIN eu
            WHERE n.tenant_id=@tenant AND n.visivel_plataforma=true
              AND (
                    n.usuario_id=@usuario OR
                    (n.usuario_id IS NULL AND (
                        n.destinatarios_perfis IS NULL OR btrim(n.destinatarios_perfis)='' OR
                        eu.perfil_legado = ANY(string_to_array(lower(replace(n.destinatarios_perfis,' ','')),',')) OR
                        eu.perfil_codigo = ANY(string_to_array(lower(replace(n.destinatarios_perfis,' ','')),',')) OR
                        eu.perfil_nome = ANY(string_to_array(lower(replace(n.destinatarios_perfis,' ','')),','))
                    ))
                  )
            ORDER BY lida,n.criada_em_utc DESC LIMIT @limite;
            """;
        var itens=new List<NotificacaoResumo>();
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("usuario",usuarioId);cmd.Parameters.AddWithValue("limite",limite);
        await using var leitor=await cmd.ExecuteReaderAsync(ct);
        while(await leitor.ReadAsync(ct)) itens.Add(new NotificacaoResumo(leitor.GetGuid(0),leitor.GetString(1),leitor.GetString(2),leitor.IsDBNull(3)?null:leitor.GetString(3),leitor.GetString(4),leitor.IsDBNull(5)?null:leitor.GetString(5),leitor.GetBoolean(6),ComoDataHora(leitor,7)));
        return new PainelNotificacoes(itens,itens.Count(x=>!x.Lida));
    }

    public async Task<int> ContarNotificacoesNaoLidasAsync(Guid tenantId, Guid usuarioId, CancellationToken ct=default)
    {
        const string sql="""
            WITH eu AS (
                SELECT u.id,lower(COALESCE(u.perfil,'')) perfil_legado,
                       lower(COALESCE(p.codigo,'')) perfil_codigo,lower(COALESCE(p.nome,'')) perfil_nome
                FROM usuarios u LEFT JOIN perfis_acesso p ON p.id=u.perfil_acesso_id
                WHERE u.id=@usuario AND u.tenant_id=@tenant AND u.ativo=true
            )
            SELECT count(*) FROM notificacoes n CROSS JOIN eu
            WHERE n.tenant_id=@tenant AND n.visivel_plataforma=true
              AND NOT EXISTS(SELECT 1 FROM notificacoes_leitura nl WHERE nl.notificacao_id=n.id AND nl.usuario_id=@usuario)
              AND (
                    n.usuario_id=@usuario OR
                    (n.usuario_id IS NULL AND (
                        n.destinatarios_perfis IS NULL OR btrim(n.destinatarios_perfis)='' OR
                        eu.perfil_legado = ANY(string_to_array(lower(replace(n.destinatarios_perfis,' ','')),',')) OR
                        eu.perfil_codigo = ANY(string_to_array(lower(replace(n.destinatarios_perfis,' ','')),',')) OR
                        eu.perfil_nome = ANY(string_to_array(lower(replace(n.destinatarios_perfis,' ','')),','))
                    ))
                  );
            """;
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("usuario",usuarioId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task MarcarNotificacaoLidaAsync(Guid tenantId, Guid usuarioId, Guid id, CancellationToken ct=default)
    {
        const string sql="""
            INSERT INTO notificacoes_leitura(notificacao_id,usuario_id)
            SELECT n.id,@usuario FROM notificacoes n
            WHERE n.id=@id AND n.tenant_id=@tenant
            ON CONFLICT(notificacao_id,usuario_id) DO UPDATE SET lida_em_utc=now();
            """;
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("usuario",usuarioId);await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarcarTodasNotificacoesLidasAsync(Guid tenantId, Guid usuarioId, CancellationToken ct=default)
    {
        const string sql="""
            WITH eu AS (
                SELECT u.id,lower(COALESCE(u.perfil,'')) perfil_legado,
                       lower(COALESCE(p.codigo,'')) perfil_codigo,lower(COALESCE(p.nome,'')) perfil_nome
                FROM usuarios u LEFT JOIN perfis_acesso p ON p.id=u.perfil_acesso_id
                WHERE u.id=@usuario AND u.tenant_id=@tenant AND u.ativo=true
            ), elegiveis AS (
                SELECT n.id FROM notificacoes n CROSS JOIN eu
                WHERE n.tenant_id=@tenant AND n.visivel_plataforma=true
                  AND (n.usuario_id=@usuario OR (n.usuario_id IS NULL AND (
                      n.destinatarios_perfis IS NULL OR btrim(n.destinatarios_perfis)='' OR
                      eu.perfil_legado = ANY(string_to_array(lower(replace(n.destinatarios_perfis,' ','')),',')) OR
                      eu.perfil_codigo = ANY(string_to_array(lower(replace(n.destinatarios_perfis,' ','')),',')) OR
                      eu.perfil_nome = ANY(string_to_array(lower(replace(n.destinatarios_perfis,' ','')),',') )
                  )))
            )
            INSERT INTO notificacoes_leitura(notificacao_id,usuario_id)
            SELECT id,@usuario FROM elegiveis
            ON CONFLICT(notificacao_id,usuario_id) DO UPDATE SET lida_em_utc=now();
            """;
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("usuario",usuarioId);await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Guid> SalvarDocumentoGovernancaAsync(Guid tenantId,Guid usuarioId,string tipo,string titulo,string? versao,string nomeArquivo,string mimeType,byte[] conteudo,DateOnly? validadeAte,CancellationToken ct=default)
    {
        if(conteudo.Length==0 || conteudo.Length>10*1024*1024) throw new InvalidOperationException("O documento deve ter entre 1 byte e 10 MB.");
        var hash=Convert.ToHexString(SHA256.HashData(conteudo)).ToLowerInvariant();
        const string sql="""
            INSERT INTO documentos_governanca(tenant_id,tipo,titulo,versao,nome_arquivo,mime_type,tamanho_bytes,sha256,conteudo,validade_ate,criado_por_usuario_id)
            VALUES(@tenant,@tipo,@titulo,@versao,@nome,@mime,@tamanho,@sha,@conteudo,@validade,@usuario) RETURNING id;
            """;
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("tipo",NormalizarTipoDocumento(tipo));cmd.Parameters.AddWithValue("titulo",string.IsNullOrWhiteSpace(titulo)?nomeArquivo:titulo.Trim());
        cmd.Parameters.Add("versao",NpgsqlDbType.Varchar).Value=(object?)Limpar(versao)??DBNull.Value;cmd.Parameters.AddWithValue("nome",nomeArquivo);cmd.Parameters.AddWithValue("mime",string.IsNullOrWhiteSpace(mimeType)?"application/octet-stream":mimeType);
        cmd.Parameters.AddWithValue("tamanho",(long)conteudo.Length);cmd.Parameters.AddWithValue("sha",hash);cmd.Parameters.AddWithValue("conteudo",conteudo);
        cmd.Parameters.Add("validade",NpgsqlDbType.Date).Value=(object?)validadeAte??DBNull.Value;cmd.Parameters.AddWithValue("usuario",usuarioId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct)??throw new InvalidOperationException("Não foi possível registrar o documento."));
    }

    public async Task<(string Nome,string Mime,byte[] Conteudo)?> ObterDocumentoAsync(Guid tenantId,Guid documentoId,CancellationToken ct=default)
    {
        const string sql="SELECT nome_arquivo,mime_type,conteudo FROM documentos_governanca WHERE tenant_id=@tenant AND id=@id";
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("id",documentoId);
        await using var leitor=await cmd.ExecuteReaderAsync(ct);if(!await leitor.ReadAsync(ct))return null;return (leitor.GetString(0),leitor.GetString(1),(byte[])leitor[2]);
    }

    public async Task<SolicitarAprovacaoResultado> SolicitarAprovacaoJuridicaAsync(Guid tenantId,Guid usuarioId,Guid? documentoId,string recurso,string? escopo,string emailJuridico,DateOnly validadeAte,CancellationToken ct=default)
    {
        recurso=NormalizarRecurso(recurso);
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var hash=HashSeguro.Sha256Texto(token);
        var expira=DateTimeOffset.UtcNow.AddDays(7);
        const string sql="""
            INSERT INTO solicitacoes_aprovacao_juridica(tenant_id,documento_id,recurso,escopo_descricao,email_juridico,token_hash,validade_solicitada_ate,criado_por_usuario_id,expira_em_utc)
            VALUES(@tenant,@documento,@recurso,@escopo,@email,@hash,@validade,@usuario,@expira) RETURNING id;
            """;
        Guid id;
        await using(var conexao=fabrica.Criar())
        {
            await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
            cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.Add("documento",NpgsqlDbType.Uuid).Value=(object?)documentoId??DBNull.Value;
            cmd.Parameters.AddWithValue("recurso",recurso);cmd.Parameters.Add("escopo",NpgsqlDbType.Text).Value=(object?)Limpar(escopo)??DBNull.Value;
            cmd.Parameters.AddWithValue("email",emailJuridico.Trim().ToLowerInvariant());cmd.Parameters.AddWithValue("hash",hash);cmd.Parameters.AddWithValue("validade",validadeAte);cmd.Parameters.AddWithValue("usuario",usuarioId);cmd.Parameters.AddWithValue("expira",expira);
            id=(Guid)(await cmd.ExecuteScalarAsync(ct)??throw new InvalidOperationException("Não foi possível criar a solicitação."));
        }
        var url=$"{configuracao.Servidor.UrlPublica.TrimEnd('/')}/governanca/aprovacao/{token}";
        await EnfileirarEmailAprovacaoAsync(tenantId,emailJuridico,recurso,url,validadeAte,ct);
        return new SolicitarAprovacaoResultado(id,token,expira);
    }

    public async Task<SolicitacaoAprovacaoPublica?> ObterSolicitacaoPublicaAsync(string token,CancellationToken ct=default)
    {
        var hash=HashSeguro.Sha256Texto(token);
        const string sql="""
            SELECT s.id,t.nome,s.recurso,s.escopo_descricao,s.email_juridico,d.titulo,d.sha256,s.validade_solicitada_ate,s.status,s.expira_em_utc
            FROM solicitacoes_aprovacao_juridica s
            JOIN tenants t ON t.id=s.tenant_id
            LEFT JOIN documentos_governanca d ON d.id=s.documento_id AND d.tenant_id=s.tenant_id
            WHERE s.token_hash=@hash LIMIT 1;
            """;
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);cmd.Parameters.AddWithValue("hash",hash);
        await using var leitor=await cmd.ExecuteReaderAsync(ct);if(!await leitor.ReadAsync(ct))return null;
        return new SolicitacaoAprovacaoPublica(leitor.GetGuid(0),leitor.GetString(1),leitor.GetString(2),leitor.IsDBNull(3)?null:leitor.GetString(3),leitor.GetString(4),leitor.IsDBNull(5)?null:leitor.GetString(5),leitor.IsDBNull(6)?null:leitor.GetString(6),leitor.GetFieldValue<DateOnly>(7),leitor.GetString(8),ComoDataHora(leitor,9));
    }

    public async Task RegistrarDecisaoJuridicaAsync(string token,string decisao,string nome,string cargo,CancellationToken ct=default)
    {
        var hash=HashSeguro.Sha256Texto(token);
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var transacao=await conexao.BeginTransactionAsync(ct);
        Guid id;Guid tenant;string recurso;string? escopo;string email;DateOnly validade;string status;DateTimeOffset expira;
        const string sel="SELECT id,tenant_id,recurso,escopo_descricao,email_juridico,validade_solicitada_ate,status,expira_em_utc FROM solicitacoes_aprovacao_juridica WHERE token_hash=@hash FOR UPDATE";
        await using(var cmd=new NpgsqlCommand(sel,conexao,transacao))
        {
            cmd.Parameters.AddWithValue("hash",hash);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new InvalidOperationException("Solicitação inválida.");
            id=r.GetGuid(0);tenant=r.GetGuid(1);recurso=r.GetString(2);escopo=r.IsDBNull(3)?null:r.GetString(3);email=r.GetString(4);validade=r.GetFieldValue<DateOnly>(5);status=r.GetString(6);expira=ComoDataHora(r,7);
        }
        if(status!="Pendente")throw new InvalidOperationException("Esta solicitação já foi decidida.");
        if(expira<DateTimeOffset.UtcNow)throw new InvalidOperationException("A solicitação expirou.");
        var aprovar=string.Equals(decisao,"Aprovar",StringComparison.OrdinalIgnoreCase);
        await using(var cmd=new NpgsqlCommand("UPDATE solicitacoes_aprovacao_juridica SET status=@status,aprovador_nome=@nome,aprovador_cargo=@cargo,decisao_em_utc=now() WHERE id=@id",conexao,transacao))
        {cmd.Parameters.AddWithValue("status",aprovar?"Aprovada":"Rejeitada");cmd.Parameters.AddWithValue("nome",nome.Trim());cmd.Parameters.AddWithValue("cargo",cargo.Trim());cmd.Parameters.AddWithValue("id",id);await cmd.ExecuteNonQueryAsync(ct);}
        if(aprovar)
        {
            await using var cmd=new NpgsqlCommand("INSERT INTO autorizacoes_governanca(tenant_id,solicitacao_id,recurso,escopo_descricao,aprovada_em_utc,validade_ate,aprovador_email,aprovador_nome,aprovador_cargo) VALUES(@tenant,@solicitacao,@recurso,@escopo,now(),@validade,@email,@nome,@cargo)",conexao,transacao);
            cmd.Parameters.AddWithValue("tenant",tenant);cmd.Parameters.AddWithValue("solicitacao",id);cmd.Parameters.AddWithValue("recurso",recurso);cmd.Parameters.Add("escopo",NpgsqlDbType.Text).Value=(object?)escopo??DBNull.Value;cmd.Parameters.AddWithValue("validade",validade);cmd.Parameters.AddWithValue("email",email);cmd.Parameters.AddWithValue("nome",nome.Trim());cmd.Parameters.AddWithValue("cargo",cargo.Trim());await cmd.ExecuteNonQueryAsync(ct);
        }
        await transacao.CommitAsync(ct);
    }

    public async Task RegistrarCienciaAsync(Guid tenantId,Guid usuarioId,Guid documentoId,string colaboradorNome,string? colaboradorEmail,string forma,string? observacao,CancellationToken ct=default)
    {
        const string sql="""
            INSERT INTO ciencias_politica(tenant_id,documento_id,colaborador_nome,colaborador_email,forma_ciencia,registrado_por_usuario_id,observacao)
            SELECT @tenant,d.id,@nome,@email,@forma,@usuario,@observacao FROM documentos_governanca d WHERE d.id=@documento AND d.tenant_id=@tenant;
            """;
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("documento",documentoId);cmd.Parameters.AddWithValue("nome",colaboradorNome.Trim());cmd.Parameters.Add("email",NpgsqlDbType.Varchar).Value=(object?)Limpar(colaboradorEmail)??DBNull.Value;cmd.Parameters.AddWithValue("forma",string.IsNullOrWhiteSpace(forma)?"RegistroAdministrativo":forma.Trim());cmd.Parameters.AddWithValue("usuario",usuarioId);cmd.Parameters.Add("observacao",NpgsqlDbType.Text).Value=(object?)Limpar(observacao)??DBNull.Value;
        if(await cmd.ExecuteNonQueryAsync(ct)==0)throw new InvalidOperationException("Documento não encontrado.");
    }

    public async Task<ConfiguracaoEmailTenant> ObterEmailTenantAsync(Guid tenantId,CancellationToken ct=default)
    {
        const string garantir="INSERT INTO configuracoes_email_tenant(tenant_id) VALUES(@tenant) ON CONFLICT(tenant_id) DO NOTHING";
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using(var g=new NpgsqlCommand(garantir,conexao)){g.Parameters.AddWithValue("tenant",tenantId);await g.ExecuteNonQueryAsync(ct);}await using var cmd=new NpgsqlCommand("SELECT remetente_nome,remetente_email,reply_to,destinatarios_adicionais FROM configuracoes_email_tenant WHERE tenant_id=@tenant",conexao);cmd.Parameters.AddWithValue("tenant",tenantId);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new InvalidOperationException("Configuração de e-mail não encontrada.");return new ConfiguracaoEmailTenant(r.GetString(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(3)?null:r.GetString(3));
    }

    public async Task SalvarEmailTenantAsync(Guid tenantId,string? replyTo,string? destinatariosAdicionais,CancellationToken ct=default)
    {
        const string sql="""
            INSERT INTO configuracoes_email_tenant(tenant_id,remetente_nome,remetente_email,reply_to,destinatarios_adicionais,atualizado_em_utc)
            VALUES(@tenant,'Share IT .Clara','monitoramento.clara@shareitbrasil.com.br',@reply,@dest,now())
            ON CONFLICT(tenant_id) DO UPDATE SET reply_to=EXCLUDED.reply_to,destinatarios_adicionais=EXCLUDED.destinatarios_adicionais,atualizado_em_utc=now();
            """;
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.Add("reply",NpgsqlDbType.Varchar).Value=(object?)Limpar(replyTo)??DBNull.Value;cmd.Parameters.Add("dest",NpgsqlDbType.Text).Value=(object?)Limpar(destinatariosAdicionais)??DBNull.Value;await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task EnfileirarEmailTesteAsync(Guid tenantId,string destinatario,CancellationToken ct=default)
    {
        destinatario=(destinatario??string.Empty).Trim().ToLowerInvariant();
        if(string.IsNullOrWhiteSpace(destinatario) || !destinatario.Contains('@'))
            throw new ArgumentException("Informe um e-mail válido para o teste.");
        const string sql="""
            INSERT INTO fila_emails(tenant_id,destinatario,assunto,corpo_texto)
            VALUES(@tenant,@dest,'Teste de e-mail — Share IT .Clara',
                   'Este é um teste do canal oficial de e-mail da Share IT .Clara. Se você recebeu esta mensagem, a fila e o transporte SMTP configurado estão funcionando.');
            """;
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand(sql,conexao);
        cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("dest",destinatario);await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<PainelGovernanca> ObterPainelGovernancaAsync(Guid tenantId,CancellationToken ct=default)
    {
        var documentos=new List<DocumentoGovernancaResumo>();var solicitacoes=new List<SolicitacaoJuridicaResumo>();var autorizacoes=new List<AutorizacaoGovernancaResumo>();var ciencias=new List<CienciaPoliticaResumo>();
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);
        await using(var cmd=new NpgsqlCommand("SELECT id,tipo,titulo,versao,nome_arquivo,tamanho_bytes,sha256,validade_ate,status_validacao,criado_em_utc FROM documentos_governanca WHERE tenant_id=@tenant ORDER BY criado_em_utc DESC",conexao))
        {cmd.Parameters.AddWithValue("tenant",tenantId);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))documentos.Add(new DocumentoGovernancaResumo(r.GetGuid(0),r.GetString(1),r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),r.GetString(4),r.GetInt64(5),r.GetString(6),r.IsDBNull(7)?null:r.GetFieldValue<DateOnly>(7),r.GetString(8),ComoDataHora(r,9)));}
        await using(var cmd=new NpgsqlCommand("SELECT id,documento_id,recurso,escopo_descricao,email_juridico,validade_solicitada_ate,status,aprovador_nome,aprovador_cargo,criado_em_utc,expira_em_utc,decisao_em_utc FROM solicitacoes_aprovacao_juridica WHERE tenant_id=@tenant ORDER BY criado_em_utc DESC",conexao))
        {cmd.Parameters.AddWithValue("tenant",tenantId);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))solicitacoes.Add(new SolicitacaoJuridicaResumo(r.GetGuid(0),r.IsDBNull(1)?null:r.GetGuid(1),r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),r.GetString(4),r.GetFieldValue<DateOnly>(5),r.GetString(6),r.IsDBNull(7)?null:r.GetString(7),r.IsDBNull(8)?null:r.GetString(8),ComoDataHora(r,9),ComoDataHora(r,10),r.IsDBNull(11)?null:ComoDataHora(r,11)));}
        await using(var cmd=new NpgsqlCommand("SELECT id,recurso,escopo_descricao,aprovada_em_utc,validade_ate,aprovador_email,aprovador_nome,aprovador_cargo,revogada_em_utc FROM autorizacoes_governanca WHERE tenant_id=@tenant ORDER BY aprovada_em_utc DESC",conexao))
        {cmd.Parameters.AddWithValue("tenant",tenantId);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))autorizacoes.Add(new AutorizacaoGovernancaResumo(r.GetGuid(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),ComoDataHora(r,3),r.GetFieldValue<DateOnly>(4),r.GetString(5),r.IsDBNull(6)?null:r.GetString(6),r.IsDBNull(7)?null:r.GetString(7),r.IsDBNull(8)?null:ComoDataHora(r,8)));}
        await using(var cmd=new NpgsqlCommand("SELECT c.id,c.documento_id,d.titulo,c.colaborador_nome,c.colaborador_email,c.forma_ciencia,c.ciencia_em_utc,c.observacao FROM ciencias_politica c JOIN documentos_governanca d ON d.id=c.documento_id WHERE c.tenant_id=@tenant ORDER BY c.ciencia_em_utc DESC LIMIT 200",conexao))
        {cmd.Parameters.AddWithValue("tenant",tenantId);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))ciencias.Add(new CienciaPoliticaResumo(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),r.GetString(5),ComoDataHora(r,6),r.IsDBNull(7)?null:r.GetString(7)));}
        var email=await ObterEmailTenantAsync(tenantId,ct);
        var hoje=DateOnly.FromDateTime(DateTime.UtcNow);
        var status=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var recurso in new[]{"Screenshot","VisualizacaoAoVivo","GravacaoTela","Audio","Webcam"})
        {
            var a=autorizacoes.FirstOrDefault(x=>x.Recurso==recurso && x.RevogadaEmUtc is null && x.ValidadeAte>=hoje);
            status[recurso]=a is not null?"Autorização jurídica vigente":solicitacoes.Any(x=>x.Recurso==recurso && x.Status=="Pendente" && x.ExpiraEmUtc>DateTimeOffset.UtcNow)?"Aguardando aprovação jurídica":"Bloqueado";
        }
        return new PainelGovernanca(documentos,solicitacoes,autorizacoes,ciencias,email,status);
    }

    private async Task EnfileirarEmailAprovacaoAsync(Guid tenantId,string email,string recurso,string url,DateOnly validade,CancellationToken ct)
    {
        var assunto=$"Aprovação jurídica — {recurso} — Share IT .Clara";
        var corpo=$"Foi solicitada autorização para o recurso {recurso} na Share IT .Clara.\n\nValidade solicitada: {validade:dd/MM/yyyy}\nAprovar ou rejeitar: {url}\n\nA aprovação registra data, e-mail e trilha de auditoria. O endereço do link é confidencial.";
        await using var conexao=fabrica.Criar();await conexao.OpenAsync(ct);await using var cmd=new NpgsqlCommand("INSERT INTO fila_emails(tenant_id,destinatario,assunto,corpo_texto) VALUES(@tenant,@dest,@assunto,@corpo)",conexao);cmd.Parameters.AddWithValue("tenant",tenantId);cmd.Parameters.AddWithValue("dest",email.Trim().ToLowerInvariant());cmd.Parameters.AddWithValue("assunto",assunto);cmd.Parameters.AddWithValue("corpo",corpo);await cmd.ExecuteNonQueryAsync(ct);
    }

    private static IEnumerable<string> SepararEmails(string? valor)
        => (valor??string.Empty).Split(new[]{',',';','\n'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(x=>x.Contains('@')).Select(x=>x.ToLowerInvariant()).Distinct();
    private static string NormalizarHorario(string? valor,string padrao)=>TimeSpan.TryParse(valor,out var h)&&h>=TimeSpan.Zero&&h<TimeSpan.FromDays(1)?h.ToString(@"hh\:mm"):padrao;
    private static string NormalizarDias(string? valor){var d=(valor??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(x=>int.TryParse(x,out var n)?n:0).Where(x=>x is>=1 and<=7).Distinct().OrderBy(x=>x).ToArray();return d.Length==0?"1,2,3,4,5":string.Join(',',d);}
    private static string NormalizarTipoDocumento(string tipo)=>tipo is "PoliticaMonitoramento" or "AutorizacaoJuridica" or "RIPD" or "ComprovanteCiencia" or "Outro"?tipo:"Outro";
    private static string NormalizarRecurso(string recurso)=>recurso is "Screenshot" or "VisualizacaoAoVivo" or "GravacaoTela" or "Audio" or "Webcam"?recurso:throw new ArgumentException("Recurso inválido.");
    private static string FormatarTempo(long segundos){var t=TimeSpan.FromSeconds(Math.Max(0,segundos));return t.TotalHours>=1?$"{(int)t.TotalHours}h {t.Minutes:00}m":$"{t.Minutes}m";}
    private static DateTimeOffset ComoDataHora(NpgsqlDataReader leitor,int indice)=>new(DateTime.SpecifyKind(leitor.GetDateTime(indice),DateTimeKind.Utc));
    private static string? Limpar(string? valor)=>string.IsNullOrWhiteSpace(valor)?null:valor.Trim();
}
