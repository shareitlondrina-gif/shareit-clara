using Npgsql;
using ShareIT.Clara.Comum.Seguranca;
using ShareIT.Clara.Dados.Banco;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: autenticar usuários e carregar tenant, perfil e módulos liberados.
/// Origem: formulário de login Web.
/// Destino: cookie autenticado do ASP.NET Core.
/// Dependências: PostgreSQL e SenhaSegura.
/// Impacto: controla o isolamento por tenant e a autorização por módulo.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioAutenticacao(FabricaConexoesClara fabrica)
{
    public async Task<UsuarioAutenticado?> AutenticarAsync(string email, string senha, CancellationToken ct = default)
    {
        const string sql = """
            SELECT u.id,u.tenant_id,u.empresa_id,u.nome,u.email,u.senha_hash,u.senha_sal,u.perfil,u.perfil_acesso_id
            FROM usuarios u
            WHERE lower(u.email)=lower(@email) AND u.ativo=true
            LIMIT 1;
            """;

        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        Guid id;
        Guid tenantId;
        Guid? empresaId;
        Guid? perfilAcessoId;
        string nome;
        string emailBanco;
        string hash;
        string sal;
        string perfil;

        await using (var comando = new NpgsqlCommand(sql, conexao))
        {
            comando.Parameters.AddWithValue("email", email.Trim());
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            if (!await leitor.ReadAsync(ct)) return null;
            id = leitor.GetGuid(0);
            tenantId = leitor.GetGuid(1);
            empresaId = leitor.IsDBNull(2) ? null : leitor.GetGuid(2);
            nome = leitor.GetString(3);
            emailBanco = leitor.GetString(4);
            hash = leitor.GetString(5);
            sal = leitor.GetString(6);
            perfil = leitor.GetString(7);
            perfilAcessoId = leitor.IsDBNull(8) ? null : leitor.GetGuid(8);
        }

        if (!SenhaSegura.Validar(senha, hash, sal)) return null;
        var modulos = await ObterModulosAsync(conexao, perfil, perfilAcessoId, ct);
        return new UsuarioAutenticado(id, tenantId, empresaId, nome, emailBanco, perfil, modulos);
    }

    private static async Task<IReadOnlyCollection<string>> ObterModulosAsync(NpgsqlConnection conexao, string perfil, Guid? perfilAcessoId, CancellationToken ct)
    {
        var lista = new List<string>();
        if (perfil.Equals("Master", StringComparison.OrdinalIgnoreCase))
        {
            await using var todos = new NpgsqlCommand("SELECT codigo FROM modulos WHERE ativo=true ORDER BY ordem", conexao);
            await using var leitorTodos = await todos.ExecuteReaderAsync(ct);
            while (await leitorTodos.ReadAsync(ct)) lista.Add(leitorTodos.GetString(0));
            lista.Add("master-empresas");
            return lista;
        }

        if (perfilAcessoId is null) return lista;
        const string sql = """
            SELECT pm.modulo_codigo
            FROM perfil_modulos pm
            JOIN modulos m ON m.codigo=pm.modulo_codigo AND m.ativo=true
            WHERE pm.perfil_id=@perfil AND pm.permitido=true
            ORDER BY m.ordem;
            """;
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("perfil", perfilAcessoId.Value);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct)) lista.Add(leitor.GetString(0));
        return lista;
    }
}

public sealed record UsuarioAutenticado(
    Guid Id,
    Guid TenantId,
    Guid? EmpresaId,
    string Nome,
    string Email,
    string Perfil,
    IReadOnlyCollection<string> Modulos);
