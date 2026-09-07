using System.Security.Cryptography;
using Npgsql;
using ShareIT.Clara.Comum.Seguranca;
using ShareIT.Clara.Dados.Banco;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: executar operações administrativas iniciais de ativos e instalação de agentes.
/// Origem: dashboard autenticado.
/// Destino: ativos e tokens_instalacao.
/// Dependências: PostgreSQL e geração criptográfica de tokens.
/// Impacto: vincula cada instalação ao tenant correto sem expor lista global de clientes no instalador.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioAdministracao(FabricaConexoesClara fabrica)
{
    public async Task<IReadOnlyCollection<EmpresaBasica>> ListarEmpresasAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = "SELECT id,nome FROM empresas WHERE tenant_id=@tenant AND ativo=true ORDER BY nome";
        var lista = new List<EmpresaBasica>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct)) lista.Add(new EmpresaBasica(leitor.GetGuid(0), leitor.GetString(1)));
        return lista;
    }

    public async Task<TokenInstalacaoGerado> CriarAtivoETokenAsync(Guid tenantId, Guid usuarioId, Guid empresaId, string hostname, string? responsavel, string? patrimonio, CancellationToken ct = default)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var hash = HashSeguro.Sha256Texto(token);
        var expira = DateTimeOffset.UtcNow.AddMinutes(30);

        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        const string ativoSql = """
            INSERT INTO ativos(tenant_id,empresa_id,hostname,responsavel,patrimonio)
            VALUES(@tenant,@empresa,@hostname,@responsavel,@patrimonio)
            ON CONFLICT(tenant_id,hostname) DO UPDATE SET
                empresa_id=EXCLUDED.empresa_id,
                responsavel=EXCLUDED.responsavel,
                patrimonio=EXCLUDED.patrimonio,
                ativo=true
            RETURNING id;
            """;
        Guid ativoId;
        await using (var comando = new NpgsqlCommand(ativoSql, conexao, transacao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("empresa", empresaId);
            comando.Parameters.AddWithValue("hostname", hostname.Trim());
            comando.Parameters.AddWithValue("responsavel", (object?)responsavel?.Trim() ?? DBNull.Value);
            comando.Parameters.AddWithValue("patrimonio", (object?)patrimonio?.Trim() ?? DBNull.Value);
            ativoId = (Guid)(await comando.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Não foi possível criar o ativo."));
        }

        await using (var invalidar = new NpgsqlCommand("UPDATE tokens_instalacao SET usado_em_utc=now() WHERE ativo_id=@ativo AND usado_em_utc IS NULL", conexao, transacao))
        {
            invalidar.Parameters.AddWithValue("ativo", ativoId);
            await invalidar.ExecuteNonQueryAsync(ct);
        }

        const string tokenSql = """
            INSERT INTO tokens_instalacao(tenant_id,ativo_id,token_hash,expira_em_utc,criado_por_usuario_id)
            VALUES(@tenant,@ativo,@hash,@expira,@usuario);
            """;
        await using (var comando = new NpgsqlCommand(tokenSql, conexao, transacao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("ativo", ativoId);
            comando.Parameters.AddWithValue("hash", hash);
            comando.Parameters.AddWithValue("expira", expira);
            comando.Parameters.AddWithValue("usuario", usuarioId);
            await comando.ExecuteNonQueryAsync(ct);
        }

        await transacao.CommitAsync(ct);
        return new TokenInstalacaoGerado(ativoId, token, expira);
    }
}

public sealed record EmpresaBasica(Guid Id, string Nome);
public sealed record TokenInstalacaoGerado(Guid AtivoId, string Token, DateTimeOffset ExpiraEmUtc);
