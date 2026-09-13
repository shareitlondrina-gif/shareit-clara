using Npgsql;
using ShareIT.Clara.Dados.Banco;

namespace ShareIT.Clara.Web.Servicos;

/// <summary>
/// Objetivo: manter somente o tenant fictício de demonstração com heartbeat visualmente atual durante apresentações.
/// Origem: navegação autenticada do tenant demonstracao.
/// Destino: agentes e último período de atividade exclusivamente do tenant de demonstração.
/// Dependências: PostgreSQL.
/// Impacto: evita dados demo envelhecidos sem alterar qualquer tenant real.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServicoRelogioDemonstracao(FabricaConexoesClara fabrica)
{
    public static readonly Guid TenantDemonstracao = Guid.Parse("d0000000-0000-4000-8000-000000000001");

    public async Task AtualizarSeNecessarioAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId != TenantDemonstracao) return;
        const string sql = """
            DO $$
            DECLARE
                v_ultima timestamptz;
            BEGIN
                SELECT max(ultima_comunicacao_utc) INTO v_ultima
                FROM agentes WHERE tenant_id='d0000000-0000-4000-8000-000000000001'::uuid AND ativo=true;
                IF v_ultima IS NULL OR v_ultima <= now()-interval '25 seconds' THEN
                WITH ordenados AS (
                    SELECT id,ativo_id,row_number() OVER (ORDER BY hostname) rn
                    FROM agentes
                    WHERE tenant_id='d0000000-0000-4000-8000-000000000001'::uuid AND ativo=true
                )
                UPDATE agentes a
                SET ultima_comunicacao_utc=CASE WHEN o.rn<=12 THEN now()-(o.rn||' seconds')::interval ELSE now()-interval '2 hours'-(o.rn||' minutes')::interval END,
                    status=CASE WHEN o.rn<=12 THEN 'Online' ELSE 'Offline' END,
                    atualizado_em_utc=now()
                FROM ordenados o WHERE a.id=o.id;

                WITH ultimos AS (
                    SELECT DISTINCT ON (p.agente_id) p.id,p.agente_id
                    FROM periodos_atividade p
                    WHERE p.tenant_id='d0000000-0000-4000-8000-000000000001'::uuid
                    ORDER BY p.agente_id,p.fim_utc DESC
                ), ordenados AS (
                    SELECT a.id,row_number() OVER (ORDER BY a.hostname) rn
                    FROM agentes a
                    WHERE a.tenant_id='d0000000-0000-4000-8000-000000000001'::uuid AND a.ativo=true
                )
                UPDATE periodos_atividade p
                SET fim_utc=CASE WHEN o.rn<=12 THEN now() ELSE now()-interval '2 hours' END,
                    inicio_utc=CASE WHEN o.rn<=12 THEN now()-interval '18 minutes' ELSE now()-interval '2 hours 18 minutes' END
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
}
