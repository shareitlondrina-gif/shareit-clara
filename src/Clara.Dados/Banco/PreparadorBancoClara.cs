// Clara
// Programador: Paulo Andrade
// Objetivo: persistir e recuperar dados da Clara.
// Origem: camada de aplicação/domínio.
// Destino: PostgreSQL e contratos de domínio.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Text.RegularExpressions;
using Npgsql;
using Clara.Comum.Configuracao;

namespace Clara.Dados.Banco;

/// <summary>
/// Objetivo: garantir automaticamente a existência do banco exclusivo clara antes das migrations.
/// Origem: conexão PostgreSQL da instância configurada em CLARA_POSTGRES; em homologação ela pode apontar para o banco visao da mesma instância.
/// Destino: novo banco lógico clara na mesma instância PostgreSQL.
/// Dependências: Npgsql, FabricaConexoesClara e ConfiguracaoClara.
/// Impacto: elimina CREATE DATABASE manual e protege o banco visao contra migrations ou tabelas da Clara.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class PreparadorBancoClara(FabricaConexoesClara fabrica, ConfiguracaoClara configuracao)
{
    private static readonly Regex NomeSeguro = new("^[a-z][a-z0-9_]{0,62}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task GarantirAsync(CancellationToken cancellationToken = default)
    {
        ValidarDestinoProtegido();

        if (await BancoDestinoDisponivelAsync(cancellationToken))
            return;

        if (!configuracao.Banco.CriarAutomaticamente)
            throw new InvalidOperationException("O banco configurado para a Clara não existe e a criação automática está desabilitada.");

        await using var administrativa = fabrica.CriarAdministrativa();
        await administrativa.OpenAsync(cancellationToken);

        var bancoAdministrativo = await ObterBancoAtualAsync(administrativa, cancellationToken);
        if (bancoAdministrativo.Equals(fabrica.NomeBancoClara, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A conexão administrativa não pode usar o próprio banco de destino durante a criação.");

        if (!await ExisteAsync(administrativa, fabrica.NomeBancoClara, cancellationToken))
        {
            // O nome é validado por whitelist antes de ser usado no comando CREATE DATABASE.
            await using var criar = new NpgsqlCommand($"CREATE DATABASE {fabrica.NomeBancoClara}", administrativa);
            await criar.ExecuteNonQueryAsync(cancellationToken);
        }

        await ValidarConexaoDestinoAsync(cancellationToken);
    }

    private void ValidarDestinoProtegido()
    {
        if (!NomeSeguro.IsMatch(fabrica.NomeBancoClara))
            throw new InvalidOperationException("Proteção de banco: informe um nome PostgreSQL seguro para a Clara.");

        var proibidos = new[] { "postgres", "template0", "template1" };
        if (proibidos.Contains(fabrica.NomeBancoClara, StringComparer.OrdinalIgnoreCase) ||
            fabrica.NomeBancoClara.Contains("visao", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Proteção de banco: o banco configurado é reservado para outro uso.");
    }

    private async Task<bool> BancoDestinoDisponivelAsync(CancellationToken ct)
    {
        try
        {
            await ValidarConexaoDestinoAsync(ct);
            return true;
        }
        catch (NpgsqlException)
        {
            return false;
        }
    }

    private async Task ValidarConexaoDestinoAsync(CancellationToken ct)
    {
        await using var destino = fabrica.Criar();
        await destino.OpenAsync(ct);
        var atual = await ObterBancoAtualAsync(destino, ct);
        if (!atual.Equals(fabrica.NomeBancoClara, StringComparison.Ordinal))
            throw new InvalidOperationException($"Proteção de banco: conexão abriu '{atual}', mas o destino configurado é '{fabrica.NomeBancoClara}'.");
    }

    private static async Task<bool> ExisteAsync(NpgsqlConnection conexao, string nomeBanco, CancellationToken ct)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @nome)";
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("nome", nomeBanco);
        return (bool)(await comando.ExecuteScalarAsync(ct) ?? false);
    }

    private static async Task<string> ObterBancoAtualAsync(NpgsqlConnection conexao, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("SELECT current_database()", conexao);
        return Convert.ToString(await comando.ExecuteScalarAsync(ct)) ?? string.Empty;
    }
}
