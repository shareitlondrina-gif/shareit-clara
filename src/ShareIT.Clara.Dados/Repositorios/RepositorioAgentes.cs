using System.Security.Cryptography;
using Npgsql;
using ShareIT.Clara.Comum.Seguranca;
using ShareIT.Clara.Dados.Banco;
using ShareIT.Clara.Dominio.Contratos;
using ShareIT.Clara.Dominio.Modelos;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: concentrar registro, autenticação, heartbeat, telemetria e status de atualização dos agentes Windows.
/// Origem: endpoints autenticados da API do Share IT .Clara.
/// Destino: tabelas ativos, agentes, periodos_atividade e atualizacoes_agente.
/// Dependências: PostgreSQL, HashSeguro e contratos do domínio.
/// Impacto: sustenta o fluxo Agent → API → PostgreSQL → Dashboard da primeira versão.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioAgentes(FabricaConexoesClara fabrica)
{
    public async Task<RegistroAgenteResposta?> RegistrarAsync(RegistroAgenteSolicitacao solicitacao, CancellationToken ct = default)
    {
        var tokenHash = HashSeguro.Sha256Texto(solicitacao.TokenInstalacao);
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        const string buscarToken = """
            SELECT t.tenant_id, t.ativo_id
            FROM tokens_instalacao t
            WHERE t.token_hash=@hash AND t.usado_em_utc IS NULL AND t.expira_em_utc > now()
            FOR UPDATE;
            """;
        Guid tenantId;
        Guid ativoId;
        await using (var comando = new NpgsqlCommand(buscarToken, conexao, transacao))
        {
            comando.Parameters.AddWithValue("hash", tokenHash);
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            if (!await leitor.ReadAsync(ct)) return null;
            tenantId = leitor.GetGuid(0);
            ativoId = leitor.GetGuid(1);
        }

        var identificadorAgente = Guid.NewGuid();
        var tokenDispositivo = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenDispositivoHash = HashSeguro.Sha256Texto(tokenDispositivo);

        const string atualizarAtivo = "UPDATE ativos SET hostname=@hostname WHERE id=@ativo AND tenant_id=@tenant";
        await using (var comando = new NpgsqlCommand(atualizarAtivo, conexao, transacao))
        {
            comando.Parameters.AddWithValue("hostname", solicitacao.Hostname);
            comando.Parameters.AddWithValue("ativo", ativoId);
            comando.Parameters.AddWithValue("tenant", tenantId);
            await comando.ExecuteNonQueryAsync(ct);
        }

        const string inserirAgente = """
            INSERT INTO agentes(tenant_id,ativo_id,identificador_agente,token_hash,versao,hostname,usuario_windows,sistema_operacional,fabricante,modelo,ultima_comunicacao_utc,status)
            VALUES(@tenant,@ativo,@identificador,@token,@versao,@hostname,@usuario,@so,@fabricante,@modelo,now(),'Online')
            ON CONFLICT(tenant_id,ativo_id) DO UPDATE SET
                identificador_agente=EXCLUDED.identificador_agente,
                token_hash=EXCLUDED.token_hash,
                versao=EXCLUDED.versao,
                hostname=EXCLUDED.hostname,
                usuario_windows=EXCLUDED.usuario_windows,
                sistema_operacional=EXCLUDED.sistema_operacional,
                fabricante=EXCLUDED.fabricante,
                modelo=EXCLUDED.modelo,
                ultima_comunicacao_utc=now(),
                status='Online',
                ativo=true,
                atualizado_em_utc=now();
            """;
        await using (var comando = new NpgsqlCommand(inserirAgente, conexao, transacao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("ativo", ativoId);
            comando.Parameters.AddWithValue("identificador", identificadorAgente);
            comando.Parameters.AddWithValue("token", tokenDispositivoHash);
            comando.Parameters.AddWithValue("versao", solicitacao.VersaoAgente);
            comando.Parameters.AddWithValue("hostname", solicitacao.Hostname);
            comando.Parameters.AddWithValue("usuario", (object?)solicitacao.UsuarioWindows ?? DBNull.Value);
            comando.Parameters.AddWithValue("so", solicitacao.SistemaOperacional);
            comando.Parameters.AddWithValue("fabricante", (object?)solicitacao.Fabricante ?? DBNull.Value);
            comando.Parameters.AddWithValue("modelo", (object?)solicitacao.Modelo ?? DBNull.Value);
            await comando.ExecuteNonQueryAsync(ct);
        }

        await using (var usarToken = new NpgsqlCommand("UPDATE tokens_instalacao SET usado_em_utc=now() WHERE token_hash=@hash", conexao, transacao))
        {
            usarToken.Parameters.AddWithValue("hash", tokenHash);
            await usarToken.ExecuteNonQueryAsync(ct);
        }

        await transacao.CommitAsync(ct);
        return new RegistroAgenteResposta(identificadorAgente, tokenDispositivo, ativoId, tenantId);
    }

    public async Task<ContextoAgente?> AutenticarAsync(Guid identificador, string token, CancellationToken ct = default)
    {
        var hash = HashSeguro.Sha256Texto(token);
        const string sql = """
            SELECT id, tenant_id, ativo_id
            FROM agentes
            WHERE identificador_agente=@identificador AND token_hash=@hash AND ativo=true
            LIMIT 1;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("identificador", identificador);
        comando.Parameters.AddWithValue("hash", hash);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        return await leitor.ReadAsync(ct)
            ? new ContextoAgente(leitor.GetGuid(0), leitor.GetGuid(1), leitor.GetGuid(2))
            : null;
    }

    public async Task RegistrarHeartbeatAsync(ContextoAgente contexto, HeartbeatAgente heartbeat, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE agentes SET
                versao=@versao,
                hostname=@hostname,
                usuario_windows=@usuario,
                ultima_comunicacao_utc=@momento,
                status='Online',
                atualizado_em_utc=now()
            WHERE id=@agente AND tenant_id=@tenant;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("versao", heartbeat.VersaoAgente);
        comando.Parameters.AddWithValue("hostname", heartbeat.Hostname);
        comando.Parameters.AddWithValue("usuario", (object?)heartbeat.UsuarioWindows ?? DBNull.Value);
        comando.Parameters.AddWithValue("momento", heartbeat.MomentoUtc);
        comando.Parameters.AddWithValue("agente", contexto.AgenteId);
        comando.Parameters.AddWithValue("tenant", contexto.TenantId);
        await comando.ExecuteNonQueryAsync(ct);
    }

    public async Task InserirPeriodosAsync(ContextoAgente contexto, IEnumerable<PeriodoAtividade> periodos, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        const string sql = """
            INSERT INTO periodos_atividade(tenant_id,ativo_id,agente_id,usuario_windows,estado,inicio_utc,fim_utc,processo_executavel)
            VALUES(@tenant,@ativo,@agente,@usuario,@estado,@inicio,@fim,@processo);
            """;
        foreach (var periodo in periodos.Where(p => p.FimUtc >= p.InicioUtc))
        {
            await using var comando = new NpgsqlCommand(sql, conexao, transacao);
            comando.Parameters.AddWithValue("tenant", contexto.TenantId);
            comando.Parameters.AddWithValue("ativo", contexto.AtivoId);
            comando.Parameters.AddWithValue("agente", contexto.AgenteId);
            comando.Parameters.AddWithValue("usuario", periodo.UsuarioWindows);
            comando.Parameters.AddWithValue("estado", (short)periodo.Estado);
            comando.Parameters.AddWithValue("inicio", periodo.InicioUtc);
            comando.Parameters.AddWithValue("fim", periodo.FimUtc);
            comando.Parameters.AddWithValue("processo", (object?)periodo.ProcessoExecutavel ?? DBNull.Value);
            await comando.ExecuteNonQueryAsync(ct);
        }

        await transacao.CommitAsync(ct);
    }

    public async Task RegistrarStatusAtualizacaoAsync(ContextoAgente contexto, StatusAtualizacaoAgente status, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO atualizacoes_agente(tenant_id,agente_id,versao_origem,versao_destino,status,detalhe,momento_utc)
            VALUES(@tenant,@agente,@origem,@destino,@status,@detalhe,@momento);
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", contexto.TenantId);
        comando.Parameters.AddWithValue("agente", contexto.AgenteId);
        comando.Parameters.AddWithValue("origem", status.VersaoOrigem);
        comando.Parameters.AddWithValue("destino", status.VersaoDestino);
        comando.Parameters.AddWithValue("status", status.Status);
        comando.Parameters.AddWithValue("detalhe", (object?)status.Detalhe ?? DBNull.Value);
        comando.Parameters.AddWithValue("momento", status.MomentoUtc);
        await comando.ExecuteNonQueryAsync(ct);
    }
}

public sealed record ContextoAgente(Guid AgenteId, Guid TenantId, Guid AtivoId);
