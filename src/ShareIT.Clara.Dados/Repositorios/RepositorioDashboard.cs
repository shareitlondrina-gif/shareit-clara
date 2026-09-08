using Npgsql;
using ShareIT.Clara.Comum.Configuracao;
using ShareIT.Clara.Dados.Banco;
using ShareIT.Clara.Dominio.Contratos;
using ShareIT.Clara.Dominio.Enumeracoes;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: fornecer visões operacionais e analíticas do Share IT .Clara para gestores autenticados.
/// Origem: agentes, ativos, períodos de atividade, classificações e integrações do tenant.
/// Destino: dashboards Agora, Timeline, Aplicativos e Integrações.
/// Dependências: PostgreSQL e configuração de limiar offline.
/// Impacto: transforma telemetria bruta em contexto de trabalho sem capturar conteúdo digitado ou conteúdo de tela.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioDashboard(FabricaConexoesClara fabrica, ConfiguracaoClara configuracao)
{
    public async Task<DashboardAgora> ObterAgoraAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT a.id,a.ativo_id,a.hostname,COALESCE(a.usuario_windows,at.usuario_windows_detectado),at.responsavel,
                   COALESCE(a.numero_serie,at.numero_serie),COALESCE(a.fabricante,at.fabricante),COALESCE(a.modelo,at.modelo),
                   a.versao,a.ultima_comunicacao_utc,
                   CASE WHEN a.ultima_comunicacao_utc >= now() - (@offlineMinutos * interval '1 minute') THEN 'Online' ELSE 'Offline' END AS status,
                   p.estado,COALESCE(a.processo_atual,p.processo_executavel),at.cadastro_completo,
                   CASE WHEN loc.tipo='Escritorio' THEN 'Escritório'
                        WHEN loc.tipo='HomeOffice' THEN 'Home office'
                        WHEN loc.tipo='Externo' THEN 'Externo'
                        WHEN loc.tipo='Outro' THEN COALESCE(loc.nome,'Outro')
                        WHEN a.endereco_ip_publico IS NULL THEN 'Desconhecido'
                        ELSE 'Remoto/externo' END AS local_trabalho
            FROM agentes a
            JOIN ativos at ON at.id=a.ativo_id AND at.tenant_id=a.tenant_id
            LEFT JOIN LATERAL (
                SELECT estado,processo_executavel FROM periodos_atividade p
                WHERE p.agente_id=a.id
                ORDER BY p.fim_utc DESC LIMIT 1
            ) p ON true
            LEFT JOIN LATERAL (
                SELECT r.tipo,r.nome FROM regras_local_trabalho r
                WHERE r.tenant_id=a.tenant_id AND r.ativo=true AND a.endereco_ip_publico IS NOT NULL
                  AND a.endereco_ip_publico <<= r.faixa_ip
                ORDER BY masklen(r.faixa_ip) DESC LIMIT 1
            ) loc ON true
            WHERE a.tenant_id=@tenant AND a.ativo=true
            ORDER BY status DESC, at.cadastro_completo, a.hostname;
            """;
        var linhas = new List<LinhaAgenteAgora>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using (var comando = new NpgsqlCommand(sql, conexao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("offlineMinutos", configuracao.Servidor.MinutosParaConsiderarOffline);
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            while (await leitor.ReadAsync(ct))
            {
                var status = leitor.GetString(10);
                string? estado = null;
                if (!leitor.IsDBNull(11)) estado = ((EstadoAtividade)leitor.GetInt16(11)).ToString();
                linhas.Add(new LinhaAgenteAgora(
                    leitor.GetGuid(0), leitor.GetGuid(1), leitor.GetString(2),
                    leitor.IsDBNull(3) ? null : leitor.GetString(3), leitor.IsDBNull(4) ? null : leitor.GetString(4),
                    leitor.IsDBNull(5) ? null : leitor.GetString(5), leitor.IsDBNull(6) ? null : leitor.GetString(6), leitor.IsDBNull(7) ? null : leitor.GetString(7),
                    status, status == "Offline" ? EstadoAtividade.Offline.ToString() : estado,
                    leitor.IsDBNull(12) ? null : leitor.GetString(12), leitor.GetString(14), leitor.GetBoolean(13),
                    leitor.GetString(8), leitor.IsDBNull(9) ? null : ComoDataHora(leitor, 9)));
            }
        }

        var aplicativos = await ObterResumoAplicativosAsync(conexao, tenantId, new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero), 6, ct);
        var (ativosHoje, ociososHoje) = await ObterTemposHojeAsync(conexao, tenantId, ct);

        return new DashboardAgora(
            linhas.Count,
            linhas.Count(x => x.Status == "Online"),
            linhas.Count(x => x.Status == "Offline"),
            linhas.Count(x => x.Status == "Online" && x.EstadoAtual == EstadoAtividade.Ativo.ToString()),
            linhas.Count(x => x.Status == "Online" && x.EstadoAtual == EstadoAtividade.Ocioso.ToString()),
            linhas.Count(x => x.Status == "Online" && x.EstadoAtual == EstadoAtividade.Bloqueado.ToString()),
            linhas.Count(x => x.Status == "Online" && x.EstadoAtual == EstadoAtividade.Reuniao.ToString()),
            linhas.Count(x => !x.CadastroCompleto),
            ativosHoje,
            ociososHoje,
            aplicativos.FirstOrDefault()?.ProcessoExecutavel,
            linhas,
            aplicativos);
    }

    public async Task<TimelineAgente?> ObterTimelineAsync(Guid tenantId, Guid agenteId, DateTimeOffset? desdeUtc = null, CancellationToken ct = default)
    {
        desdeUtc ??= DateTimeOffset.UtcNow.AddHours(-24);
        const string cabecalhoSql = """
            SELECT a.hostname, at.responsavel
            FROM agentes a
            JOIN ativos at ON at.id=a.ativo_id AND at.tenant_id=a.tenant_id
            WHERE a.id=@agente AND a.tenant_id=@tenant AND a.ativo=true;
            """;
        const string periodosSql = """
            SELECT inicio_utc,fim_utc,estado,processo_executavel,usuario_windows
            FROM periodos_atividade
            WHERE tenant_id=@tenant AND agente_id=@agente AND fim_utc>=@desde
            ORDER BY inicio_utc DESC
            LIMIT 2000;
            """;

        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        string hostname;
        string? responsavel;
        await using (var cmd = new NpgsqlCommand(cabecalhoSql, conexao))
        {
            cmd.Parameters.AddWithValue("tenant", tenantId);
            cmd.Parameters.AddWithValue("agente", agenteId);
            await using var leitor = await cmd.ExecuteReaderAsync(ct);
            if (!await leitor.ReadAsync(ct)) return null;
            hostname = leitor.GetString(0);
            responsavel = leitor.IsDBNull(1) ? null : leitor.GetString(1);
        }

        var linhas = new List<LinhaTimeline>();
        await using (var cmd = new NpgsqlCommand(periodosSql, conexao))
        {
            cmd.Parameters.AddWithValue("tenant", tenantId);
            cmd.Parameters.AddWithValue("agente", agenteId);
            cmd.Parameters.AddWithValue("desde", desdeUtc.Value);
            await using var leitor = await cmd.ExecuteReaderAsync(ct);
            while (await leitor.ReadAsync(ct))
            {
                linhas.Add(new LinhaTimeline(
                    ComoDataHora(leitor, 0), ComoDataHora(leitor, 1),
                    ((EstadoAtividade)leitor.GetInt16(2)).ToString(),
                    leitor.IsDBNull(3) ? null : leitor.GetString(3), leitor.GetString(4)));
            }
        }
        return new TimelineAgente(agenteId, hostname, responsavel, linhas);
    }

    public async Task<PainelAplicativos> ObterAplicativosAsync(Guid tenantId, int dias = 7, CancellationToken ct = default)
    {
        dias = Math.Clamp(dias, 1, 90);
        var inicio = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-(dias - 1)), TimeSpan.Zero);
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        var resumo = await ObterResumoAplicativosDetalhadoAsync(conexao, tenantId, inicio, 100, ct);
        return new PainelAplicativos(inicio, resumo);
    }

    public async Task<PainelIntegracoes> ObterIntegracoesAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT provedor,nome_exibicao,status,ultima_sincronizacao_utc,ultimo_erro
            FROM integracoes
            WHERE tenant_id=@tenant AND ativo=true
            ORDER BY CASE provedor WHEN 'Microsoft' THEN 1 WHEN 'Google' THEN 2 ELSE 3 END;
            """;
        var lista = new List<IntegracaoResumo>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
            lista.Add(new IntegracaoResumo(leitor.GetString(0), leitor.GetString(1), leitor.GetString(2), leitor.IsDBNull(3) ? null : ComoDataHora(leitor, 3), leitor.IsDBNull(4) ? null : leitor.GetString(4)));
        return new PainelIntegracoes(lista);
    }


    public async Task<PainelIndiceGeral> ObterIndiceGeralAsync(Guid tenantId, CancellationToken ct = default)
    {
        var inicio = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        const string sql = """
            WITH base AS (
                SELECT p.*,
                       LAG(p.processo_executavel) OVER (PARTITION BY p.agente_id ORDER BY p.inicio_utc) AS processo_anterior
                FROM periodos_atividade p
                WHERE p.tenant_id=@tenant AND p.inicio_utc>=@inicio
            ), contexto AS (
                SELECT b.*,
                       regra.classificacao AS classificacao_explicita
                FROM base b
                JOIN ativos at ON at.id=b.ativo_id AND at.tenant_id=b.tenant_id
                LEFT JOIN LATERAL (
                    SELECT r.classificacao FROM regras_aplicativos r
                    WHERE r.tenant_id=b.tenant_id AND lower(r.processo_executavel)=lower(b.processo_executavel)
                      AND (r.departamento_id=at.departamento_id OR r.departamento_id IS NULL)
                    ORDER BY (r.departamento_id IS NOT NULL) DESC LIMIT 1
                ) regra ON true
            )
            SELECT
                COALESCE(SUM(EXTRACT(EPOCH FROM (fim_utc-inicio_utc))),0)::bigint AS total,
                COALESCE(SUM(EXTRACT(EPOCH FROM (fim_utc-inicio_utc))) FILTER (WHERE estado IN (1,5)),0)::bigint AS atividade,
                COALESCE(SUM(EXTRACT(EPOCH FROM (fim_utc-inicio_utc))) FILTER (WHERE classificacao_explicita='Produtivo'),0)::bigint AS produtivo,
                COALESCE(SUM(EXTRACT(EPOCH FROM (fim_utc-inicio_utc))) FILTER (WHERE classificacao_explicita IS NOT NULL),0)::bigint AS classificado,
                COUNT(*) FILTER (WHERE processo_executavel IS DISTINCT FROM processo_anterior AND processo_anterior IS NOT NULL)::int AS trocas,
                COUNT(DISTINCT agente_id)::int AS estacoes_dados
            FROM contexto;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        long total=0, atividade=0, produtivo=0, classificado=0;
        int trocas=0, estacoesDados=0;
        await using (var comando = new NpgsqlCommand(sql, conexao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("inicio", inicio);
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            if (await leitor.ReadAsync(ct))
            {
                total = leitor.GetInt64(0); atividade = leitor.GetInt64(1); produtivo = leitor.GetInt64(2);
                classificado = leitor.GetInt64(3); trocas = leitor.GetInt32(4); estacoesDados = leitor.GetInt32(5);
            }
        }
        int totalEstacoes;
        await using (var comando = new NpgsqlCommand("SELECT COUNT(*)::int FROM agentes WHERE tenant_id=@tenant AND ativo=true", conexao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            totalEstacoes = Convert.ToInt32(await comando.ExecuteScalarAsync(ct));
        }

        var atividadeScore = total > 0 ? Percentual(atividade, total) : 0;
        var horasAtividade = Math.Max(atividade / 3600d, .25d);
        var trocasPorHora = trocas / horasAtividade;
        var continuidadeScore = total > 0 ? Math.Clamp((int)Math.Round(100 - trocasPorHora * 2.0), 0, 100) : 0;
        var contextoDisponivel = classificado > 0;
        var contextoScore = contextoDisponivel ? Percentual(produtivo, classificado) : 0;

        var componentes = new List<ComponenteIndice>();
        int indice;
        if (contextoDisponivel)
        {
            indice = (int)Math.Round(atividadeScore * .45 + continuidadeScore * .25 + contextoScore * .30);
            componentes.Add(new("Atividade observada", atividadeScore, 45, true, "Tempo ativo/reunião sobre o tempo observado."));
            componentes.Add(new("Continuidade", continuidadeScore, 25, true, "Heurística baseada na frequência de trocas de aplicativo por hora ativa."));
            componentes.Add(new("Contexto produtivo", contextoScore, 30, true, "Tempo marcado explicitamente como Produtivo dentro do tempo com regra de classificação."));
        }
        else
        {
            indice = (int)Math.Round(atividadeScore * .65 + continuidadeScore * .35);
            componentes.Add(new("Atividade observada", atividadeScore, 65, total > 0, "Tempo ativo/reunião sobre o tempo observado."));
            componentes.Add(new("Continuidade", continuidadeScore, 35, total > 0, "Heurística baseada na frequência de trocas de aplicativo por hora ativa."));
            componentes.Add(new("Contexto produtivo", 0, 0, false, "Ainda não há tempo suficiente com aplicativos classificados pelo tenant."));
        }

        var coberturaEstacoes = totalEstacoes > 0 ? Math.Clamp((int)Math.Round(estacoesDados * 100d / totalEstacoes),0,100) : 0;
        var coberturaClassificacao = atividade > 0 ? Math.Clamp((int)Math.Round(classificado * 100d / atividade),0,100) : 0;
        var confianca = contextoDisponivel
            ? (int)Math.Round(coberturaEstacoes * .70 + coberturaClassificacao * .30)
            : (int)Math.Round(coberturaEstacoes * .70);

        return new PainelIndiceGeral(
            Math.Clamp(indice,0,100), Math.Clamp(confianca,0,100), inicio, componentes,
            total, estacoesDados, totalEstacoes,
            contextoDisponivel
                ? "O índice combina atividade, continuidade e classificações explícitas do tenant. Ele é um indicador de contexto, não uma decisão automática sobre pessoas."
                : "O índice é preliminar porque ainda faltam classificações explícitas de aplicativos. A confiança aumenta conforme a cobertura de dados e contexto melhora.");
    }

    private static int Percentual(long parte, long total)
        => total <= 0 ? 0 : Math.Clamp((int)Math.Round(parte * 100d / total), 0, 100);

    private static async Task<IReadOnlyCollection<ResumoAplicativo>> ObterResumoAplicativosAsync(NpgsqlConnection conexao, Guid tenantId, DateTimeOffset inicio, int limite, CancellationToken ct)
    {
        const string sql = """
            SELECT p.processo_executavel,
                   SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc)))::bigint AS segundos,
                   COALESCE(regra.classificacao,'Neutro') AS classificacao,
                   cat.nome
            FROM periodos_atividade p
            JOIN ativos at ON at.id=p.ativo_id AND at.tenant_id=p.tenant_id
            LEFT JOIN LATERAL (
                SELECT r.classificacao,r.categoria_id FROM regras_aplicativos r
                WHERE r.tenant_id=p.tenant_id AND lower(r.processo_executavel)=lower(p.processo_executavel)
                  AND (r.departamento_id=at.departamento_id OR r.departamento_id IS NULL)
                ORDER BY (r.departamento_id IS NOT NULL) DESC LIMIT 1
            ) regra ON true
            LEFT JOIN categorias_atividade cat ON cat.id=regra.categoria_id
            WHERE p.tenant_id=@tenant AND p.inicio_utc>=@inicio AND p.processo_executavel IS NOT NULL AND btrim(p.processo_executavel)<>''
            GROUP BY p.processo_executavel,regra.classificacao,cat.nome
            ORDER BY segundos DESC
            LIMIT @limite;
            """;
        var lista = new List<ResumoAplicativo>();
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("inicio", inicio);
        comando.Parameters.AddWithValue("limite", limite);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct)) lista.Add(new ResumoAplicativo(leitor.GetString(0), leitor.GetInt64(1), leitor.GetString(2), leitor.IsDBNull(3) ? null : leitor.GetString(3)));
        return lista;
    }

    private static async Task<IReadOnlyCollection<ResumoAplicativoDetalhado>> ObterResumoAplicativosDetalhadoAsync(NpgsqlConnection conexao, Guid tenantId, DateTimeOffset inicio, int limite, CancellationToken ct)
    {
        const string sql = """
            SELECT p.processo_executavel,
                   SUM(EXTRACT(EPOCH FROM (p.fim_utc-p.inicio_utc)))::bigint AS segundos,
                   COUNT(DISTINCT p.ativo_id)::int AS estacoes,
                   COALESCE(regra.classificacao,'Neutro') AS classificacao,
                   cat.nome
            FROM periodos_atividade p
            JOIN ativos at ON at.id=p.ativo_id AND at.tenant_id=p.tenant_id
            LEFT JOIN LATERAL (
                SELECT r.classificacao,r.categoria_id FROM regras_aplicativos r
                WHERE r.tenant_id=p.tenant_id AND lower(r.processo_executavel)=lower(p.processo_executavel)
                  AND (r.departamento_id=at.departamento_id OR r.departamento_id IS NULL)
                ORDER BY (r.departamento_id IS NOT NULL) DESC LIMIT 1
            ) regra ON true
            LEFT JOIN categorias_atividade cat ON cat.id=regra.categoria_id
            WHERE p.tenant_id=@tenant AND p.inicio_utc>=@inicio AND p.processo_executavel IS NOT NULL AND btrim(p.processo_executavel)<>''
            GROUP BY p.processo_executavel,regra.classificacao,cat.nome
            ORDER BY segundos DESC
            LIMIT @limite;
            """;
        var lista = new List<ResumoAplicativoDetalhado>();
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("inicio", inicio);
        comando.Parameters.AddWithValue("limite", limite);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct)) lista.Add(new ResumoAplicativoDetalhado(leitor.GetString(0), leitor.GetInt64(1), leitor.GetInt32(2), leitor.GetString(3), leitor.IsDBNull(4) ? null : leitor.GetString(4)));
        return lista;
    }

    private static async Task<(long Ativo, long Ocioso)> ObterTemposHojeAsync(NpgsqlConnection conexao, Guid tenantId, CancellationToken ct)
    {
        const string sql = """
            SELECT
                COALESCE(SUM(EXTRACT(EPOCH FROM (fim_utc-inicio_utc))) FILTER (WHERE estado IN (1,5)),0)::bigint,
                COALESCE(SUM(EXTRACT(EPOCH FROM (fim_utc-inicio_utc))) FILTER (WHERE estado=2),0)::bigint
            FROM periodos_atividade
            WHERE tenant_id=@tenant AND inicio_utc>=date_trunc('day',now());
            """;
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct)) return (0, 0);
        return (leitor.GetInt64(0), leitor.GetInt64(1));
    }

    private static DateTimeOffset ComoDataHora(NpgsqlDataReader leitor, int indice)
        => new(DateTime.SpecifyKind(leitor.GetDateTime(indice), DateTimeKind.Utc));
}
