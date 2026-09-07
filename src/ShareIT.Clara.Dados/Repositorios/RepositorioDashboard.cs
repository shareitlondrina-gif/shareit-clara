using Npgsql;
using ShareIT.Clara.Comum.Configuracao;
using ShareIT.Clara.Dados.Banco;
using ShareIT.Clara.Dominio.Contratos;
using ShareIT.Clara.Dominio.Enumeracoes;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: fornecer a visão operacional “Agora” para gestores autenticados.
/// Origem: agentes e último período de atividade de cada estação.
/// Destino: Dashboard Web.
/// Dependências: PostgreSQL e configuração de limiar offline.
/// Impacto: entrega o primeiro painel ponta a ponta do Share IT .Clara.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioDashboard(FabricaConexoesClara fabrica, ConfiguracaoClara configuracao)
{
    public async Task<DashboardAgora> ObterAgoraAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT a.id, a.hostname, at.responsavel, a.versao, a.ultima_comunicacao_utc,
                   CASE WHEN a.ultima_comunicacao_utc >= now() - (@offlineMinutos * interval '1 minute') THEN 'Online' ELSE 'Offline' END AS status,
                   p.estado
            FROM agentes a
            JOIN ativos at ON at.id=a.ativo_id AND at.tenant_id=a.tenant_id
            LEFT JOIN LATERAL (
                SELECT estado FROM periodos_atividade p
                WHERE p.agente_id=a.id
                ORDER BY p.fim_utc DESC LIMIT 1
            ) p ON true
            WHERE a.tenant_id=@tenant AND a.ativo=true
            ORDER BY status DESC, a.hostname;
            """;
        var linhas = new List<LinhaAgenteAgora>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("offlineMinutos", configuracao.Servidor.MinutosParaConsiderarOffline);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
        {
            var status = leitor.GetString(5);
            string? estado = null;
            if (!leitor.IsDBNull(6)) estado = ((EstadoAtividade)leitor.GetInt16(6)).ToString();
            linhas.Add(new LinhaAgenteAgora(
                leitor.GetGuid(0),
                leitor.GetString(1),
                leitor.IsDBNull(2) ? null : leitor.GetString(2),
                status,
                status == "Offline" ? EstadoAtividade.Offline.ToString() : estado,
                leitor.GetString(3),
                leitor.IsDBNull(4) ? null : new DateTimeOffset(DateTime.SpecifyKind(leitor.GetDateTime(4), DateTimeKind.Utc))));
        }

        return new DashboardAgora(
            linhas.Count,
            linhas.Count(x => x.Status == "Online"),
            linhas.Count(x => x.Status == "Offline"),
            linhas.Count(x => x.Status == "Online" && x.EstadoAtual == EstadoAtividade.Ativo.ToString()),
            linhas.Count(x => x.Status == "Online" && x.EstadoAtual == EstadoAtividade.Ocioso.ToString()),
            linhas.Count(x => x.Status == "Online" && x.EstadoAtual == EstadoAtividade.Bloqueado.ToString()),
            linhas);
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
                var inicio = new DateTimeOffset(DateTime.SpecifyKind(leitor.GetDateTime(0), DateTimeKind.Utc));
                var fim = new DateTimeOffset(DateTime.SpecifyKind(leitor.GetDateTime(1), DateTimeKind.Utc));
                linhas.Add(new LinhaTimeline(
                    inicio,
                    fim,
                    ((EstadoAtividade)leitor.GetInt16(2)).ToString(),
                    leitor.IsDBNull(3) ? null : leitor.GetString(3),
                    leitor.GetString(4)));
            }
        }
        return new TimelineAgente(agenteId, hostname, responsavel, linhas);
    }

}
