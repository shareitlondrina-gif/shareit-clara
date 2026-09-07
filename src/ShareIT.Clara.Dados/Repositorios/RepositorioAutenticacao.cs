using Npgsql;
using ShareIT.Clara.Comum.Seguranca;
using ShareIT.Clara.Dados.Banco;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: autenticar usuários administrativos e fornecer identidade de tenant/perfil.
/// Origem: formulário de login Web.
/// Destino: cookie autenticado do ASP.NET Core.
/// Dependências: PostgreSQL e SenhaSegura.
/// Impacto: controla acesso administrativo ao dashboard e às operações de instalação.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioAutenticacao(FabricaConexoesClara fabrica)
{
    public async Task<UsuarioAutenticado?> AutenticarAsync(string email, string senha, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, tenant_id, nome, email, senha_hash, senha_sal, perfil
            FROM usuarios
            WHERE lower(email)=lower(@email) AND ativo=true
            LIMIT 1;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("email", email.Trim());
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct)) return null;

        var hash = leitor.GetString(4);
        var sal = leitor.GetString(5);
        if (!SenhaSegura.Validar(senha, hash, sal)) return null;

        return new UsuarioAutenticado(
            leitor.GetGuid(0), leitor.GetGuid(1), leitor.GetString(2), leitor.GetString(3), leitor.GetString(6));
    }
}

public sealed record UsuarioAutenticado(Guid Id, Guid TenantId, string Nome, string Email, string Perfil);
