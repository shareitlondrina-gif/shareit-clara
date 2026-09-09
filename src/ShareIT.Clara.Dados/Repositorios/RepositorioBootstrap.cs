using Npgsql;
using ShareIT.Clara.Comum.Configuracao;
using ShareIT.Clara.Comum.Seguranca;
using ShareIT.Clara.Dados.Banco;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: preparar o tenant e o administrador inicial somente quando a base ainda estiver vazia.
/// Origem: configuração de bootstrap do servidor.
/// Destino: tenants, empresas e usuarios.
/// Dependências: PostgreSQL e variável de ambiente da senha inicial.
/// Impacto: permite primeira publicação automatizada sem credenciais fixas no código.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioBootstrap(FabricaConexoesClara fabrica, ConfiguracaoClara configuracao)
{
    public async Task GarantirAsync(CancellationToken ct = default)
    {
        var senha = Environment.GetEnvironmentVariable(configuracao.Bootstrap.SenhaAdministradorVariavel);
        if (string.IsNullOrWhiteSpace(senha)) return;

        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        var tenantId = await ObterOuCriarTenantAsync(conexao, transacao, ct);
        await GarantirEmpresaInicialAsync(conexao, transacao, tenantId, ct);
        await GarantirAdministradorAsync(conexao, transacao, tenantId, senha, ct);
        await GarantirContextoInicialAsync(conexao, transacao, tenantId, ct);
        await transacao.CommitAsync(ct);
    }

    private async Task<Guid> ObterOuCriarTenantAsync(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO tenants(nome, identificador)
            VALUES(@nome,@identificador)
            ON CONFLICT(identificador) DO UPDATE SET nome=EXCLUDED.nome
            RETURNING id;
            """;
        await using var cmd = new NpgsqlCommand(sql, c, t);
        cmd.Parameters.AddWithValue("nome", configuracao.Bootstrap.NomeTenant);
        cmd.Parameters.AddWithValue("identificador", configuracao.Bootstrap.IdentificadorTenant);
        return (Guid)(await cmd.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Falha ao criar tenant inicial."));
    }

    private async Task GarantirEmpresaInicialAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid tenantId, CancellationToken ct)
    {
        const string existe = "SELECT EXISTS(SELECT 1 FROM empresas WHERE tenant_id=@tenant)";
        await using var verificar = new NpgsqlCommand(existe, c, t);
        verificar.Parameters.AddWithValue("tenant", tenantId);
        if ((bool)(await verificar.ExecuteScalarAsync(ct) ?? false)) return;

        await using var criar = new NpgsqlCommand("INSERT INTO empresas(tenant_id,nome) VALUES(@tenant,@nome)", c, t);
        criar.Parameters.AddWithValue("tenant", tenantId);
        criar.Parameters.AddWithValue("nome", configuracao.Bootstrap.NomeTenant);
        await criar.ExecuteNonQueryAsync(ct);
    }

    private static async Task GarantirContextoInicialAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid tenantId, CancellationToken ct)
    {
        const string categorias = """
            INSERT INTO categorias_atividade(tenant_id,nome,classificacao)
            SELECT @tenant,x.nome,x.classificacao
            FROM (VALUES
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
            SELECT @tenant,x.provedor,x.nome,'NaoConfigurada'
            FROM (VALUES
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

    private async Task GarantirAdministradorAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid tenantId, string senha, CancellationToken ct)
    {
        const string existe = "SELECT EXISTS(SELECT 1 FROM usuarios WHERE tenant_id=@tenant AND lower(email)=lower(@email))";
        await using var verificar = new NpgsqlCommand(existe, c, t);
        verificar.Parameters.AddWithValue("tenant", tenantId);
        verificar.Parameters.AddWithValue("email", configuracao.Bootstrap.EmailAdministrador);
        var existeUsuario = (bool)(await verificar.ExecuteScalarAsync(ct) ?? false);

        if (!existeUsuario)
        {
            var (hash, sal) = SenhaSegura.Criar(senha);
            const string inserir = """
                INSERT INTO usuarios(tenant_id,nome,email,senha_hash,senha_sal,perfil)
                VALUES(@tenant,@nome,@email,@hash,@sal,'Master');
                """;
            await using var cmd = new NpgsqlCommand(inserir, c, t);
            cmd.Parameters.AddWithValue("tenant", tenantId);
            cmd.Parameters.AddWithValue("nome", configuracao.Bootstrap.NomeAdministrador);
            cmd.Parameters.AddWithValue("email", configuracao.Bootstrap.EmailAdministrador);
            cmd.Parameters.AddWithValue("hash", hash);
            cmd.Parameters.AddWithValue("sal", sal);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string promover = """
                UPDATE usuarios
                SET perfil='Master'
                WHERE tenant_id=@tenant AND lower(email)=lower(@email);
                """;
            await using var cmd = new NpgsqlCommand(promover, c, t);
            cmd.Parameters.AddWithValue("tenant", tenantId);
            cmd.Parameters.AddWithValue("email", configuracao.Bootstrap.EmailAdministrador);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
