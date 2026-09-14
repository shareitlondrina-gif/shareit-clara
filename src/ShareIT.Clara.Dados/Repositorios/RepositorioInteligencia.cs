using Npgsql;
using NpgsqlTypes;
using ShareIT.Clara.Dados.Banco;
using ShareIT.Clara.Dominio.Contratos;
using ShareIT.Clara.Dominio.Enumeracoes;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: concentrar leituras gerenciais, oportunidades, evidências, configurações e drill-down do Share IT .Clara.
/// Origem: telemetria autorizada do tenant e parâmetros persistidos no PostgreSQL.
/// Destino: dashboards, detalhe da estação, políticas do agente e telas de inteligência.
/// Dependências: PostgreSQL / shareit_clara.
/// Impacto: mantém fatos observáveis separados das recomendações e evita lógica sensível apenas na camada visual.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioInteligencia(FabricaConexoesClara fabrica)
{
    public async Task<PainelOportunidades> ObterOportunidadesAsync(
        Guid tenantId,
        int dias = 7,
        string? categoria = null,
        string? pessoa = null,
        string? impacto = null,
        CancellationToken ct = default)
    {
        dias = Math.Clamp(dias, 1, 365);
        var oportunidades = await ListarOportunidadesAsync(tenantId, dias, categoria, pessoa, impacto, 500, ct);
        var indicadores = await ObterIndicadoresOportunidadesAsync(tenantId, dias, ct);
        return new PainelOportunidades(
            oportunidades,
            indicadores,
            oportunidades.Count,
            oportunidades.Count(x => x.Status == "Pendente"),
            oportunidades.Count(x => x.Status == "EmAndamento"),
            oportunidades.Count(x => x.Status == "Concluida"));
    }

    public async Task<IReadOnlyCollection<OportunidadeResumo>> ListarOportunidadesAsync(
        Guid tenantId,
        int dias,
        string? categoria,
        string? pessoa,
        string? impacto,
        int limite,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT o.id,o.ativo_id,at.hostname,o.pessoa,o.equipe,o.categoria,o.evidencia_resumida,
                   o.impacto,o.recomendacao,o.status,o.detectada_em_utc
            FROM oportunidades o
            LEFT JOIN ativos at ON at.id=o.ativo_id AND at.tenant_id=o.tenant_id
            WHERE o.tenant_id=@tenant
              AND o.detectada_em_utc >= now() - (@dias * interval '1 day')
              AND (@categoria='' OR o.categoria=@categoria)
              AND (@pessoa='' OR lower(COALESCE(o.pessoa,'')) LIKE '%' || lower(@pessoa) || '%')
              AND (@impacto='' OR o.impacto=@impacto)
            ORDER BY o.detectada_em_utc DESC
            LIMIT @limite;
            """;
        var lista = new List<OportunidadeResumo>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("dias", Math.Clamp(dias, 1, 3650));
        comando.Parameters.AddWithValue("categoria", Limpar(categoria) ?? string.Empty);
        comando.Parameters.AddWithValue("pessoa", Limpar(pessoa) ?? string.Empty);
        comando.Parameters.AddWithValue("impacto", NormalizarImpactoFiltro(impacto));
        comando.Parameters.AddWithValue("limite", Math.Clamp(limite, 1, 2000));
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
        {
            lista.Add(new OportunidadeResumo(
                leitor.GetGuid(0),
                leitor.IsDBNull(1) ? null : leitor.GetGuid(1),
                leitor.IsDBNull(2) ? null : leitor.GetString(2),
                leitor.IsDBNull(3) ? null : leitor.GetString(3),
                leitor.IsDBNull(4) ? null : leitor.GetString(4),
                leitor.GetString(5),
                leitor.GetString(6),
                leitor.GetString(7),
                leitor.GetString(8),
                leitor.GetString(9),
                ComoDataHora(leitor, 10)));
        }
        return lista;
    }

    public async Task AtualizarStatusOportunidadeAsync(Guid tenantId, Guid oportunidadeId, string status, CancellationToken ct = default)
    {
        status = status switch
        {
            "EmAndamento" => "EmAndamento",
            "Concluida" => "Concluida",
            _ => "Pendente"
        };
        const string sql = "UPDATE oportunidades SET status=@status,atualizada_em_utc=now() WHERE id=@id AND tenant_id=@tenant";
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("id", oportunidadeId);
        comando.Parameters.AddWithValue("status", status);
        await comando.ExecuteNonQueryAsync(ct);
    }

    public async Task<PainelEvidencias> ObterEvidenciasAsync(
        Guid tenantId,
        int dias = 1,
        string? pessoa = null,
        string? categoria = null,
        string? gravidade = null,
        CancellationToken ct = default)
    {
        dias = Math.Clamp(dias, 1, 365);
        var evidencias = await ListarEvidenciasAsync(tenantId, dias, pessoa, categoria, gravidade, 500, ct);
        var evidenciasVisuais = await ListarEvidenciasVisuaisAsync(tenantId, dias, null, 24, ct);
        var metricas = await ObterMetricasEvidenciasAsync(tenantId, ct);
        return new PainelEvidencias(
            evidencias,
            evidenciasVisuais,
            metricas.Produtividade,
            metricas.PeriodosOciosos,
            metricas.AcessosNaoProdutivos,
            metricas.UsoSistemaPrincipal,
            metricas.SegundosReuniao);
    }

    public async Task<IReadOnlyCollection<EvidenciaResumo>> ListarEvidenciasAsync(
        Guid tenantId,
        int dias,
        string? pessoa,
        string? categoria,
        string? gravidade,
        int limite,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT e.id,e.ativo_id,at.hostname,e.pessoa,e.equipe,e.categoria,e.gravidade,e.resumo,e.detalhe,e.fonte,e.observada_em_utc
            FROM evidencias_contextuais e
            LEFT JOIN ativos at ON at.id=e.ativo_id AND at.tenant_id=e.tenant_id
            WHERE e.tenant_id=@tenant
              AND e.observada_em_utc >= now() - (@dias * interval '1 day')
              AND (@pessoa='' OR lower(COALESCE(e.pessoa,e.equipe,'')) LIKE '%' || lower(@pessoa) || '%')
              AND (@categoria='' OR e.categoria=@categoria)
              AND (@gravidade='' OR e.gravidade=@gravidade)
            ORDER BY e.observada_em_utc DESC
            LIMIT @limite;
            """;
        var lista = new List<EvidenciaResumo>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("dias", Math.Clamp(dias, 1, 3650));
        comando.Parameters.AddWithValue("pessoa", Limpar(pessoa) ?? string.Empty);
        comando.Parameters.AddWithValue("categoria", Limpar(categoria) ?? string.Empty);
        comando.Parameters.AddWithValue("gravidade", NormalizarGravidadeFiltro(gravidade));
        comando.Parameters.AddWithValue("limite", Math.Clamp(limite, 1, 2000));
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
        {
            lista.Add(new EvidenciaResumo(
                leitor.GetGuid(0),
                leitor.IsDBNull(1) ? null : leitor.GetGuid(1),
                leitor.IsDBNull(2) ? null : leitor.GetString(2),
                leitor.IsDBNull(3) ? null : leitor.GetString(3),
                leitor.IsDBNull(4) ? null : leitor.GetString(4),
                leitor.GetString(5),
                leitor.GetString(6),
                leitor.GetString(7),
                leitor.IsDBNull(8) ? null : leitor.GetString(8),
                leitor.IsDBNull(9) ? null : leitor.GetString(9),
                ComoDataHora(leitor, 10)));
        }
        return lista;
    }

    public async Task<IReadOnlyCollection<EvidenciaVisualResumo>> ListarEvidenciasVisuaisAsync(
        Guid tenantId,
        int dias,
        Guid? ativoId = null,
        int limite = 24,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT v.id,v.ativo_id,at.hostname,v.pessoa,v.tipo,v.titulo,v.descricao,v.arquivo_relativo,
                   v.capturada_em_utc,v.demonstrativa
            FROM evidencias_visuais v
            LEFT JOIN ativos at ON at.id=v.ativo_id AND at.tenant_id=v.tenant_id
            WHERE v.tenant_id=@tenant
              AND v.capturada_em_utc >= now() - (@dias * interval '1 day')
              AND (@ativo IS NULL OR v.ativo_id=@ativo)
            ORDER BY v.capturada_em_utc DESC
            LIMIT @limite;
            """;
        var lista = new List<EvidenciaVisualResumo>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("dias", Math.Clamp(dias, 1, 3650));
        comando.Parameters.AddWithValue("ativo", NpgsqlDbType.Uuid, (object?)ativoId ?? DBNull.Value);
        comando.Parameters.AddWithValue("limite", Math.Clamp(limite, 1, 200));
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
        {
            lista.Add(new EvidenciaVisualResumo(
                leitor.GetGuid(0),
                leitor.IsDBNull(1) ? null : leitor.GetGuid(1),
                leitor.IsDBNull(2) ? null : leitor.GetString(2),
                leitor.IsDBNull(3) ? null : leitor.GetString(3),
                leitor.GetString(4),
                leitor.GetString(5),
                leitor.IsDBNull(6) ? null : leitor.GetString(6),
                NormalizarUrlEvidencia(leitor.GetString(7)),
                ComoDataHora(leitor, 8),
                leitor.GetBoolean(9)));
        }
        return lista;
    }

    public async Task<PainelLocais> ObterPainelLocaisAsync(Guid tenantId, CancellationToken ct = default)
    {
        var resumos = await ObterResumosLocaisAsync(tenantId, ct);
        var regras = await ListarRegrasLocaisAsync(tenantId, ct);
        var estacoes = await ListarEstacoesLocaisAsync(tenantId, ct);
        var oportunidades = await ListarOportunidadesAsync(tenantId, 30, null, null, null, 8, ct);
        oportunidades = oportunidades
            .Where(x => x.Categoria.Contains("Home", StringComparison.OrdinalIgnoreCase)
                     || x.Categoria.Contains("Local", StringComparison.OrdinalIgnoreCase)
                     || x.EvidenciaResumida.Contains("escrit", StringComparison.OrdinalIgnoreCase)
                     || x.EvidenciaResumida.Contains("home", StringComparison.OrdinalIgnoreCase)
                     || x.EvidenciaResumida.Contains("extern", StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .ToArray();

        var escritorio = resumos.FirstOrDefault(x => x.Codigo == "escritorio");
        var home = resumos.FirstOrDefault(x => x.Codigo == "home");
        var insight = "A comparação por local ficará mais precisa conforme as estações acumularem dados suficientes.";
        if (escritorio is not null && home is not null && escritorio.Produtividade > 0 && home.Produtividade > 0)
        {
            var produtividade = (int)Math.Round((escritorio.Produtividade - home.Produtividade) * 100d / Math.Max(1, home.Produtividade));
            var ociosidade = home.Ociosidade > 0
                ? (int)Math.Round((home.Ociosidade - escritorio.Ociosidade) * 100d / Math.Max(1, home.Ociosidade))
                : 0;
            insight = produtividade >= 0
                ? $"Estações no escritório apresentaram {produtividade}% mais produtividade e {Math.Max(0, ociosidade)}% menos ociosidade que no home office."
                : $"Estações em home office apresentaram {Math.Abs(produtividade)}% mais produtividade no período analisado.";
        }

        return new PainelLocais(resumos, regras, estacoes, oportunidades, insight);
    }

    public async Task<ConfiguracoesMonitoramento> ObterConfiguracoesAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string garantir = "INSERT INTO configuracoes_monitoramento(tenant_id) VALUES(@tenant) ON CONFLICT(tenant_id) DO NOTHING";
        const string sql = """
            SELECT permitir_classificacao_web,monitorar_urls_dominios,usar_categorias_web,
                   tempo_minimo_ociosidade_minutos,baixa_continuidade_minutos,limiar_alternancia_aplicativos,
                   redes_confiaveis_cidr,considerar_vpn_escritorio,classificar_ip_publico,
                   captura_screenshots,intervalo_captura_minutos,retencao_evidencias_dias,anonimizacao_parcial,
                   horario_inicio,horario_fim,dias_monitorados,intervalo_heartbeat_segundos,tempo_offline_minutos,
                   alerta_estacao_offline,notificar_por_email,destinatarios_alertas,atualizado_em_utc
            FROM configuracoes_monitoramento WHERE tenant_id=@tenant;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using (var cmd = new NpgsqlCommand(garantir, conexao))
        {
            cmd.Parameters.AddWithValue("tenant", tenantId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct)) throw new InvalidOperationException("Configuração de monitoramento não encontrada.");
        return LerConfiguracoes(leitor);
    }

    public async Task SalvarConfiguracoesAsync(Guid tenantId, ConfiguracoesMonitoramento configuracoes, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO configuracoes_monitoramento(
                tenant_id,permitir_classificacao_web,monitorar_urls_dominios,usar_categorias_web,
                tempo_minimo_ociosidade_minutos,baixa_continuidade_minutos,limiar_alternancia_aplicativos,
                redes_confiaveis_cidr,considerar_vpn_escritorio,classificar_ip_publico,
                captura_screenshots,intervalo_captura_minutos,retencao_evidencias_dias,anonimizacao_parcial,
                horario_inicio,horario_fim,dias_monitorados,intervalo_heartbeat_segundos,tempo_offline_minutos,
                alerta_estacao_offline,notificar_por_email,destinatarios_alertas,atualizado_em_utc)
            VALUES(
                @tenant,@classificacaoWeb,@urls,@categorias,@ociosidade,@continuidade,@alternancia,
                @redes,@vpn,@ipPublico,@screenshots,@captura,@retencao,@anonimizacao,
                CAST(@inicio AS time),CAST(@fim AS time),@dias,@heartbeat,@offline,@alertaOffline,@email,@destinatarios,now())
            ON CONFLICT(tenant_id) DO UPDATE SET
                permitir_classificacao_web=EXCLUDED.permitir_classificacao_web,
                monitorar_urls_dominios=EXCLUDED.monitorar_urls_dominios,
                usar_categorias_web=EXCLUDED.usar_categorias_web,
                tempo_minimo_ociosidade_minutos=EXCLUDED.tempo_minimo_ociosidade_minutos,
                baixa_continuidade_minutos=EXCLUDED.baixa_continuidade_minutos,
                limiar_alternancia_aplicativos=EXCLUDED.limiar_alternancia_aplicativos,
                redes_confiaveis_cidr=EXCLUDED.redes_confiaveis_cidr,
                considerar_vpn_escritorio=EXCLUDED.considerar_vpn_escritorio,
                classificar_ip_publico=EXCLUDED.classificar_ip_publico,
                captura_screenshots=EXCLUDED.captura_screenshots,
                intervalo_captura_minutos=EXCLUDED.intervalo_captura_minutos,
                retencao_evidencias_dias=EXCLUDED.retencao_evidencias_dias,
                anonimizacao_parcial=EXCLUDED.anonimizacao_parcial,
                horario_inicio=EXCLUDED.horario_inicio,
                horario_fim=EXCLUDED.horario_fim,
                dias_monitorados=EXCLUDED.dias_monitorados,
                intervalo_heartbeat_segundos=EXCLUDED.intervalo_heartbeat_segundos,
                tempo_offline_minutos=EXCLUDED.tempo_offline_minutos,
                alerta_estacao_offline=EXCLUDED.alerta_estacao_offline,
                notificar_por_email=EXCLUDED.notificar_por_email,
                destinatarios_alertas=EXCLUDED.destinatarios_alertas,
                atualizado_em_utc=now();
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("classificacaoWeb", configuracoes.PermitirClassificacaoWeb);
        comando.Parameters.AddWithValue("urls", configuracoes.MonitorarUrlsDominios);
        comando.Parameters.AddWithValue("categorias", configuracoes.UsarCategoriasWeb);
        comando.Parameters.AddWithValue("ociosidade", Math.Clamp(configuracoes.TempoMinimoOciosidadeMinutos, 1, 240));
        comando.Parameters.AddWithValue("continuidade", Math.Clamp(configuracoes.BaixaContinuidadeMinutos, 1, 240));
        comando.Parameters.AddWithValue("alternancia", Math.Clamp(configuracoes.LimiarAlternanciaAplicativos, 1, 500));
        comando.Parameters.Add("redes", NpgsqlDbType.Text).Value = (object?)Limpar(configuracoes.RedesConfiaveisCidr) ?? DBNull.Value;
        comando.Parameters.AddWithValue("vpn", configuracoes.ConsiderarVpnEscritorio);
        comando.Parameters.AddWithValue("ipPublico", configuracoes.ClassificarIpPublico);
        comando.Parameters.AddWithValue("screenshots", configuracoes.CapturaScreenshots);
        comando.Parameters.AddWithValue("captura", Math.Clamp(configuracoes.IntervaloCapturaMinutos, 1, 1440));
        comando.Parameters.AddWithValue("retencao", Math.Clamp(configuracoes.RetencaoEvidenciasDias, 1, 3650));
        comando.Parameters.AddWithValue("anonimizacao", configuracoes.AnonimizacaoParcial);
        comando.Parameters.AddWithValue("inicio", NormalizarHorario(configuracoes.HorarioInicio, "08:00"));
        comando.Parameters.AddWithValue("fim", NormalizarHorario(configuracoes.HorarioFim, "18:00"));
        comando.Parameters.AddWithValue("dias", NormalizarDias(configuracoes.DiasMonitorados));
        comando.Parameters.AddWithValue("heartbeat", Math.Clamp(configuracoes.IntervaloHeartbeatSegundos, 15, 3600));
        comando.Parameters.AddWithValue("offline", Math.Clamp(configuracoes.TempoOfflineMinutos, 1, 1440));
        comando.Parameters.AddWithValue("alertaOffline", configuracoes.AlertaEstacaoOffline);
        comando.Parameters.AddWithValue("email", configuracoes.NotificarPorEmail);
        comando.Parameters.Add("destinatarios", NpgsqlDbType.Text).Value = (object?)Limpar(configuracoes.DestinatariosAlertas) ?? DBNull.Value;
        await comando.ExecuteNonQueryAsync(ct);
    }

    public async Task RestaurarConfiguracoesPadraoAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM configuracoes_monitoramento WHERE tenant_id=@tenant; INSERT INTO configuracoes_monitoramento(tenant_id) VALUES(@tenant);";
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await comando.ExecuteNonQueryAsync(ct);
    }

    public async Task<PoliticaMonitoramentoAgente> ObterPoliticaAgenteAsync(Guid tenantId, CancellationToken ct = default)
    {
        var c = await ObterConfiguracoesAsync(tenantId, ct);
        return new PoliticaMonitoramentoAgente(
            c.IntervaloHeartbeatSegundos,
            c.TempoOfflineMinutos,
            c.TempoMinimoOciosidadeMinutos,
            c.HorarioInicio,
            c.HorarioFim,
            c.DiasMonitorados,
            c.PermitirClassificacaoWeb,
            c.MonitorarUrlsDominios,
            c.UsarCategoriasWeb,
            c.CapturaScreenshots,
            c.IntervaloCapturaMinutos,
            c.RetencaoEvidenciasDias,
            c.AnonimizacaoParcial,
            c.AtualizadoEmUtc);
    }

    public async Task<ContextoGerencialEstacao?> ObterContextoEstacaoAsync(Guid tenantId, Guid ativoId, CancellationToken ct = default)
    {
        const string cabecalho = """
            SELECT a.id,u.nome,d.nome,
                   CASE WHEN a.id IS NULL THEN 'Sem agente'
                        WHEN a.ultima_comunicacao_utc >= now() - (COALESCE(cm.tempo_offline_minutos,15) * interval '1 minute') THEN 'Online'
                        ELSE 'Offline' END AS status,
                   CASE WHEN a.id IS NULL OR a.ultima_comunicacao_utc < now() - (COALESCE(cm.tempo_offline_minutos,15) * interval '1 minute') THEN 'Offline'
                        WHEN p.estado=1 THEN 'Ativo' WHEN p.estado=2 THEN 'Ocioso' WHEN p.estado=3 THEN 'Bloqueado'
                        WHEN p.estado=5 THEN 'Reunião' ELSE 'Online' END AS estado,
                   COALESCE(a.processo_atual,p.processo_executavel),
                   CASE WHEN loc.tipo='Escritorio' THEN 'Escritório'
                        WHEN loc.tipo='HomeOffice' THEN 'Home office'
                        WHEN loc.tipo='Externo' THEN 'Externo/Remoto'
                        WHEN loc.tipo='Outro' THEN COALESCE(loc.nome,'Outro')
                        WHEN a.endereco_ip_publico IS NULL THEN 'Sem classificação'
                        ELSE 'Sem classificação' END AS local_trabalho
            FROM ativos at
            LEFT JOIN agentes a ON a.ativo_id=at.id AND a.tenant_id=at.tenant_id AND a.ativo=true
            LEFT JOIN unidades u ON u.id=at.unidade_id AND u.tenant_id=at.tenant_id
            LEFT JOIN departamentos d ON d.id=at.departamento_id AND d.tenant_id=at.tenant_id
            LEFT JOIN configuracoes_monitoramento cm ON cm.tenant_id=at.tenant_id
            LEFT JOIN LATERAL (
                SELECT estado,processo_executavel FROM periodos_atividade p
                WHERE p.tenant_id=at.tenant_id AND p.ativo_id=at.id
                ORDER BY p.fim_utc DESC LIMIT 1
            ) p ON true
            LEFT JOIN LATERAL (
                SELECT r.tipo,r.nome FROM regras_local_trabalho r
                WHERE r.tenant_id=at.tenant_id AND r.ativo=true AND a.endereco_ip_publico IS NOT NULL
                  AND a.endereco_ip_publico <<= r.faixa_ip
                ORDER BY masklen(r.faixa_ip) DESC LIMIT 1
            ) loc ON true
            WHERE at.tenant_id=@tenant AND at.id=@ativo AND at.ativo=true
            LIMIT 1;
            """;

        Guid? agenteId;
        string? unidade;
        string? departamento;
        string status;
        string estado;
        string? processo;
        string local;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using (var comando = new NpgsqlCommand(cabecalho, conexao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("ativo", ativoId);
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            if (!await leitor.ReadAsync(ct)) return null;
            agenteId = leitor.IsDBNull(0) ? null : leitor.GetGuid(0);
            unidade = leitor.IsDBNull(1) ? null : leitor.GetString(1);
            departamento = leitor.IsDBNull(2) ? null : leitor.GetString(2);
            status = leitor.GetString(3);
            estado = leitor.GetString(4);
            processo = leitor.IsDBNull(5) ? null : leitor.GetString(5);
            local = leitor.GetString(6);
        }

        var inicioHoje = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        long produtivo = 0;
        long ocioso = 0;
        long observado = 0;
        const string tempos = """
            SELECT
                COALESCE(SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))) FILTER (WHERE p.estado IN (1,5)),0)::bigint,
                COALESCE(SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))) FILTER (WHERE p.estado=2),0)::bigint,
                COALESCE(SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))),0)::bigint
            FROM periodos_atividade p
            WHERE p.tenant_id=@tenant AND p.ativo_id=@ativo AND p.inicio_utc>=@inicio;
            """;
        await using (var comando = new NpgsqlCommand(tempos, conexao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("ativo", ativoId);
            comando.Parameters.AddWithValue("inicio", inicioHoje);
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            if (await leitor.ReadAsync(ct))
            {
                produtivo = leitor.GetInt64(0);
                ocioso = leitor.GetInt64(1);
                observado = leitor.GetInt64(2);
            }
        }

        string? aplicativoPrincipal = null;
        const string app = """
            SELECT processo_executavel,SUM(EXTRACT(EPOCH FROM (fim_utc-inicio_utc)))::bigint segundos
            FROM periodos_atividade
            WHERE tenant_id=@tenant AND ativo_id=@ativo AND inicio_utc>=@inicio
              AND processo_executavel IS NOT NULL AND btrim(processo_executavel)<>''
            GROUP BY processo_executavel ORDER BY segundos DESC LIMIT 1;
            """;
        await using (var comando = new NpgsqlCommand(app, conexao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("ativo", ativoId);
            comando.Parameters.AddWithValue("inicio", inicioHoje);
            aplicativoPrincipal = Convert.ToString(await comando.ExecuteScalarAsync(ct));
        }

        var timeline = new List<LinhaTimeline>();
        const string timelineSql = """
            SELECT inicio_utc,fim_utc,estado,processo_executavel,usuario_windows
            FROM periodos_atividade
            WHERE tenant_id=@tenant AND ativo_id=@ativo AND inicio_utc>=@inicio
            ORDER BY inicio_utc DESC LIMIT 12;
            """;
        await using (var comando = new NpgsqlCommand(timelineSql, conexao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("ativo", ativoId);
            comando.Parameters.AddWithValue("inicio", inicioHoje);
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            while (await leitor.ReadAsync(ct))
            {
                timeline.Add(new LinhaTimeline(
                    ComoDataHora(leitor, 0),
                    ComoDataHora(leitor, 1),
                    ((EstadoAtividade)leitor.GetInt16(2)).ToString(),
                    leitor.IsDBNull(3) ? null : leitor.GetString(3),
                    leitor.GetString(4)));
            }
        }

        var oportunidades = await ListarOportunidadesPorAtivoAsync(conexao, tenantId, ativoId, 6, ct);
        var evidencias = await ListarEvidenciasPorAtivoAsync(conexao, tenantId, ativoId, 8, ct);
        var evidenciasVisuais = await ListarEvidenciasVisuaisAsync(tenantId, 30, ativoId, 8, ct);
        var foco = observado > 0 ? Math.Clamp((int)Math.Round(produtivo * 100d / observado), 0, 100) : 0;
        return new ContextoGerencialEstacao(
            agenteId, unidade, departamento, status, estado, processo, local,
            produtivo, ocioso, aplicativoPrincipal, foco, timeline, oportunidades, evidencias, evidenciasVisuais);
    }

    public async Task<PainelDrilldown> ObterDrilldownAsync(Guid tenantId, string tipo, string? valor, int dias = 30, CancellationToken ct = default)
    {
        tipo = (tipo ?? string.Empty).Trim().ToLowerInvariant();
        valor = Limpar(valor);
        dias = Math.Clamp(dias, 1, 365);
        return tipo switch
        {
            "estado" => await ObterDrilldownEstadoAsync(tenantId, valor ?? "Online", ct),
            "aplicativo" => await ObterDrilldownAplicativoAsync(tenantId, valor ?? string.Empty, dias, ct),
            "site" => await ObterDrilldownSiteAsync(tenantId, valor, dias, ct),
            "local" => await ObterDrilldownLocalAsync(tenantId, valor ?? "escritorio", dias, ct),
            "oportunidade" => await ObterDrilldownOportunidadeAsync(tenantId, valor, dias, ct),
            "evidencia" => await ObterDrilldownEvidenciaAsync(tenantId, valor, dias, ct),
            "indice" => await ObterDrilldownIndiceAsync(tenantId, valor, dias, ct),
            _ => new PainelDrilldown("Detalhes", "Nenhum detalhamento disponível para este indicador.", Array.Empty<LinhaDrilldown>())
        };
    }

    private async Task<IReadOnlyCollection<IndicadorOportunidade>> ObterIndicadoresOportunidadesAsync(Guid tenantId, int dias, CancellationToken ct)
    {
        const string sql = """
            SELECT categoria,
                   COUNT(*) FILTER (WHERE detectada_em_utc >= now() - (@dias * interval '1 day'))::int atual,
                   COUNT(*) FILTER (WHERE detectada_em_utc < now() - (@dias * interval '1 day')
                                     AND detectada_em_utc >= now() - (@dias * 2 * interval '1 day'))::int anterior
            FROM oportunidades
            WHERE tenant_id=@tenant AND detectada_em_utc >= now() - (@dias * 2 * interval '1 day')
            GROUP BY categoria;
            """;
        var contagens = new Dictionary<string, (int Atual, int Anterior)>(StringComparer.OrdinalIgnoreCase);
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("dias", Math.Clamp(dias, 1, 365));
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct)) contagens[leitor.GetString(0)] = (leitor.GetInt32(1), leitor.GetInt32(2));

        var definicoes = new[]
        {
            ("OciosidadeAlta", "Ociosidade alta", "pessoas com alto tempo ocioso"),
            ("SitesNaoProdutivos", "Sites não produtivos", "pessoas com uso relevante"),
            ("BaixaContinuidade", "Baixa continuidade", "pessoas com muitas trocas"),
            ("PossivelNecessidadeTreinamento", "Possível necessidade de treinamento", "pessoas identificadas"),
            ("BaixoUsoSistemaPrincipal", "Baixo uso de sistema principal", "pessoas com uso abaixo do esperado")
        };
        var lista = new List<IndicadorOportunidade>();
        foreach (var d in definicoes)
        {
            contagens.TryGetValue(d.Item1, out var c);
            int? variacao = c.Anterior > 0 ? (int)Math.Round((c.Atual - c.Anterior) * 100d / c.Anterior) : null;
            lista.Add(new IndicadorOportunidade(d.Item1, d.Item2, c.Atual, d.Item3, variacao));
        }
        return lista;
    }

    private async Task<(int Produtividade, int PeriodosOciosos, int AcessosNaoProdutivos, int UsoSistemaPrincipal, long SegundosReuniao)> ObterMetricasEvidenciasAsync(Guid tenantId, CancellationToken ct)
    {
        const string sql = """
            WITH p AS (
                SELECT p.*, COALESCE(r.classificacao,'Neutro') classificacao
                FROM periodos_atividade p
                LEFT JOIN ativos at ON at.id=p.ativo_id AND at.tenant_id=p.tenant_id
                LEFT JOIN LATERAL (
                    SELECT ra.classificacao FROM regras_aplicativos ra
                    WHERE ra.tenant_id=p.tenant_id AND lower(ra.processo_executavel)=lower(p.processo_executavel)
                      AND (ra.departamento_id=at.departamento_id OR ra.departamento_id IS NULL)
                    ORDER BY (ra.departamento_id IS NOT NULL) DESC LIMIT 1
                ) r ON true
                WHERE p.tenant_id=@tenant AND p.inicio_utc>=date_trunc('day',now())
            ), s AS (
                SELECT su.*, COALESCE(rs.classificacao,'Neutro') classificacao
                FROM sites_utilizados su
                LEFT JOIN LATERAL (
                    SELECT r.classificacao FROM regras_sites r
                    WHERE r.tenant_id=su.tenant_id AND lower(r.dominio)=lower(su.dominio)
                    ORDER BY (r.departamento_id IS NOT NULL) DESC LIMIT 1
                ) rs ON true
                WHERE su.tenant_id=@tenant AND su.inicio_utc>=date_trunc('day',now())
            )
            SELECT
                COALESCE(ROUND(100.0 * SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))) FILTER (WHERE p.classificacao='Produtivo') /
                    NULLIF(SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))) FILTER (WHERE p.estado IN (1,5)),0)),0)::int produtividade,
                COUNT(DISTINCT p.agente_id) FILTER (WHERE p.estado=2)::int ociosos,
                (SELECT COUNT(*)::int FROM s WHERE classificacao='Atencao') acessos,
                COALESCE(ROUND(100.0 * SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))) FILTER (WHERE upper(p.processo_executavel)='ERPDEMO.EXE') /
                    NULLIF(SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))) FILTER (WHERE p.processo_executavel IS NOT NULL),0)),0)::int sistema,
                COALESCE(SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))) FILTER (WHERE p.estado=5),0)::bigint reuniao
            FROM p;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct)) return (0, 0, 0, 0, 0);
        return (leitor.GetInt32(0), leitor.GetInt32(1), leitor.GetInt32(2), leitor.GetInt32(3), leitor.GetInt64(4));
    }

    private async Task<IReadOnlyCollection<ResumoLocalTrabalho>> ObterResumosLocaisAsync(Guid tenantId, CancellationToken ct)
    {
        const string sql = """
            WITH estacoes AS (
                SELECT at.id ativo_id,at.departamento_id,a.id agente_id,
                       CASE WHEN loc.tipo='Escritorio' THEN 'escritorio'
                            WHEN loc.tipo='HomeOffice' THEN 'home'
                            WHEN loc.tipo='Externo' THEN 'externo'
                            ELSE 'sem' END codigo,
                       CASE WHEN loc.tipo='Escritorio' THEN 'Escritório'
                            WHEN loc.tipo='HomeOffice' THEN 'Home office'
                            WHEN loc.tipo='Externo' THEN 'Externo/Remoto'
                            ELSE 'Sem classificação' END nome
                FROM ativos at
                LEFT JOIN agentes a ON a.ativo_id=at.id AND a.tenant_id=at.tenant_id AND a.ativo=true
                LEFT JOIN LATERAL (
                    SELECT r.tipo FROM regras_local_trabalho r
                    WHERE r.tenant_id=at.tenant_id AND r.ativo=true AND a.endereco_ip_publico IS NOT NULL
                      AND a.endereco_ip_publico <<= r.faixa_ip
                    ORDER BY masklen(r.faixa_ip) DESC LIMIT 1
                ) loc ON true
                WHERE at.tenant_id=@tenant AND at.ativo=true
            ), periodos AS (
                SELECT e.codigo,e.nome,e.ativo_id,p.estado,p.inicio_utc,p.fim_utc,p.processo_executavel,
                       COALESCE(regra.classificacao,'Neutro') classificacao
                FROM estacoes e
                LEFT JOIN periodos_atividade p ON p.tenant_id=@tenant AND p.ativo_id=e.ativo_id AND p.inicio_utc>=now()-interval '30 days'
                LEFT JOIN LATERAL (
                    SELECT r.classificacao FROM regras_aplicativos r
                    WHERE r.tenant_id=@tenant AND lower(r.processo_executavel)=lower(p.processo_executavel)
                    ORDER BY (r.departamento_id IS NOT NULL) DESC LIMIT 1
                ) regra ON true
            )
            SELECT e.codigo,e.nome,COUNT(DISTINCT e.ativo_id)::int estacoes,
                   COALESCE(ROUND(100.0 * SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))) FILTER (WHERE p.classificacao='Produtivo') /
                       NULLIF(SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))),0)),0)::int produtividade,
                   COALESCE(ROUND(100.0 * SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))) FILTER (WHERE p.estado IN (1,5)) /
                       NULLIF(SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))),0)),0)::int foco,
                   COALESCE(ROUND(100.0 * SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))) FILTER (WHERE p.estado=2) /
                       NULLIF(SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc))),0)),0)::int ociosidade
            FROM estacoes e
            LEFT JOIN periodos p ON p.ativo_id=e.ativo_id AND p.codigo=e.codigo
            GROUP BY e.codigo,e.nome
            ORDER BY CASE e.codigo WHEN 'escritorio' THEN 1 WHEN 'home' THEN 2 WHEN 'externo' THEN 3 ELSE 4 END;
            """;
        var lista = new List<ResumoLocalTrabalho>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
            lista.Add(new ResumoLocalTrabalho(leitor.GetString(0), leitor.GetString(1), leitor.GetInt32(2), leitor.GetInt32(3), leitor.GetInt32(4), leitor.GetInt32(5)));

        foreach (var esperado in new[] { ("escritorio", "Escritório"), ("home", "Home office"), ("externo", "Externo/Remoto"), ("sem", "Sem classificação") })
            if (lista.All(x => x.Codigo != esperado.Item1)) lista.Add(new ResumoLocalTrabalho(esperado.Item1, esperado.Item2, 0, 0, 0, 0));
        return lista.OrderBy(x => x.Codigo switch { "escritorio" => 1, "home" => 2, "externo" => 3, _ => 4 }).ToArray();
    }

    private async Task<IReadOnlyCollection<RegraLocalResumo>> ListarRegrasLocaisAsync(Guid tenantId, CancellationToken ct)
    {
        const string sql = "SELECT id,nome,faixa_ip::text,tipo FROM regras_local_trabalho WHERE tenant_id=@tenant AND ativo=true ORDER BY nome";
        var lista = new List<RegraLocalResumo>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct)) lista.Add(new RegraLocalResumo(leitor.GetGuid(0), leitor.GetString(1), leitor.GetString(2), leitor.GetString(3)));
        return lista;
    }

    private async Task<IReadOnlyCollection<EstacaoLocalResumo>> ListarEstacoesLocaisAsync(Guid tenantId, CancellationToken ct)
    {
        const string sql = """
            SELECT at.id,at.hostname,at.responsavel,
                   CASE WHEN loc.tipo='Escritorio' THEN 'Escritório'
                        WHEN loc.tipo='HomeOffice' THEN 'Home office'
                        WHEN loc.tipo='Externo' THEN 'Externo/Remoto'
                        ELSE 'Sem classificação' END local_trabalho,
                   host(a.endereco_ip_publico),a.ultima_comunicacao_utc
            FROM ativos at
            LEFT JOIN agentes a ON a.ativo_id=at.id AND a.tenant_id=at.tenant_id AND a.ativo=true
            LEFT JOIN LATERAL (
                SELECT r.tipo FROM regras_local_trabalho r
                WHERE r.tenant_id=at.tenant_id AND r.ativo=true AND a.endereco_ip_publico IS NOT NULL
                  AND a.endereco_ip_publico <<= r.faixa_ip
                ORDER BY masklen(r.faixa_ip) DESC LIMIT 1
            ) loc ON true
            WHERE at.tenant_id=@tenant AND at.ativo=true
            ORDER BY at.hostname;
            """;
        var lista = new List<EstacaoLocalResumo>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
            lista.Add(new EstacaoLocalResumo(
                leitor.GetGuid(0), leitor.GetString(1), leitor.IsDBNull(2) ? null : leitor.GetString(2), leitor.GetString(3),
                leitor.IsDBNull(4) ? null : leitor.GetString(4), leitor.IsDBNull(5) ? null : ComoDataHora(leitor, 5)));
        return lista;
    }

    private async Task<IReadOnlyCollection<OportunidadeResumo>> ListarOportunidadesPorAtivoAsync(NpgsqlConnection conexao, Guid tenantId, Guid ativoId, int limite, CancellationToken ct)
    {
        const string sql = """
            SELECT o.id,o.ativo_id,at.hostname,o.pessoa,o.equipe,o.categoria,o.evidencia_resumida,o.impacto,o.recomendacao,o.status,o.detectada_em_utc
            FROM oportunidades o LEFT JOIN ativos at ON at.id=o.ativo_id AND at.tenant_id=o.tenant_id
            WHERE o.tenant_id=@tenant AND o.ativo_id=@ativo ORDER BY o.detectada_em_utc DESC LIMIT @limite;
            """;
        var lista = new List<OportunidadeResumo>();
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("ativo", ativoId);
        comando.Parameters.AddWithValue("limite", limite);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
            lista.Add(new OportunidadeResumo(leitor.GetGuid(0), leitor.GetGuid(1), leitor.IsDBNull(2) ? null : leitor.GetString(2), leitor.IsDBNull(3) ? null : leitor.GetString(3), leitor.IsDBNull(4) ? null : leitor.GetString(4), leitor.GetString(5), leitor.GetString(6), leitor.GetString(7), leitor.GetString(8), leitor.GetString(9), ComoDataHora(leitor, 10)));
        return lista;
    }

    private async Task<IReadOnlyCollection<EvidenciaResumo>> ListarEvidenciasPorAtivoAsync(NpgsqlConnection conexao, Guid tenantId, Guid ativoId, int limite, CancellationToken ct)
    {
        const string sql = """
            SELECT e.id,e.ativo_id,at.hostname,e.pessoa,e.equipe,e.categoria,e.gravidade,e.resumo,e.detalhe,e.fonte,e.observada_em_utc
            FROM evidencias_contextuais e LEFT JOIN ativos at ON at.id=e.ativo_id AND at.tenant_id=e.tenant_id
            WHERE e.tenant_id=@tenant AND e.ativo_id=@ativo ORDER BY e.observada_em_utc DESC LIMIT @limite;
            """;
        var lista = new List<EvidenciaResumo>();
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("ativo", ativoId);
        comando.Parameters.AddWithValue("limite", limite);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
            lista.Add(new EvidenciaResumo(leitor.GetGuid(0), leitor.GetGuid(1), leitor.IsDBNull(2) ? null : leitor.GetString(2), leitor.IsDBNull(3) ? null : leitor.GetString(3), leitor.IsDBNull(4) ? null : leitor.GetString(4), leitor.GetString(5), leitor.GetString(6), leitor.GetString(7), leitor.IsDBNull(8) ? null : leitor.GetString(8), leitor.IsDBNull(9) ? null : leitor.GetString(9), ComoDataHora(leitor, 10)));
        return lista;
    }

    private async Task<PainelDrilldown> ObterDrilldownEstadoAsync(Guid tenantId, string valor, CancellationToken ct)
    {
        var valorNormalizado = valor.Trim();
        const string sql = """
            WITH base AS (
                SELECT at.id,at.hostname,at.responsavel,a.ultima_comunicacao_utc,
                       COALESCE(a.processo_atual,p.processo_executavel) processo,
                       CASE WHEN a.id IS NULL THEN 'Pendente'
                            WHEN a.ultima_comunicacao_utc < now() - (COALESCE(cm.tempo_offline_minutos,15) * interval '1 minute') THEN 'Offline'
                            WHEN p.estado=1 THEN 'Ativo' WHEN p.estado=2 THEN 'Ocioso' WHEN p.estado=3 THEN 'Bloqueado'
                            WHEN p.estado=5 THEN 'Reunião' ELSE 'Online' END estado,
                       at.cadastro_completo
                FROM ativos at
                LEFT JOIN agentes a ON a.ativo_id=at.id AND a.tenant_id=at.tenant_id AND a.ativo=true
                LEFT JOIN configuracoes_monitoramento cm ON cm.tenant_id=at.tenant_id
                LEFT JOIN LATERAL (SELECT estado,processo_executavel FROM periodos_atividade p WHERE p.tenant_id=at.tenant_id AND p.ativo_id=at.id ORDER BY p.fim_utc DESC LIMIT 1) p ON true
                WHERE at.tenant_id=@tenant AND at.ativo=true
            )
            SELECT id,hostname,responsavel,estado,processo,ultima_comunicacao_utc
            FROM base
            WHERE CASE
                WHEN lower(@valor)='online' THEN estado NOT IN ('Offline','Pendente')
                WHEN lower(@valor) IN ('pendente','cadastro pendente') THEN cadastro_completo=false
                ELSE lower(estado)=lower(@valor) END
            ORDER BY hostname;
            """;
        var linhas = new List<LinhaDrilldown>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("valor", valorNormalizado);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
        {
            var ultima = leitor.IsDBNull(5) ? null : ComoDataHora(leitor, 5).ToLocalTime().ToString("dd/MM HH:mm");
            linhas.Add(new LinhaDrilldown(
                leitor.GetString(1),
                leitor.IsDBNull(2) ? "Responsável não informado" : leitor.GetString(2),
                leitor.IsDBNull(4) ? ultima : leitor.GetString(4),
                leitor.GetString(3),
                $"/estacoes/{leitor.GetGuid(0)}"));
        }
        return new PainelDrilldown($"Estações — {valorNormalizado}", $"{linhas.Count} item(ns) geraram este indicador.", linhas);
    }

    private async Task<PainelDrilldown> ObterDrilldownAplicativoAsync(Guid tenantId, string processo, int dias, CancellationToken ct)
    {
        const string sql = """
            SELECT at.id,at.hostname,at.responsavel,
                   SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc)))::bigint segundos
            FROM periodos_atividade p
            JOIN ativos at ON at.id=p.ativo_id AND at.tenant_id=p.tenant_id
            WHERE p.tenant_id=@tenant AND p.inicio_utc>=now()-(@dias * interval '1 day')
              AND lower(p.processo_executavel)=lower(@processo)
            GROUP BY at.id,at.hostname,at.responsavel
            ORDER BY segundos DESC;
            """;
        var linhas = new List<LinhaDrilldown>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("dias", dias);
        comando.Parameters.AddWithValue("processo", processo);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
            linhas.Add(new LinhaDrilldown(leitor.GetString(1), leitor.IsDBNull(2) ? null : leitor.GetString(2), FormatarTempo(leitor.GetInt64(3)), processo, $"/estacoes/{leitor.GetGuid(0)}"));
        return new PainelDrilldown(processo, "Estações e tempo que compõem o uso exibido no gráfico.", linhas);
    }

    private async Task<PainelDrilldown> ObterDrilldownSiteAsync(Guid tenantId, string? filtro, int dias, CancellationToken ct)
    {
        var classificacao = filtro is "Produtivo" or "Neutro" or "Atencao" ? filtro : null;
        var dominio = classificacao is null ? filtro : null;
        const string sql = """
            SELECT at.id,at.hostname,at.responsavel,lower(s.dominio) dominio,
                   COALESCE(SUM(EXTRACT(EPOCH FROM (s.fim_utc-s.inicio_utc))),0)::bigint segundos,
                   COALESCE(r.classificacao,'Neutro') classificacao
            FROM sites_utilizados s
            JOIN ativos at ON at.id=s.ativo_id AND at.tenant_id=s.tenant_id
            LEFT JOIN regras_sites r ON r.tenant_id=s.tenant_id AND r.departamento_id IS NULL AND lower(r.dominio)=lower(s.dominio)
            WHERE s.tenant_id=@tenant AND s.inicio_utc>=now()-(@dias * interval '1 day')
              AND (@dominio='' OR lower(s.dominio)=lower(@dominio))
              AND (@classificacao='' OR COALESCE(r.classificacao,'Neutro')=@classificacao)
            GROUP BY at.id,at.hostname,at.responsavel,lower(s.dominio),r.classificacao
            ORDER BY segundos DESC
            LIMIT 300;
            """;
        var linhas = new List<LinhaDrilldown>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("dias", dias);
        comando.Parameters.AddWithValue("dominio", dominio ?? string.Empty);
        comando.Parameters.AddWithValue("classificacao", classificacao ?? string.Empty);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
            linhas.Add(new LinhaDrilldown(leitor.GetString(1), leitor.IsDBNull(2) ? null : leitor.GetString(2), $"{leitor.GetString(3)} · {FormatarTempo(leitor.GetInt64(4))}", leitor.GetString(5) == "Atencao" ? "Atenção" : leitor.GetString(5), $"/estacoes/{leitor.GetGuid(0)}"));
        var titulo = dominio is not null ? dominio : classificacao is not null ? $"Sites — {(classificacao == "Atencao" ? "Atenção" : classificacao)}" : "Contexto web";
        return new PainelDrilldown(titulo, "Estações e duração agregada que formaram o indicador. Somente domínio, navegador e período são considerados.", linhas);
    }

    private async Task<PainelDrilldown> ObterDrilldownLocalAsync(Guid tenantId, string codigo, int dias, CancellationToken ct)
    {
        var painel = await ObterPainelLocaisAsync(tenantId, ct);
        var nome = painel.Resumos.FirstOrDefault(x => x.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase))?.Nome ?? codigo;
        var linhas = painel.Estacoes
            .Where(x => NormalizarCodigoLocal(x.Local).Equals(codigo, StringComparison.OrdinalIgnoreCase))
            .Select(x => new LinhaDrilldown(x.Hostname, x.Responsavel, x.EnderecoIpPublico, x.Local, $"/estacoes/{x.AtivoId}"))
            .ToArray();
        return new PainelDrilldown(nome, $"Estações classificadas neste local. Período analítico: {dias} dia(s).", linhas);
    }

    private async Task<PainelDrilldown> ObterDrilldownOportunidadeAsync(Guid tenantId, string? categoria, int dias, CancellationToken ct)
    {
        var itens = await ListarOportunidadesAsync(tenantId, dias, categoria, null, null, 200, ct);
        var linhas = itens.Select(x => new LinhaDrilldown(x.Pessoa ?? x.Hostname ?? "Equipe", x.EvidenciaResumida, x.Recomendacao, x.Impacto, x.AtivoId.HasValue ? $"/estacoes/{x.AtivoId}" : null)).ToArray();
        return new PainelDrilldown(categoria is null ? "Oportunidades" : NomeCategoria(categoria), "Itens que compõem o indicador selecionado.", linhas);
    }

    private async Task<PainelDrilldown> ObterDrilldownEvidenciaAsync(Guid tenantId, string? categoria, int dias, CancellationToken ct)
    {
        var itens = await ListarEvidenciasAsync(tenantId, dias, null, categoria, null, 200, ct);
        var linhas = itens.Select(x => new LinhaDrilldown(x.Pessoa ?? x.Equipe ?? x.Hostname ?? "Ambiente", x.Resumo, x.Detalhe, x.Gravidade, x.AtivoId.HasValue ? $"/estacoes/{x.AtivoId}" : null)).ToArray();
        return new PainelDrilldown(categoria is null ? "Evidências" : categoria, "Fatos observáveis que compõem o indicador selecionado.", linhas);
    }

    private async Task<PainelDrilldown> ObterDrilldownIndiceAsync(Guid tenantId, string? componente, int dias, CancellationToken ct)
    {
        if (string.Equals(componente, "web", StringComparison.OrdinalIgnoreCase))
            return await ObterDrilldownSiteAsync(tenantId, null, dias, ct);

        const string sql = """
            WITH periodos AS (
                SELECT p.*,
                       LAG(p.processo_executavel) OVER (PARTITION BY p.ativo_id ORDER BY p.inicio_utc) AS processo_anterior
                FROM periodos_atividade p
                WHERE p.tenant_id=@tenant AND p.inicio_utc>=now()-(@dias * interval '1 day')
            ), contexto AS (
                SELECT p.*,
                       regra.classificacao AS classificacao_explicita,
                       COALESCE(regra.classificacao,'Neutro') AS classificacao
                FROM periodos p
                JOIN ativos at ON at.id=p.ativo_id AND at.tenant_id=p.tenant_id
                LEFT JOIN LATERAL (
                    SELECT r.classificacao
                    FROM regras_aplicativos r
                    WHERE r.tenant_id=p.tenant_id
                      AND lower(r.processo_executavel)=lower(p.processo_executavel)
                      AND (r.departamento_id=at.departamento_id OR r.departamento_id IS NULL)
                    ORDER BY (r.departamento_id IS NOT NULL) DESC
                    LIMIT 1
                ) regra ON true
            ), agregado AS (
                SELECT c.ativo_id,
                       COALESCE(SUM(EXTRACT(EPOCH FROM (c.fim_utc-c.inicio_utc))),0)::bigint AS total,
                       COALESCE(SUM(EXTRACT(EPOCH FROM (c.fim_utc-c.inicio_utc))) FILTER (WHERE c.estado IN (1,5)),0)::bigint AS atividade,
                       COALESCE(SUM(EXTRACT(EPOCH FROM (c.fim_utc-c.inicio_utc))) FILTER (WHERE c.classificacao_explicita='Produtivo'),0)::bigint AS produtivo,
                       COALESCE(SUM(EXTRACT(EPOCH FROM (c.fim_utc-c.inicio_utc))) FILTER (WHERE c.classificacao_explicita IS NOT NULL),0)::bigint AS classificado,
                       COALESCE(SUM(EXTRACT(EPOCH FROM (c.fim_utc-c.inicio_utc))) FILTER (WHERE c.classificacao='Atencao'),0)::bigint AS atencao,
                       COUNT(*) FILTER (WHERE c.processo_executavel IS DISTINCT FROM c.processo_anterior AND c.processo_anterior IS NOT NULL)::int AS trocas,
                       COUNT(DISTINCT c.processo_executavel) FILTER (WHERE c.processo_executavel IS NOT NULL AND btrim(c.processo_executavel)<>'')::int AS aplicativos
                FROM contexto c
                GROUP BY c.ativo_id
            )
            SELECT at.id,at.hostname,at.responsavel,
                   COALESCE(a.total,0),COALESCE(a.atividade,0),COALESCE(a.produtivo,0),COALESCE(a.classificado,0),
                   COALESCE(a.atencao,0),COALESCE(a.trocas,0),COALESCE(a.aplicativos,0)
            FROM ativos at
            LEFT JOIN agregado a ON a.ativo_id=at.id
            WHERE at.tenant_id=@tenant AND at.ativo=true
            ORDER BY COALESCE(a.total,0) DESC,at.hostname;
            """;

        componente = (componente ?? "indice").Trim().ToLowerInvariant();
        var linhas = new List<LinhaDrilldown>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("dias", dias);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
        {
            var total = leitor.GetInt64(3);
            var atividade = leitor.GetInt64(4);
            var produtivo = leitor.GetInt64(5);
            var classificado = leitor.GetInt64(6);
            var atencao = leitor.GetInt64(7);
            var trocas = leitor.GetInt32(8);
            var aplicativos = leitor.GetInt32(9);
            var atividadePercentual = total > 0 ? Math.Clamp((int)Math.Round(atividade * 100d / total), 0, 100) : 0;
            var continuidade = atividade > 0
                ? Math.Clamp((int)Math.Round(100 - (trocas / Math.Max(atividade / 3600d, .25d)) * 2d), 0, 100)
                : 0;
            var contextoProdutivo = classificado > 0 ? Math.Clamp((int)Math.Round(produtivo * 100d / classificado), 0, 100) : 0;
            var atencaoPercentual = total > 0 ? Math.Clamp((int)Math.Round(atencao * 100d / total), 0, 100) : 0;

            var (valor, estado) = componente switch
            {
                "atividade" => ($"{atividadePercentual}/100", "Atividade observada"),
                "continuidade" => ($"{continuidade}/100", $"{trocas} troca(s)"),
                "contexto" or "produtivo" => ($"{contextoProdutivo}%", "Contexto produtivo"),
                "atencao" => ($"{atencaoPercentual}%", "Tempo de atenção"),
                "aplicativos" => ($"{aplicativos} app(s)", FormatarTempo(total)),
                "cobertura" => (FormatarTempo(total), total > 0 ? "Com dados" : "Sem dados"),
                _ => ($"{atividadePercentual}/100", "Contribuição observada")
            };

            linhas.Add(new LinhaDrilldown(
                leitor.GetString(1),
                leitor.IsDBNull(2) ? null : leitor.GetString(2),
                valor,
                estado,
                $"/estacoes/{leitor.GetGuid(0)}"));
        }

        var (titulo, subtitulo) = componente switch
        {
            "atividade" => ("Atividade observada", "Estações e percentuais de tempo ativo/reunião sobre o tempo observado."),
            "continuidade" => ("Continuidade", "Estações, trocas de aplicativo e score de continuidade que contribuíram para o índice."),
            "contexto" or "produtivo" => ("Contexto produtivo", "Estações e proporção do tempo explicitamente classificado como produtivo."),
            "atencao" => ("Tempo de atenção", "Estações com tempo em aplicativos classificados como Atenção."),
            "aplicativos" => ("Cobertura de aplicativos", "Estações, quantidade de aplicativos observados e duração total do período."),
            "cobertura" => ("Cobertura dos dados", "Estações que possuem ou não tempo observado no período selecionado."),
            _ => ("Composição do índice", "Estações que contribuem para os sinais agregados do período.")
        };
        return new PainelDrilldown(titulo, subtitulo, linhas);
    }

    private static ConfiguracoesMonitoramento LerConfiguracoes(NpgsqlDataReader leitor)
        => new(
            leitor.GetBoolean(0), leitor.GetBoolean(1), leitor.GetBoolean(2),
            leitor.GetInt32(3), leitor.GetInt32(4), leitor.GetInt32(5),
            leitor.IsDBNull(6) ? null : leitor.GetString(6), leitor.GetBoolean(7), leitor.GetBoolean(8),
            leitor.GetBoolean(9), leitor.GetInt32(10), leitor.GetInt32(11), leitor.GetBoolean(12),
            leitor.GetFieldValue<TimeOnly>(13).ToString("HH:mm"), leitor.GetFieldValue<TimeOnly>(14).ToString("HH:mm"), leitor.GetString(15),
            leitor.GetInt32(16), leitor.GetInt32(17), leitor.GetBoolean(18), leitor.GetBoolean(19),
            leitor.IsDBNull(20) ? null : leitor.GetString(20), ComoDataHora(leitor, 21));

    private static string NormalizarUrlEvidencia(string valor)
    {
        valor = (valor ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        if (valor.StartsWith("/", StringComparison.Ordinal)) return valor;
        return "/" + valor.TrimStart('/');
    }

    private static string NomeCategoria(string codigo) => codigo switch
    {
        "OciosidadeAlta" => "Ociosidade alta",
        "SitesNaoProdutivos" => "Sites não produtivos",
        "BaixaContinuidade" => "Baixa continuidade",
        "PossivelNecessidadeTreinamento" => "Possível necessidade de treinamento",
        "BaixoUsoSistemaPrincipal" => "Baixo uso de sistema principal",
        "HomeOffice" => "Oportunidades em home office",
        _ => codigo
    };

    private static string NormalizarCodigoLocal(string local)
        => local.StartsWith("Escrit", StringComparison.OrdinalIgnoreCase) ? "escritorio"
         : local.StartsWith("Home", StringComparison.OrdinalIgnoreCase) ? "home"
         : local.StartsWith("Externo", StringComparison.OrdinalIgnoreCase) ? "externo"
         : "sem";

    private static string NormalizarImpactoFiltro(string? valor)
        => valor is "Baixo" or "Medio" or "Alto" ? valor : string.Empty;

    private static string NormalizarGravidadeFiltro(string? valor)
        => valor is "Positiva" or "Informativa" or "Atencao" or "Alta" ? valor : string.Empty;

    private static string NormalizarHorario(string? valor, string padrao)
        => TimeSpan.TryParse(valor, out var horario) && horario >= TimeSpan.Zero && horario < TimeSpan.FromDays(1)
            ? horario.ToString(@"hh\:mm")
            : padrao;

    private static string NormalizarDias(string? valor)
    {
        var dias = (valor ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var d) ? d : 0)
            .Where(x => x is >= 1 and <= 7)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        return dias.Length == 0 ? "1,2,3,4,5" : string.Join(',', dias);
    }

    private static string FormatarTempo(long segundos)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, segundos));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m";
    }

    private static DateTimeOffset ComoDataHora(NpgsqlDataReader leitor, int indice)
        => new(DateTime.SpecifyKind(leitor.GetDateTime(indice), DateTimeKind.Utc));

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
