using Npgsql;
using NpgsqlTypes;
using ShareIT.Clara.Dados.Banco;
using ShareIT.Clara.Dominio.Contratos;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: administrar regras de contexto usadas para interpretar aplicativos e localização de trabalho.
/// Origem: configurações do administrador do tenant.
/// Destino: regras_aplicativos, categorias_atividade e regras_local_trabalho.
/// Dependências: PostgreSQL.
/// Impacto: permite que o mesmo aplicativo tenha significado adequado à realidade de cada empresa sem impor julgamento global.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioContexto(FabricaConexoesClara fabrica)
{
    public async Task<IReadOnlyCollection<CategoriaAtividadeBasica>> ListarCategoriasAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = "SELECT id,nome,classificacao FROM categorias_atividade WHERE tenant_id=@tenant AND ativo=true ORDER BY nome";
        var lista = new List<CategoriaAtividadeBasica>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct)) lista.Add(new CategoriaAtividadeBasica(leitor.GetGuid(0), leitor.GetString(1), leitor.GetString(2)));
        return lista;
    }

    public async Task ClassificarAplicativoAsync(Guid tenantId, string processo, string classificacao, Guid? categoriaId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(processo)) throw new ArgumentException("Processo é obrigatório.", nameof(processo));
        classificacao = NormalizarClassificacao(classificacao);
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        if (categoriaId.HasValue)
        {
            await using var validar = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM categorias_atividade WHERE id=@categoria AND tenant_id=@tenant AND ativo=true)", conexao);
            validar.Parameters.AddWithValue("categoria", categoriaId.Value);
            validar.Parameters.AddWithValue("tenant", tenantId);
            if (!Convert.ToBoolean(await validar.ExecuteScalarAsync(ct)))
                throw new InvalidOperationException("Categoria não encontrada para este tenant.");
        }
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        const string atualizar = """
            UPDATE regras_aplicativos SET categoria_id=@categoria,classificacao=@classificacao,atualizado_em_utc=now()
            WHERE tenant_id=@tenant AND departamento_id IS NULL AND lower(processo_executavel)=lower(@processo);
            """;
        await using var comandoAtualizar = new NpgsqlCommand(atualizar, conexao, transacao);
        comandoAtualizar.Parameters.AddWithValue("tenant", tenantId);
        comandoAtualizar.Parameters.AddWithValue("processo", processo.Trim());
        comandoAtualizar.Parameters.Add("categoria", NpgsqlDbType.Uuid).Value = (object?)categoriaId ?? DBNull.Value;
        comandoAtualizar.Parameters.AddWithValue("classificacao", classificacao);
        var alterados = await comandoAtualizar.ExecuteNonQueryAsync(ct);
        if (alterados == 0)
        {
            const string inserir = """
                INSERT INTO regras_aplicativos(tenant_id,departamento_id,processo_executavel,categoria_id,classificacao)
                VALUES(@tenant,NULL,@processo,@categoria,@classificacao);
                """;
            await using var comandoInserir = new NpgsqlCommand(inserir, conexao, transacao);
            comandoInserir.Parameters.AddWithValue("tenant", tenantId);
            comandoInserir.Parameters.AddWithValue("processo", processo.Trim());
            comandoInserir.Parameters.Add("categoria", NpgsqlDbType.Uuid).Value = (object?)categoriaId ?? DBNull.Value;
            comandoInserir.Parameters.AddWithValue("classificacao", classificacao);
            await comandoInserir.ExecuteNonQueryAsync(ct);
        }
        await transacao.CommitAsync(ct);
    }

    public async Task<IReadOnlyCollection<RegraSiteResumo>> ListarRegrasSitesAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id,dominio,categoria,classificacao
            FROM regras_sites
            WHERE tenant_id=@tenant AND departamento_id IS NULL
            ORDER BY lower(dominio);
            """;
        var lista = new List<RegraSiteResumo>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
            lista.Add(new RegraSiteResumo(leitor.GetGuid(0), leitor.GetString(1), leitor.IsDBNull(2) ? null : leitor.GetString(2), leitor.GetString(3)));
        return lista;
    }

    public async Task ClassificarSiteAsync(Guid tenantId, string dominio, string classificacao, string? categoria, CancellationToken ct = default)
    {
        dominio = NormalizarDominio(dominio) ?? throw new ArgumentException("Domínio inválido.", nameof(dominio));
        classificacao = NormalizarClassificacao(classificacao);
        // A regra global (departamento NULL) é atualizada primeiro; se não existir, é inserida.
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        const string atualizar = """
            UPDATE regras_sites SET categoria=@categoria,classificacao=@classificacao,atualizado_em_utc=now()
            WHERE tenant_id=@tenant AND departamento_id IS NULL AND lower(dominio)=lower(@dominio);
            """;
        await using var cmdAtualizar = new NpgsqlCommand(atualizar, conexao, transacao);
        cmdAtualizar.Parameters.AddWithValue("tenant", tenantId);
        cmdAtualizar.Parameters.AddWithValue("dominio", dominio);
        cmdAtualizar.Parameters.Add("categoria", NpgsqlDbType.Varchar).Value = (object?)Limpar(categoria) ?? DBNull.Value;
        cmdAtualizar.Parameters.AddWithValue("classificacao", classificacao);
        var alterados = await cmdAtualizar.ExecuteNonQueryAsync(ct);
        if (alterados == 0)
        {
            await using var cmdInserir = new NpgsqlCommand("INSERT INTO regras_sites(tenant_id,departamento_id,dominio,categoria,classificacao) VALUES(@tenant,NULL,@dominio,@categoria,@classificacao)", conexao, transacao);
            cmdInserir.Parameters.AddWithValue("tenant", tenantId);
            cmdInserir.Parameters.AddWithValue("dominio", dominio);
            cmdInserir.Parameters.Add("categoria", NpgsqlDbType.Varchar).Value = (object?)Limpar(categoria) ?? DBNull.Value;
            cmdInserir.Parameters.AddWithValue("classificacao", classificacao);
            await cmdInserir.ExecuteNonQueryAsync(ct);
        }
        await transacao.CommitAsync(ct);
    }

    public async Task RemoverRegraSiteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand("DELETE FROM regras_sites WHERE id=@id AND tenant_id=@tenant AND departamento_id IS NULL", conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("id", id);
        await comando.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyCollection<RegraLocalTrabalho>> ListarLocaisAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = "SELECT id,nome,faixa_ip::text,tipo FROM regras_local_trabalho WHERE tenant_id=@tenant AND ativo=true ORDER BY nome";
        var lista = new List<RegraLocalTrabalho>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct)) lista.Add(new RegraLocalTrabalho(leitor.GetGuid(0), leitor.GetString(1), leitor.GetString(2), leitor.GetString(3)));
        return lista;
    }

    public async Task AdicionarLocalAsync(Guid tenantId, string nome, string faixaIp, string tipo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(faixaIp)) throw new ArgumentException("Nome e faixa de IP são obrigatórios.");
        tipo = tipo is "Escritorio" or "HomeOffice" or "Externo" or "Outro" ? tipo : "Escritorio";
        const string sql = """
            INSERT INTO regras_local_trabalho(tenant_id,nome,faixa_ip,tipo)
            VALUES(@tenant,@nome,CAST(@faixa AS cidr),@tipo)
            ON CONFLICT(tenant_id,faixa_ip) DO UPDATE SET nome=EXCLUDED.nome,tipo=EXCLUDED.tipo,ativo=true;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("nome", nome.Trim());
        comando.Parameters.AddWithValue("faixa", faixaIp.Trim());
        comando.Parameters.AddWithValue("tipo", tipo);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string? NormalizarDominio(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var dominio = valor.Trim();
        if (Uri.TryCreate(dominio, UriKind.Absolute, out var uri)) dominio = uri.Host;
        dominio = dominio.Trim().Trim('.').ToLowerInvariant();
        if (dominio.StartsWith("www.", StringComparison.Ordinal)) dominio = dominio[4..];
        if (dominio.Length > 500 || dominio.Contains('/') || dominio.Contains('\\') || dominio.Contains(':') || dominio.Contains(' ')) return null;
        return Uri.CheckHostName(dominio) is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6 ? dominio : null;
    }

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string NormalizarClassificacao(string? valor)
        => valor?.Trim() switch
        {
            "Produtivo" => "Produtivo",
            "Atencao" => "Atencao",
            _ => "Neutro"
        };
}

public sealed record CategoriaAtividadeBasica(Guid Id, string Nome, string ClassificacaoPadrao);
public sealed record RegraLocalTrabalho(Guid Id, string Nome, string FaixaIp, string Tipo);
