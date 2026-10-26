// Clara
// Programador: Paulo Andrade
// Objetivo: executar uma regra de aplicação da Clara.
// Origem: controladores, configuração e serviços hospedados.
// Destino: repositórios, integrações ou eventos da aplicação.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using Npgsql;
using Clara.Dados.Banco;

namespace Clara.Web.Servicos;

/// <summary>
/// Objetivo: executar, somente por solicitação do Master, o script SQL idempotente da empresa demonstração.
/// Origem: botão protegido na área Master.
/// Destino: tenant fictício "demonstracao" e sua telemetria de exemplo.
/// Dependências: script database/demo/CRIAR-EMPRESA-DEMONSTRACAO.sql e PostgreSQL.
/// Impacto: permite apresentar dashboards e fluxos do produto sem usar dados reais de clientes.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServicoEmpresaDemonstracao(FabricaConexoesClara fabrica)
{
    public async Task CriarOuAtualizarAsync(CancellationToken ct = default)
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "database", "demo", "CRIAR-EMPRESA-DEMONSTRACAO.sql");
        if (!File.Exists(caminho)) throw new FileNotFoundException("Script da empresa demonstração não foi encontrado no pacote publicado.", caminho);
        var sql = await File.ReadAllTextAsync(caminho, ct);
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao) { CommandTimeout = 120 };
        await comando.ExecuteNonQueryAsync(ct);
    }
}
