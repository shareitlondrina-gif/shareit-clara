using Npgsql;
using ShareIT.Clara.Comum.Configuracao;

namespace ShareIT.Clara.Dados.Banco;

/// <summary>
/// Objetivo: criar conexões PostgreSQL do Share IT .Clara e separar a conexão de origem da conexão exclusiva do banco shareit_clara.
/// Origem: ConfiguracaoClara e variável de ambiente definida em ConfigSys/Render.
/// Destino: preparador de banco, repositórios e migrador de banco.
/// Dependências: Npgsql e NormalizadorStringConexaoPostgres.
/// Impacto: permite compartilhar a mesma instância PostgreSQL do .VISÃO sem executar migrations ou consultas do .Clara no banco shareit_visao.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class FabricaConexoesClara
{
    private readonly string _stringConexaoOrigem;
    private readonly string _stringConexaoClara;

    public string NomeBancoClara { get; }
    public string NomeBancoOrigem { get; }

    public FabricaConexoesClara(ConfiguracaoClara configuracao)
    {
        NomeBancoClara = configuracao.Banco.NomeBanco.Trim();
        if (!NomeBancoClara.Equals("shareit_clara", StringComparison.Ordinal))
            throw new InvalidOperationException($"Banco de destino inválido para esta baseline: '{NomeBancoClara}'. Esperado: shareit_clara.");

        var valor = CarregadorConfiguracaoClara.ObterStringConexao(configuracao);
        var origem = NormalizadorStringConexaoPostgres.Criar(valor);

        if (string.IsNullOrWhiteSpace(origem.Host) || string.IsNullOrWhiteSpace(origem.Username))
            throw new InvalidOperationException("CLARA_POSTGRES precisa informar host e usuário PostgreSQL.");

        NomeBancoOrigem = origem.Database?.Trim() ?? string.Empty;
        _stringConexaoOrigem = origem.ConnectionString;

        var destino = new NpgsqlConnectionStringBuilder(origem.ConnectionString)
        {
            Database = NomeBancoClara
        };
        _stringConexaoClara = destino.ConnectionString;
    }

    public NpgsqlConnection Criar() => new(_stringConexaoClara);

    public NpgsqlConnection CriarAdministrativa()
    {
        var administrativa = new NpgsqlConnectionStringBuilder(_stringConexaoOrigem);
        if (string.IsNullOrWhiteSpace(administrativa.Database) ||
            administrativa.Database.Equals(NomeBancoClara, StringComparison.OrdinalIgnoreCase))
        {
            administrativa.Database = "postgres";
        }

        return new NpgsqlConnection(administrativa.ConnectionString);
    }
}
