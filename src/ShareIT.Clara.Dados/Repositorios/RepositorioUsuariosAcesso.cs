using Npgsql;
using ShareIT.Clara.Comum.Seguranca;
using ShareIT.Clara.Dados.Banco;
using ShareIT.Clara.Dominio.Contratos;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: administrar usuários do tenant e o perfil padrão de acesso por módulos.
/// Origem: administrador da empresa.
/// Destino: usuarios, perfis_acesso e perfil_modulos.
/// Dependências: PostgreSQL e SenhaSegura.
/// Impacto: permite delegar acesso à plataforma sem compartilhar credenciais administrativas.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioUsuariosAcesso(FabricaConexoesClara fabrica)
{
    public async Task<PerfilUsuarioResumo> ObterPerfilUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT u.id,u.nome,u.email,u.perfil,p.nome,t.nome,e.nome,u.telefone,u.cargo,(u.foto_perfil IS NOT NULL)
            FROM usuarios u
            JOIN tenants t ON t.id=u.tenant_id
            LEFT JOIN empresas e ON e.id=u.empresa_id AND e.tenant_id=u.tenant_id
            LEFT JOIN perfis_acesso p ON p.id=u.perfil_acesso_id AND p.tenant_id=u.tenant_id
            WHERE u.id=@usuario AND u.ativo=true
            LIMIT 1;
            """;
        await using var c = fabrica.Criar();
        await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("usuario", usuarioId);
        await using var leitor = await cmd.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct)) throw new InvalidOperationException("Usuário não encontrado.");
        return new PerfilUsuarioResumo(
            leitor.GetGuid(0), leitor.GetString(1), leitor.GetString(2), leitor.GetString(3),
            leitor.IsDBNull(4) ? null : leitor.GetString(4), leitor.GetString(5),
            leitor.IsDBNull(6) ? null : leitor.GetString(6), leitor.IsDBNull(7) ? null : leitor.GetString(7),
            leitor.IsDBNull(8) ? null : leitor.GetString(8), leitor.GetBoolean(9));
    }

    public async Task SalvarPerfilUsuarioAsync(Guid usuarioId, string nome, string? telefone, string? cargo, CancellationToken ct = default)
    {
        nome = (nome ?? string.Empty).Trim();
        if (nome.Length < 2 || nome.Length > 200) throw new ArgumentException("Informe um nome válido, com até 200 caracteres.");
        telefone = Limpar(telefone, 30);
        cargo = Limpar(cargo, 120);
        const string sql = """
            UPDATE usuarios
            SET nome=@nome,telefone=@telefone,cargo=@cargo,perfil_atualizado_em_utc=now()
            WHERE id=@usuario AND ativo=true;
            """;
        await using var c = fabrica.Criar();
        await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("usuario", usuarioId);
        cmd.Parameters.AddWithValue("nome", nome);
        cmd.Parameters.AddWithValue("telefone", (object?)telefone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cargo", (object?)cargo ?? DBNull.Value);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Usuário não encontrado.");
    }

    public async Task SalvarFotoPerfilAsync(Guid usuarioId, byte[] conteudo, string mimeType, CancellationToken ct = default)
    {
        if (conteudo.Length == 0 || conteudo.Length > 2 * 1024 * 1024) throw new ArgumentException("A foto deve possuir no máximo 2 MB.");
        if (mimeType is not ("image/jpeg" or "image/png" or "image/webp")) throw new ArgumentException("Formato de foto não permitido.");
        const string sql = """
            UPDATE usuarios SET foto_perfil=@foto,foto_perfil_mime=@mime,perfil_atualizado_em_utc=now()
            WHERE id=@usuario AND ativo=true;
            """;
        await using var c = fabrica.Criar();
        await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("usuario", usuarioId);
        cmd.Parameters.AddWithValue("foto", conteudo);
        cmd.Parameters.AddWithValue("mime", mimeType);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Usuário não encontrado.");
    }

    public async Task RemoverFotoPerfilAsync(Guid usuarioId, CancellationToken ct = default)
    {
        await using var c = fabrica.Criar();
        await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("UPDATE usuarios SET foto_perfil=NULL,foto_perfil_mime=NULL,perfil_atualizado_em_utc=now() WHERE id=@usuario", c);
        cmd.Parameters.AddWithValue("usuario", usuarioId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<FotoPerfilUsuario?> ObterFotoPerfilAsync(Guid usuarioId, CancellationToken ct = default)
    {
        await using var c = fabrica.Criar();
        await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT foto_perfil,foto_perfil_mime FROM usuarios WHERE id=@usuario AND ativo=true", c);
        cmd.Parameters.AddWithValue("usuario", usuarioId);
        await using var leitor = await cmd.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct) || leitor.IsDBNull(0) || leitor.IsDBNull(1)) return null;
        return new FotoPerfilUsuario((byte[])leitor[0], leitor.GetString(1));
    }

    private static string? Limpar(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var limpo = valor.Trim();
        return limpo.Length <= maximo ? limpo : limpo[..maximo];
    }
    public async Task<IReadOnlyCollection<UsuarioAcessoResumo>> ListarUsuariosAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT u.id,u.nome,u.email,u.perfil,p.id,p.nome,u.ativo,u.criado_em_utc
            FROM usuarios u
            LEFT JOIN perfis_acesso p ON p.id=u.perfil_acesso_id AND p.tenant_id=u.tenant_id
            WHERE u.tenant_id=@tenant
            ORDER BY CASE u.perfil WHEN 'Administrador' THEN 0 ELSE 1 END,u.nome;
            """;
        var lista = new List<UsuarioAcessoResumo>();
        await using var c = fabrica.Criar();
        await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await cmd.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
        {
            lista.Add(new UsuarioAcessoResumo(
                leitor.GetGuid(0), leitor.GetString(1), leitor.GetString(2), leitor.GetString(3),
                leitor.IsDBNull(4) ? null : leitor.GetGuid(4), leitor.IsDBNull(5) ? null : leitor.GetString(5), leitor.GetBoolean(6),
                new DateTimeOffset(DateTime.SpecifyKind(leitor.GetDateTime(7), DateTimeKind.Utc))));
        }
        return lista;
    }

    public async Task CriarUsuarioAsync(Guid tenantId, string nome, string email, string senha, string perfilCodigo = "PADRAO", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("E-mail é obrigatório.");
        if (string.IsNullOrWhiteSpace(senha) || senha.Length < 8) throw new ArgumentException("A senha deve possuir pelo menos 8 caracteres.");

        await using var c = fabrica.Criar();
        await c.OpenAsync(ct);
        await using var t = await c.BeginTransactionAsync(ct);

        var codigoNormalizado = NormalizarPerfilSelecionavel(perfilCodigo);
        var perfilId = await ObterPerfilPorCodigoAsync(c, t, tenantId, codigoNormalizado, ct);
        var empresaId = await ObterEmpresaPrincipalAsync(c, t, tenantId, ct);
        var (hash, sal) = SenhaSegura.Criar(senha);
        const string sql = """
            INSERT INTO usuarios(tenant_id,empresa_id,nome,email,senha_hash,senha_sal,perfil,perfil_acesso_id)
            VALUES(@tenant,@empresa,@nome,@email,@hash,@sal,'Usuario',@perfil);
            """;
        await using var cmd = new NpgsqlCommand(sql, c, t);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("empresa", empresaId);
        cmd.Parameters.AddWithValue("nome", nome.Trim());
        cmd.Parameters.AddWithValue("email", email.Trim().ToLowerInvariant());
        cmd.Parameters.AddWithValue("hash", hash);
        cmd.Parameters.AddWithValue("sal", sal);
        cmd.Parameters.AddWithValue("perfil", perfilId);
        await cmd.ExecuteNonQueryAsync(ct);
        await t.CommitAsync(ct);
    }

    public async Task AtualizarPerfilUsuarioAsync(Guid tenantId, Guid usuarioId, string perfilCodigo, CancellationToken ct = default)
    {
        var codigoNormalizado = NormalizarPerfilSelecionavel(perfilCodigo);
        await using var c = fabrica.Criar();
        await c.OpenAsync(ct);
        await using var t = await c.BeginTransactionAsync(ct);

        var perfilId = await ObterPerfilPorCodigoAsync(c, t, tenantId, codigoNormalizado, ct);
        const string sql = """
            UPDATE usuarios
            SET perfil_acesso_id=@perfil
            WHERE id=@usuario AND tenant_id=@tenant AND perfil <> 'Administrador';
            """;
        await using var cmd = new NpgsqlCommand(sql, c, t);
        cmd.Parameters.AddWithValue("perfil", perfilId);
        cmd.Parameters.AddWithValue("usuario", usuarioId);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException("Usuário não encontrado ou perfil Administrador não pode ser alterado por esta ação.");
        await t.CommitAsync(ct);
    }

    public async Task<IReadOnlyCollection<PerfilAcessoResumo>> ListarPerfisAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.id,p.codigo,p.nome,p.padrao,p.sistema,m.codigo,m.nome,m.grupo,COALESCE(pm.permitido,false)
            FROM perfis_acesso p
            CROSS JOIN modulos m
            LEFT JOIN perfil_modulos pm ON pm.perfil_id=p.id AND pm.modulo_codigo=m.codigo
            WHERE p.tenant_id=@tenant AND p.ativo=true AND m.ativo=true
            ORDER BY CASE p.codigo WHEN 'ADMINISTRADOR' THEN 0 WHEN 'PADRAO' THEN 1 ELSE 2 END,p.nome,m.ordem;
            """;
        var mapa = new Dictionary<Guid, (string Codigo, string Nome, bool Padrao, bool Sistema, List<ModuloAcessoResumo> Modulos)>();
        await using var c = fabrica.Criar();
        await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await cmd.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
        {
            var id = leitor.GetGuid(0);
            if (!mapa.TryGetValue(id, out var valor))
            {
                valor = (leitor.GetString(1), leitor.GetString(2), leitor.GetBoolean(3), leitor.GetBoolean(4), new List<ModuloAcessoResumo>());
                mapa[id] = valor;
            }
            valor.Modulos.Add(new ModuloAcessoResumo(leitor.GetString(5), leitor.GetString(6), leitor.GetString(7), leitor.GetBoolean(8)));
        }
        return mapa.Select(x => new PerfilAcessoResumo(x.Key, x.Value.Codigo, x.Value.Nome, x.Value.Padrao, x.Value.Sistema, x.Value.Modulos)).ToArray();
    }

    public async Task AtualizarModulosPerfilAsync(Guid tenantId, Guid perfilId, IEnumerable<string> modulosPermitidos, CancellationToken ct = default)
    {
        var permitidos = modulosPermitidos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        await using var c = fabrica.Criar();
        await c.OpenAsync(ct);
        await using var t = await c.BeginTransactionAsync(ct);

        await using (var validar = new NpgsqlCommand("SELECT codigo FROM perfis_acesso WHERE id=@perfil AND tenant_id=@tenant AND ativo=true", c, t))
        {
            validar.Parameters.AddWithValue("perfil", perfilId);
            validar.Parameters.AddWithValue("tenant", tenantId);
            var codigo = Convert.ToString(await validar.ExecuteScalarAsync(ct));
            if (string.IsNullOrWhiteSpace(codigo)) throw new InvalidOperationException("Perfil não encontrado.");
            if (codigo == "ADMINISTRADOR") throw new InvalidOperationException("O perfil Administrador é de sistema e mantém acesso administrativo completo.");
        }

        const string sql = """
            INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
            SELECT @perfil,m.codigo,@permitido FROM modulos m WHERE m.codigo=@modulo AND m.ativo=true
            ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=EXCLUDED.permitido,atualizado_em_utc=now();
            """;
        foreach (var modulo in await ListarCodigosModulosAsync(c, t, ct))
        {
            await using var cmd = new NpgsqlCommand(sql, c, t);
            cmd.Parameters.AddWithValue("perfil", perfilId);
            cmd.Parameters.AddWithValue("modulo", modulo);
            cmd.Parameters.AddWithValue("permitido", permitidos.Contains(modulo));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await t.CommitAsync(ct);
    }

    private static string NormalizarPerfilSelecionavel(string? perfilCodigo)
    {
        var codigo = (perfilCodigo ?? "PADRAO").Trim().ToUpperInvariant();
        return codigo switch
        {
            "PADRAO" => "PADRAO",
            "SUPERVISOR" => "SUPERVISOR",
            _ => throw new ArgumentException("Perfil inválido. Selecione Padrão ou Supervisor.")
        };
    }

    private static async Task<Guid> ObterPerfilPorCodigoAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid tenantId, string codigo, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT id FROM perfis_acesso WHERE tenant_id=@tenant AND codigo=@codigo AND ativo=true LIMIT 1", c, t);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("codigo", codigo);
        return (Guid)(await cmd.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException($"Perfil {codigo} não configurado para a empresa."));
    }

    private static async Task<Guid> ObterEmpresaPrincipalAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid tenantId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT id FROM empresas WHERE tenant_id=@tenant AND ativo=true ORDER BY criado_em_utc LIMIT 1", c, t);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Empresa principal não encontrada."));
    }

    private static async Task<IReadOnlyCollection<string>> ListarCodigosModulosAsync(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        var lista = new List<string>();
        await using var cmd = new NpgsqlCommand("SELECT codigo FROM modulos WHERE ativo=true ORDER BY ordem", c, t);
        await using var leitor = await cmd.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct)) lista.Add(leitor.GetString(0));
        return lista;
    }
}
