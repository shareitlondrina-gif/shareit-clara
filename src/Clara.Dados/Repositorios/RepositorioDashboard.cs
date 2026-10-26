// Clara
// Programador: Paulo Andrade
// Objetivo: persistir e recuperar dados da Clara.
// Origem: camada de aplicação/domínio.
// Destino: PostgreSQL e contratos de domínio.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using Npgsql;
using Clara.Comum.Configuracao;
using Clara.Dados.Banco;
using Clara.Dominio.Contratos;
using Clara.Dominio.Enumeracoes;

namespace Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: fornecer visões operacionais e analíticas da Clara para gestores autenticados.
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
                   CASE WHEN a.ultima_comunicacao_utc >= now() - (COALESCE(cm.tempo_offline_minutos,@offlineMinutos) * interval '1 minute') THEN 'Online' ELSE 'Offline' END AS status,
                   p.estado,COALESCE(a.processo_atual,p.processo_executavel),at.cadastro_completo,
                   CASE WHEN loc.tipo='Escritorio' THEN 'Escritório'
                        WHEN loc.tipo='HomeOffice' THEN 'Home office'
                        WHEN loc.tipo='Externo' THEN 'Externo'
                        WHEN loc.tipo='Outro' THEN COALESCE(loc.nome,'Outro')
                        WHEN a.endereco_ip_publico IS NULL THEN 'Desconhecido'
                        ELSE 'Remoto/externo' END AS local_trabalho
            FROM agentes a
            JOIN ativos at ON at.id=a.ativo_id AND at.tenant_id=a.tenant_id
            LEFT JOIN configuracoes_monitoramento cm ON cm.tenant_id=a.tenant_id
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

    public async Task<PainelNavegacaoWeb> ObterNavegacaoWebAsync(Guid tenantId, int dias = 7, CancellationToken ct = default)
    {
        dias = Math.Clamp(dias, 1, 90);
        var inicio = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-(dias - 1)), TimeSpan.Zero);
        const string sql = """
            SELECT lower(s.dominio) dominio,
                   COALESCE(SUM(EXTRACT(EPOCH FROM (s.fim_utc-s.inicio_utc))),0)::bigint segundos,
                   COUNT(DISTINCT s.ativo_id)::int estacoes,
                   COALESCE(r.classificacao,'Neutro') classificacao,
                   r.categoria
            FROM sites_utilizados s
            LEFT JOIN regras_sites r ON r.tenant_id=s.tenant_id AND r.departamento_id IS NULL AND lower(r.dominio)=lower(s.dominio)
            WHERE s.tenant_id=@tenant AND s.inicio_utc>=@inicio
            GROUP BY lower(s.dominio),r.classificacao,r.categoria
            ORDER BY segundos DESC
            LIMIT 150;
            """;
        var sites = new List<ResumoSiteUtilizado>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using (var comando = new NpgsqlCommand(sql, conexao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("inicio", inicio);
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            while (await leitor.ReadAsync(ct))
                sites.Add(new ResumoSiteUtilizado(leitor.GetString(0), leitor.GetInt64(1), leitor.GetInt32(2), leitor.GetString(3), leitor.IsDBNull(4) ? null : leitor.GetString(4)));
        }

        var regras = new List<RegraSiteResumo>();
        await using (var comando = new NpgsqlCommand("SELECT id,dominio,categoria,classificacao FROM regras_sites WHERE tenant_id=@tenant AND departamento_id IS NULL ORDER BY lower(dominio)", conexao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            while (await leitor.ReadAsync(ct))
                regras.Add(new RegraSiteResumo(leitor.GetGuid(0), leitor.GetString(1), leitor.IsDBNull(2) ? null : leitor.GetString(2), leitor.GetString(3)));
        }

        int estacoes;
        DateTimeOffset? ultimo;
        await using (var comando = new NpgsqlCommand("SELECT COUNT(DISTINCT ativo_id)::int,MAX(fim_utc) FROM sites_utilizados WHERE tenant_id=@tenant AND inicio_utc>=@inicio", conexao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("inicio", inicio);
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            await leitor.ReadAsync(ct);
            estacoes = leitor.GetInt32(0);
            ultimo = leitor.IsDBNull(1) ? null : ComoDataHora(leitor, 1);
        }
        return new PainelNavegacaoWeb(inicio, sites, regras, estacoes, ultimo);
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
        const string sqlAplicativos = """
            WITH base AS (
                SELECT p.*,
                       LAG(p.processo_executavel) OVER (PARTITION BY p.agente_id ORDER BY p.inicio_utc) AS processo_anterior
                FROM periodos_atividade p
                WHERE p.tenant_id=@tenant AND p.inicio_utc>=@inicio
            ), contexto AS (
                SELECT b.*, regra.classificacao AS classificacao_explicita
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
                COALESCE(SUM(EXTRACT(EPOCH FROM (fim_utc-inicio_utc))),0)::bigint,
                COALESCE(SUM(EXTRACT(EPOCH FROM (fim_utc-inicio_utc))) FILTER (WHERE estado IN (1,5)),0)::bigint,
                COALESCE(SUM(EXTRACT(EPOCH FROM (fim_utc-inicio_utc))) FILTER (WHERE classificacao_explicita='Produtivo'),0)::bigint,
                COALESCE(SUM(EXTRACT(EPOCH FROM (fim_utc-inicio_utc))) FILTER (WHERE classificacao_explicita IS NOT NULL),0)::bigint,
                COUNT(*) FILTER (WHERE processo_executavel IS DISTINCT FROM processo_anterior AND processo_anterior IS NOT NULL)::int,
                COUNT(DISTINCT agente_id)::int
            FROM contexto;
            """;
        const string sqlWeb = """
            SELECT
                COALESCE(SUM(EXTRACT(EPOCH FROM (s.fim_utc-s.inicio_utc))),0)::bigint total_web,
                COALESCE(SUM(EXTRACT(EPOCH FROM (s.fim_utc-s.inicio_utc))) FILTER (WHERE r.classificacao='Produtivo'),0)::bigint produtivo_web,
                COALESCE(SUM(EXTRACT(EPOCH FROM (s.fim_utc-s.inicio_utc))) FILTER (WHERE r.id IS NOT NULL),0)::bigint classificado_web,
                COUNT(DISTINCT s.ativo_id)::int estacoes_web
            FROM sites_utilizados s
            LEFT JOIN regras_sites r ON r.tenant_id=s.tenant_id AND r.departamento_id IS NULL AND lower(r.dominio)=lower(s.dominio)
            WHERE s.tenant_id=@tenant AND s.inicio_utc>=@inicio;
            """;

        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        long total=0, atividade=0, produtivo=0, classificado=0;
        int trocas=0, estacoesDados=0;
        await using (var comando = new NpgsqlCommand(sqlAplicativos, conexao))
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

        long totalWeb=0, produtivoWeb=0, classificadoWeb=0;
        int estacoesWeb=0;
        await using (var comando = new NpgsqlCommand(sqlWeb, conexao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("inicio", inicio);
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            if (await leitor.ReadAsync(ct))
            {
                totalWeb = leitor.GetInt64(0); produtivoWeb = leitor.GetInt64(1); classificadoWeb = leitor.GetInt64(2); estacoesWeb = leitor.GetInt32(3);
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
        var contextoAppDisponivel = classificado > 0;
        var contextoAppScore = contextoAppDisponivel ? Percentual(produtivo, classificado) : 0;
        var contextoWebDisponivel = classificadoWeb > 0;
        var contextoWebScore = contextoWebDisponivel ? Percentual(produtivoWeb, classificadoWeb) : 0;

        var componentes = new List<ComponenteIndice>();
        int indice;
        if (contextoAppDisponivel && contextoWebDisponivel)
        {
            indice = (int)Math.Round(atividadeScore * .35 + continuidadeScore * .20 + contextoAppScore * .25 + contextoWebScore * .20);
            componentes.Add(new("Atividade observada", atividadeScore, 35, true, "Tempo ativo/reunião sobre o tempo observado."));
            componentes.Add(new("Continuidade", continuidadeScore, 20, true, "Heurística transparente baseada na frequência de trocas de aplicativo por hora ativa."));
            componentes.Add(new("Contexto produtivo de aplicativos", contextoAppScore, 25, true, "Tempo explicitamente classificado como Produtivo dentro dos aplicativos com regra do tenant."));
            componentes.Add(new("Contexto produtivo web", contextoWebScore, 20, true, "Tempo em domínios explicitamente classificados como Produtivo pelo tenant."));
        }
        else if (contextoAppDisponivel)
        {
            indice = (int)Math.Round(atividadeScore * .45 + continuidadeScore * .25 + contextoAppScore * .30);
            componentes.Add(new("Atividade observada", atividadeScore, 45, true, "Tempo ativo/reunião sobre o tempo observado."));
            componentes.Add(new("Continuidade", continuidadeScore, 25, true, "Heurística transparente baseada na frequência de trocas de aplicativo por hora ativa."));
            componentes.Add(new("Contexto produtivo de aplicativos", contextoAppScore, 30, true, "Tempo explicitamente classificado como Produtivo dentro dos aplicativos com regra do tenant."));
            componentes.Add(new("Contexto produtivo web", 0, 0, false, "Há coleta web disponível somente quando o conector autorizado envia domínios e o tenant os classifica."));
        }
        else if (contextoWebDisponivel)
        {
            indice = (int)Math.Round(atividadeScore * .45 + continuidadeScore * .25 + contextoWebScore * .30);
            componentes.Add(new("Atividade observada", atividadeScore, 45, total > 0, "Tempo ativo/reunião sobre o tempo observado."));
            componentes.Add(new("Continuidade", continuidadeScore, 25, total > 0, "Heurística transparente baseada na frequência de trocas de aplicativo por hora ativa."));
            componentes.Add(new("Contexto produtivo de aplicativos", 0, 0, false, "Ainda não há aplicativos com classificação explícita suficiente."));
            componentes.Add(new("Contexto produtivo web", contextoWebScore, 30, true, "Tempo em domínios explicitamente classificados como Produtivo pelo tenant."));
        }
        else
        {
            indice = (int)Math.Round(atividadeScore * .65 + continuidadeScore * .35);
            componentes.Add(new("Atividade observada", atividadeScore, 65, total > 0, "Tempo ativo/reunião sobre o tempo observado."));
            componentes.Add(new("Continuidade", continuidadeScore, 35, total > 0, "Heurística transparente baseada na frequência de trocas de aplicativo por hora ativa."));
            componentes.Add(new("Contexto produtivo de aplicativos", 0, 0, false, "Ainda não há tempo suficiente com aplicativos classificados pelo tenant."));
            componentes.Add(new("Contexto produtivo web", 0, 0, false, "Ainda não há tempo suficiente em domínios classificados pelo tenant."));
        }

        var coberturaEstacoes = totalEstacoes > 0 ? Math.Clamp((int)Math.Round(estacoesDados * 100d / totalEstacoes),0,100) : 0;
        var coberturaClassificacaoApp = atividade > 0 ? Math.Clamp((int)Math.Round(classificado * 100d / atividade),0,100) : 0;
        var coberturaWeb = totalEstacoes > 0 ? Math.Clamp((int)Math.Round(estacoesWeb * 100d / totalEstacoes),0,100) : 0;
        var coberturaClassificacaoWeb = totalWeb > 0 ? Math.Clamp((int)Math.Round(classificadoWeb * 100d / totalWeb),0,100) : 0;
        var confianca = contextoAppDisponivel && contextoWebDisponivel
            ? (int)Math.Round(coberturaEstacoes * .55 + coberturaClassificacaoApp * .20 + coberturaWeb * .15 + coberturaClassificacaoWeb * .10)
            : contextoAppDisponivel
                ? (int)Math.Round(coberturaEstacoes * .70 + coberturaClassificacaoApp * .30)
                : contextoWebDisponivel
                    ? (int)Math.Round(coberturaEstacoes * .65 + coberturaWeb * .20 + coberturaClassificacaoWeb * .15)
                    : (int)Math.Round(coberturaEstacoes * .70);

        var explicacao = contextoAppDisponivel && contextoWebDisponivel
            ? "O índice combina atividade, continuidade, classificação explícita de aplicativos e contexto web autorizado. O número é explicável e não substitui análise humana."
            : contextoAppDisponivel || contextoWebDisponivel
                ? "O índice usa os sinais disponíveis e redistribui os pesos quando um tipo de contexto ainda não possui cobertura suficiente."
                : "O índice é preliminar porque ainda faltam classificações explícitas de contexto. A confiança aumenta conforme a cobertura de dados melhora.";

        return new PainelIndiceGeral(
            Math.Clamp(indice,0,100), Math.Clamp(confianca,0,100), inicio, componentes,
            total, estacoesDados, totalEstacoes, explicacao);
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
