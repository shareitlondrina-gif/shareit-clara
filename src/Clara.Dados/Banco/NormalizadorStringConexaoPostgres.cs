// Clara
// Programador: Paulo Andrade
// Objetivo: persistir e recuperar dados da Clara.
// Origem: camada de aplicação/domínio.
// Destino: PostgreSQL e contratos de domínio.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using Npgsql;

namespace Clara.Dados.Banco;

/// <summary>
/// Objetivo: normalizar a conexão PostgreSQL recebida pela Clara, aceitando formato Npgsql ou URL postgres/postgresql.
/// Origem: variável CLARA_POSTGRES configurada no ambiente local ou no Render.
/// Destino: FabricaConexoesClara e preparação automática do banco.
/// Dependências: Npgsql e System.Uri.
/// Impacto: permite usar diretamente a URL interna fornecida pelo provedor sem espalhar conversões pela solução.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public static class NormalizadorStringConexaoPostgres
{
    public static NpgsqlConnectionStringBuilder Criar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException("String de conexão PostgreSQL não informada.");

        valor = valor.Trim();
        if (Uri.TryCreate(valor, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase)))
        {
            var partesUsuario = uri.UserInfo.Split(':', 2);
            var usuario = partesUsuario.Length > 0 ? Uri.UnescapeDataString(partesUsuario[0]) : string.Empty;
            var senha = partesUsuario.Length > 1 ? Uri.UnescapeDataString(partesUsuario[1]) : string.Empty;
            var banco = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));

            if (string.IsNullOrWhiteSpace(uri.Host) || string.IsNullOrWhiteSpace(usuario))
                throw new InvalidOperationException("URL PostgreSQL inválida: host e usuário são obrigatórios.");

            return new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
                Database = banco,
                Username = usuario,
                Password = senha,
                Pooling = true
            };
        }

        return new NpgsqlConnectionStringBuilder(valor);
    }
}
