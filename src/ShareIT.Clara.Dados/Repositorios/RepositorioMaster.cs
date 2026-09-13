using System.Globalization;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using ShareIT.Clara.Comum.Seguranca;
using ShareIT.Clara.Dados.Banco;
using ShareIT.Clara.Dominio.Contratos;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: administrar tenants/clientes a partir do perfil Master da Share IT.
/// Origem: área Master do Share IT .Clara.
/// Destino: tenants, empresas, perfis, módulos, integrações e usuário administrador inicial do cliente.
/// Dependências: PostgreSQL e SenhaSegura.
/// Impacto: permite operar o .Clara como SaaS multiempresa com isolamento por tenant.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioMaster(FabricaConexoesClara fabrica)
{
    public async Task<IReadOnlyCollection<EmpresaMasterResumo>> ListarEmpresasAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT t.id,e.id,e.nome,t.identificador,e.documento,
                   (SELECT count(*) FROM usuarios u WHERE u.tenant_id=t.id AND u.ativo=true),
                   (SELECT count(*) FROM ativos a WHERE a.tenant_id=t.id AND a.ativo=true),
                   t.ativo AND e.ativo,
                   COALESCE(a.plano,CASE WHEN t.identificador='shareit' THEN 'Interno' ELSE 'Gestão' END),
                   COALESCE(a.limite_estacoes,15),
                   COALESCE(a.status,'Ativa'),
                   a.vencimento,
                   COALESCE(a.retencao_dias,90),
                   COALESCE(a.armazenamento_gb,5),
                   a.observacao
            FROM tenants t
            JOIN LATERAL (
                SELECT e1.* FROM empresas e1 WHERE e1.tenant_id=t.id ORDER BY e1.criado_em_utc LIMIT 1
            ) e ON true
            LEFT JOIN assinaturas_tenant a ON a.tenant_id=t.id
            ORDER BY CASE WHEN t.identificador='shareit' THEN 0 ELSE 1 END,e.nome;
            """;
        var lista = new List<EmpresaMasterResumo>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
        {
            lista.Add(new EmpresaMasterResumo(
                leitor.GetGuid(0), leitor.GetGuid(1), leitor.GetString(2), leitor.GetString(3),
                leitor.IsDBNull(4) ? null : leitor.GetString(4), Convert.ToInt32(leitor.GetInt64(5)), Convert.ToInt32(leitor.GetInt64(6)), leitor.GetBoolean(7),
                leitor.GetString(8), leitor.GetInt32(9), leitor.GetString(10), leitor.IsDBNull(11) ? null : leitor.GetFieldValue<DateOnly>(11),
                leitor.GetInt32(12), leitor.GetInt32(13), leitor.IsDBNull(14) ? null : leitor.GetString(14)));
        }
        return lista;
    }

    public async Task<(Guid TenantId, Guid EmpresaId)> CriarEmpresaAsync(
        string nomeEmpresa,
        string? documento,
        string nomeAdministrador,
        string emailAdministrador,
        string senhaAdministrador,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nomeEmpresa)) throw new ArgumentException("Nome da empresa é obrigatório.");
        if (string.IsNullOrWhiteSpace(nomeAdministrador)) throw new ArgumentException("Nome do administrador é obrigatório.");
        if (string.IsNullOrWhiteSpace(emailAdministrador)) throw new ArgumentException("E-mail do administrador é obrigatório.");
        if (string.IsNullOrWhiteSpace(senhaAdministrador) || senhaAdministrador.Length < 8) throw new ArgumentException("A senha do administrador deve possuir pelo menos 8 caracteres.");

        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        var identificador = await CriarIdentificadorUnicoAsync(conexao, transacao, nomeEmpresa, ct);
        var tenantId = Guid.NewGuid();
        var empresaId = Guid.NewGuid();

        const string inserirTenant = "INSERT INTO tenants(id,nome,identificador) VALUES(@id,@nome,@identificador)";
        await using (var cmd = new NpgsqlCommand(inserirTenant, conexao, transacao))
        {
            cmd.Parameters.AddWithValue("id", tenantId);
            cmd.Parameters.AddWithValue("nome", nomeEmpresa.Trim());
            cmd.Parameters.AddWithValue("identificador", identificador);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        const string inserirEmpresa = "INSERT INTO empresas(id,tenant_id,nome,documento) VALUES(@id,@tenant,@nome,@documento)";
        await using (var cmd = new NpgsqlCommand(inserirEmpresa, conexao, transacao))
        {
            cmd.Parameters.AddWithValue("id", empresaId);
            cmd.Parameters.AddWithValue("tenant", tenantId);
            cmd.Parameters.AddWithValue("nome", nomeEmpresa.Trim());
            cmd.Parameters.AddWithValue("documento", (object?)Limpar(documento) ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await CriarEstruturaPadraoAsync(conexao, transacao, tenantId, ct);
        await using (var assinatura = new NpgsqlCommand("INSERT INTO assinaturas_tenant(tenant_id,plano,status,limite_estacoes,retencao_dias,armazenamento_gb) VALUES(@tenant,'Gestão','Ativa',15,90,5) ON CONFLICT(tenant_id) DO NOTHING", conexao, transacao))
        {
            assinatura.Parameters.AddWithValue("tenant", tenantId);
            await assinatura.ExecuteNonQueryAsync(ct);
        }
        var perfilAdmin = await ObterPerfilAsync(conexao, transacao, tenantId, "ADMINISTRADOR", ct);
        var (hash, sal) = SenhaSegura.Criar(senhaAdministrador);
        const string inserirUsuario = """
            INSERT INTO usuarios(tenant_id,empresa_id,nome,email,senha_hash,senha_sal,perfil,perfil_acesso_id)
            VALUES(@tenant,@empresa,@nome,@email,@hash,@sal,'Administrador',@perfil);
            """;
        await using (var cmd = new NpgsqlCommand(inserirUsuario, conexao, transacao))
        {
            cmd.Parameters.AddWithValue("tenant", tenantId);
            cmd.Parameters.AddWithValue("empresa", empresaId);
            cmd.Parameters.AddWithValue("nome", nomeAdministrador.Trim());
            cmd.Parameters.AddWithValue("email", emailAdministrador.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("hash", hash);
            cmd.Parameters.AddWithValue("sal", sal);
            cmd.Parameters.AddWithValue("perfil", perfilAdmin);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await transacao.CommitAsync(ct);
        return (tenantId, empresaId);
    }


    public async Task AtualizarAssinaturaAsync(
        Guid tenantId,
        string plano,
        string status,
        int limiteEstacoes,
        int retencaoDias,
        int armazenamentoGb,
        DateOnly? vencimento,
        string? observacao,
        CancellationToken ct = default)
    {
        plano = string.IsNullOrWhiteSpace(plano) ? "Gestão" : plano.Trim();
        status = status is "Ativa" or "Trial" or "Suspensa" or "Cancelada" ? status : "Ativa";
        limiteEstacoes = Math.Clamp(limiteEstacoes, 1, 100000);
        retencaoDias = Math.Clamp(retencaoDias, 1, 3650);
        armazenamentoGb = Math.Clamp(armazenamentoGb, 1, 100000);

        const string sql = """
            INSERT INTO assinaturas_tenant(tenant_id,plano,status,limite_estacoes,retencao_dias,armazenamento_gb,vencimento,observacao,atualizado_em_utc)
            SELECT t.id,@plano,@status,@limite,@retencao,@armazenamento,@vencimento,@observacao,now()
            FROM tenants t WHERE t.id=@tenant
            ON CONFLICT(tenant_id) DO UPDATE SET
                plano=EXCLUDED.plano,
                status=EXCLUDED.status,
                limite_estacoes=EXCLUDED.limite_estacoes,
                retencao_dias=EXCLUDED.retencao_dias,
                armazenamento_gb=EXCLUDED.armazenamento_gb,
                vencimento=EXCLUDED.vencimento,
                observacao=EXCLUDED.observacao,
                atualizado_em_utc=now();
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("plano", plano);
        comando.Parameters.AddWithValue("status", status);
        comando.Parameters.AddWithValue("limite", limiteEstacoes);
        comando.Parameters.AddWithValue("retencao", retencaoDias);
        comando.Parameters.AddWithValue("armazenamento", armazenamentoGb);
        comando.Parameters.Add("vencimento", NpgsqlDbType.Date).Value = (object?)vencimento ?? DBNull.Value;
        comando.Parameters.Add("observacao", NpgsqlDbType.Text).Value = (object?)Limpar(observacao) ?? DBNull.Value;
        if (await comando.ExecuteNonQueryAsync(ct) == 0)
            throw new InvalidOperationException("Tenant não encontrado.");
    }

    private static async Task CriarEstruturaPadraoAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid tenantId, CancellationToken ct)
    {
        const string perfis = """
            INSERT INTO perfis_acesso(tenant_id,codigo,nome,padrao,sistema) VALUES
            (@tenant,'ADMINISTRADOR','Administrador',false,true),
            (@tenant,'PADRAO','Padrão',true,true)
            ON CONFLICT(tenant_id,codigo) DO NOTHING;
            """;
        await using (var cmd = new NpgsqlCommand(perfis, c, t))
        {
            cmd.Parameters.AddWithValue("tenant", tenantId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        const string modulosAdmin = """
            INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
            SELECT p.id,m.codigo,true FROM perfis_acesso p CROSS JOIN modulos m
            WHERE p.tenant_id=@tenant AND p.codigo='ADMINISTRADOR' AND m.ativo=true
            ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();
            """;
        await using (var cmd = new NpgsqlCommand(modulosAdmin, c, t))
        {
            cmd.Parameters.AddWithValue("tenant", tenantId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        const string modulosPadrao = """
            INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
            SELECT p.id,m.codigo,true FROM perfis_acesso p JOIN modulos m
              ON m.codigo IN ('agora','estacoes','aplicativos','locais','integracoes','indice-geral')
            WHERE p.tenant_id=@tenant AND p.codigo='PADRAO'
            ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();
            """;
        await using (var cmd = new NpgsqlCommand(modulosPadrao, c, t))
        {
            cmd.Parameters.AddWithValue("tenant", tenantId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        const string categorias = """
            INSERT INTO categorias_atividade(tenant_id,nome,classificacao)
            SELECT @tenant,x.nome,x.classificacao FROM (VALUES
              ('Trabalho','Produtivo'),('Comunicacao','Neutro'),('Desenvolvimento','Produtivo'),
              ('Administrativo','Produtivo'),('Navegacao','Neutro'),('Entretenimento','Atencao'),('Nao classificado','Neutro')
            ) AS x(nome,classificacao)
            ON CONFLICT DO NOTHING;
            """;
        await using (var cmd = new NpgsqlCommand(categorias, c, t))
        {
            cmd.Parameters.AddWithValue("tenant", tenantId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        const string integracoes = """
            INSERT INTO integracoes(tenant_id,provedor,nome_exibicao,status)
            SELECT @tenant,x.provedor,x.nome,'NaoConfigurada' FROM (VALUES
              ('Microsoft','Microsoft 365 / Outlook / Teams'),
              ('Google','Google Workspace / Gmail / Meet'),
              ('Zoom','Zoom')
            ) AS x(provedor,nome)
            ON CONFLICT DO NOTHING;
            """;
        await using var integrar = new NpgsqlCommand(integracoes, c, t);
        integrar.Parameters.AddWithValue("tenant", tenantId);
        await integrar.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Guid> ObterPerfilAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid tenantId, string codigo, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT id FROM perfis_acesso WHERE tenant_id=@tenant AND codigo=@codigo", c, t);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("codigo", codigo);
        return (Guid)(await cmd.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Perfil padrão não encontrado."));
    }

    private static async Task<string> CriarIdentificadorUnicoAsync(NpgsqlConnection c, NpgsqlTransaction t, string nome, CancellationToken ct)
    {
        var baseId = RemoverAcentos(nome).ToLowerInvariant();
        var sb = new StringBuilder();
        foreach (var ch in baseId)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var candidato = sb.ToString().Trim('-');
        if (string.IsNullOrWhiteSpace(candidato)) candidato = "empresa";
        candidato = candidato.Length > 70 ? candidato[..70] : candidato;
        var original = candidato;
        var sufixo = 1;
        while (true)
        {
            await using var cmd = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM tenants WHERE identificador=@id)", c, t);
            cmd.Parameters.AddWithValue("id", candidato);
            var existe = (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
            if (!existe) return candidato;
            candidato = $"{original}-{++sufixo}";
        }
    }

    private static string RemoverAcentos(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        return new string(normalizado.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    }

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
