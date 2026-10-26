// Clara
// Programador: Paulo Andrade
// Objetivo: executar uma regra de aplicação da Clara.
// Origem: controladores, configuração e serviços hospedados.
// Destino: repositórios, integrações ou eventos da aplicação.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Reflection;
using Npgsql;
using Clara.Dados.Banco;

namespace Clara.Web.Servicos;

/// <summary>
/// Objetivo: manter somente o tenant fictício de demonstração com heartbeat visualmente atual e dados gerenciais completos.
/// Origem: navegação autenticada do tenant demonstracao.
/// Destino: agentes, períodos, oportunidades, evidências e evidências visuais exclusivamente do tenant de demonstração.
/// Dependências: PostgreSQL e ServicoEmpresaDemonstracao.
/// Impacto: evita dados demo envelhecidos ou incompletos sem alterar qualquer tenant real.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServicoRelogioDemonstracao(
    FabricaConexoesClara fabrica,
    ServicoEmpresaDemonstracao empresaDemonstracao,
    ILogger<ServicoRelogioDemonstracao> logger)
{
    public static readonly Guid TenantDemonstracao = Guid.Parse("d0000000-0000-4000-8000-000000000001");

    public async Task AtualizarSeNecessarioAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId != TenantDemonstracao) return;

        // A v0.5.6 passa a reparar automaticamente a carga da empresa Demonstração quando
        // a base veio de uma versão anterior ou quando oportunidades/evidências não existem.
        // O comportamento é restrito ao GUID fixo do tenant fictício.
        try
        {
            if (await PrecisaRecarregarDemonstracaoAsync(ct))
            {
                logger.LogInformation("Carga da empresa Demonstração está incompleta ou desatualizada. Recriando fotografia fictícia da versão atual.");
                await empresaDemonstracao.CriarOuAtualizarAsync(ct);
            }
        }
        catch (PostgresException ex) when (ex.SqlState is "42P01" or "42703")
        {
            // Pode ocorrer por poucos instantes enquanto as migrations sobem no Render.
            // O health/readiness continuará indicando o estado real do banco; não afeta tenants reais.
            logger.LogWarning(ex, "A carga automática da empresa Demonstração aguardará a próxima navegação porque o schema ainda está sendo atualizado.");
        }

        const string sql = """
            DO $$
            DECLARE
                v_ultima timestamptz;
            BEGIN
                SELECT max(ultima_comunicacao_utc) INTO v_ultima
                FROM agentes WHERE tenant_id='d0000000-0000-4000-8000-000000000001'::uuid AND ativo=true;
                IF v_ultima IS NULL OR v_ultima <= now()-interval '25 seconds' THEN
                WITH ordenados AS (
                    SELECT id,ativo_id,hostname,row_number() OVER (ORDER BY hostname) rn
                    FROM agentes
                    WHERE tenant_id='d0000000-0000-4000-8000-000000000001'::uuid AND ativo=true
                )
                UPDATE agentes a
                SET ultima_comunicacao_utc=CASE WHEN o.hostname IN ('DEMO-PC-06','DEMO-PC-12','DEMO-PC-15') THEN now()-interval '2 hours'-(o.rn||' minutes')::interval ELSE now()-(o.rn||' seconds')::interval END,
                    status=CASE WHEN o.hostname IN ('DEMO-PC-06','DEMO-PC-12','DEMO-PC-15') THEN 'Offline' ELSE 'Online' END,
                    atualizado_em_utc=now()
                FROM ordenados o WHERE a.id=o.id;

                WITH ultimos AS (
                    SELECT DISTINCT ON (p.agente_id) p.id,p.agente_id
                    FROM periodos_atividade p
                    WHERE p.tenant_id='d0000000-0000-4000-8000-000000000001'::uuid
                    ORDER BY p.agente_id,p.fim_utc DESC
                ), ordenados AS (
                    SELECT a.id,a.hostname,row_number() OVER (ORDER BY a.hostname) rn
                    FROM agentes a
                    WHERE a.tenant_id='d0000000-0000-4000-8000-000000000001'::uuid AND a.ativo=true
                )
                UPDATE periodos_atividade p
                SET fim_utc=CASE WHEN o.hostname IN ('DEMO-PC-06','DEMO-PC-12','DEMO-PC-15') THEN now()-interval '2 hours' ELSE now() END,
                    inicio_utc=CASE WHEN o.hostname IN ('DEMO-PC-06','DEMO-PC-12','DEMO-PC-15') THEN now()-interval '2 hours 18 minutes' ELSE now()-interval '18 minutes' END
                FROM ultimos u JOIN ordenados o ON o.id=u.agente_id
                WHERE p.id=u.id;
                END IF;
            END $$;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao) { CommandTimeout = 15 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private async Task<bool> PrecisaRecarregarDemonstracaoAsync(CancellationToken ct)
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);

        await using (var esquema = new NpgsqlCommand("SELECT to_regclass('public.evidencias_visuais') IS NOT NULL", conexao))
        {
            var schemaPronto = (bool)(await esquema.ExecuteScalarAsync(ct) ?? false);
            if (!schemaPronto) return false;
        }

        const string sql = """
            SELECT
                (SELECT COUNT(*)::int FROM oportunidades WHERE tenant_id=@tenant),
                (SELECT COUNT(*)::int FROM evidencias_contextuais WHERE tenant_id=@tenant),
                (SELECT COUNT(*)::int FROM evidencias_visuais WHERE tenant_id=@tenant),
                COALESCE((SELECT max(versao) FROM agentes WHERE tenant_id=@tenant AND ativo=true),'');
            """;
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", TenantDemonstracao);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct)) return true;

        var oportunidades = leitor.GetInt32(0);
        var evidencias = leitor.GetInt32(1);
        var visuais = leitor.GetInt32(2);
        var versaoAgente = leitor.GetString(3);
        var versaoAtual = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.6.0";

        return oportunidades < 15 || evidencias < 10 || visuais < 4 || !string.Equals(versaoAgente, versaoAtual, StringComparison.OrdinalIgnoreCase);
    }
}
