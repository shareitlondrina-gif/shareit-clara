using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Npgsql;
using ShareIT.Clara.Dados.Banco;

namespace ShareIT.Clara.Web.Seguranca;

/// <summary>
/// Objetivo: persistir o key ring do ASP.NET Core Data Protection no PostgreSQL/Neon.
/// Origem: infraestrutura de autenticação/cookies da Web.
/// Destino: tabela chaves_protecao_dados da migration 0005.
/// Dependências: Npgsql e IXmlRepository.
/// Impacto: mantém cookies e proteções estáveis entre reinicializações do filesystem efêmero do Render.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioChavesProtecaoDados(FabricaConexoesClara fabrica) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        const string sql = "SELECT xml FROM chaves_protecao_dados ORDER BY id";
        var elementos = new List<XElement>();
        try
        {
            using var conexao = fabrica.Criar();
            conexao.Open();
            using var comando = new NpgsqlCommand(sql, conexao);
            using var leitor = comando.ExecuteReader();
            while (leitor.Read())
            {
                var xml = leitor.GetString(0);
                if (!string.IsNullOrWhiteSpace(xml)) elementos.Add(XElement.Parse(xml));
            }
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // A migration 0005 pode ainda estar sendo aplicada durante o primeiro liveness do Render.
            // Não cria tabela fora do migrador; o próximo acesso ocorre após a inicialização do banco.
        }
        return elementos;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        const string sql = "INSERT INTO chaves_protecao_dados(nome,xml) VALUES(@nome,@xml)";
        using var conexao = fabrica.Criar();
        conexao.Open();
        using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("nome", string.IsNullOrWhiteSpace(friendlyName) ? "data-protection-key" : friendlyName);
        comando.Parameters.AddWithValue("xml", element.ToString(SaveOptions.DisableFormatting));
        comando.ExecuteNonQuery();
    }
}
