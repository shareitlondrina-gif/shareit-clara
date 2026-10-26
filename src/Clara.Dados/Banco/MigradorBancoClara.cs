// Clara
// Programador: Paulo Andrade
// Objetivo: persistir e recuperar dados da Clara.
// Origem: camada de aplicação/domínio.
// Destino: PostgreSQL e contratos de domínio.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Reflection;
using Npgsql;

namespace Clara.Dados.Banco;

/// <summary>
/// Objetivo: aplicar migrations SQL sequenciais e imutáveis da Clara.
/// Origem: recursos SQL incorporados ao assembly Clara.Dados.
/// Destino: banco PostgreSQL clara.
/// Dependências: Npgsql e migracoes_aplicadas.
/// Impacto: garante evolução reproduzível do schema em qualquer ambiente.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class MigradorBancoClara(FabricaConexoesClara fabrica)
{
    public async Task AplicarAsync(CancellationToken cancellationToken = default)
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(cancellationToken);
        await ValidarBancoDestinoAsync(conexao, fabrica.NomeBancoClara, cancellationToken);

        await GarantirTabelaControleAsync(conexao, cancellationToken);

        var assembly = typeof(MigradorBancoClara).Assembly;
        var recursos = assembly.GetManifestResourceNames()
            .Where(nome => nome.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(nome => nome, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var recurso in recursos)
        {
            var id = ExtrairId(recurso);
            if (await JaAplicadaAsync(conexao, id, cancellationToken))
                continue;

            await using var fluxo = assembly.GetManifestResourceStream(recurso) ?? throw new InvalidOperationException($"Migration não encontrada: {recurso}");
            using var leitor = new StreamReader(fluxo);
            var sql = await leitor.ReadToEndAsync(cancellationToken);

            await using var transacao = await conexao.BeginTransactionAsync(cancellationToken);
            await using (var comando = new NpgsqlCommand(sql, conexao, transacao))
                await comando.ExecuteNonQueryAsync(cancellationToken);

            await using (var registrar = new NpgsqlCommand("INSERT INTO migracoes_aplicadas(id,nome) VALUES (@id,@nome)", conexao, transacao))
            {
                registrar.Parameters.AddWithValue("id", id);
                registrar.Parameters.AddWithValue("nome", recurso);
                await registrar.ExecuteNonQueryAsync(cancellationToken);
            }

            await transacao.CommitAsync(cancellationToken);
        }
    }

    private static async Task ValidarBancoDestinoAsync(NpgsqlConnection conexao, string bancoEsperado, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("SELECT current_database()", conexao);
        var atual = Convert.ToString(await comando.ExecuteScalarAsync(ct)) ?? string.Empty;
        if (!atual.Equals(bancoEsperado, StringComparison.Ordinal))
            throw new InvalidOperationException($"Proteção de migrations: banco conectado '{atual}', mas o destino configurado é '{bancoEsperado}'.");
    }

    private static async Task GarantirTabelaControleAsync(NpgsqlConnection conexao, CancellationToken ct)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS migracoes_aplicadas (
                id varchar(100) PRIMARY KEY,
                nome varchar(250) NOT NULL,
                aplicada_em_utc timestamptz NOT NULL DEFAULT now()
            );
            """;
        await using var comando = new NpgsqlCommand(sql, conexao);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> JaAplicadaAsync(NpgsqlConnection conexao, string id, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM migracoes_aplicadas WHERE id=@id)", conexao);
        comando.Parameters.AddWithValue("id", id);
        return (bool)(await comando.ExecuteScalarAsync(ct) ?? false);
    }

    private static string ExtrairId(string recurso)
    {
        var arquivo = recurso.Split('.').Reverse().Skip(1).FirstOrDefault() ?? recurso;
        var inicio = recurso.LastIndexOf("migrations.", StringComparison.OrdinalIgnoreCase);
        if (inicio >= 0)
            arquivo = recurso[(inicio + "migrations.".Length)..];
        return arquivo.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) ? arquivo[..^4] : arquivo;
    }
}
