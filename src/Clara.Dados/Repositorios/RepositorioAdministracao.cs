// Clara
// Programador: Paulo Andrade
// Objetivo: persistir e recuperar dados da Clara.
// Origem: camada de aplicação/domínio.
// Destino: PostgreSQL e contratos de domínio.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;
using Clara.Comum.Configuracao;
using Clara.Comum.Seguranca;
using Clara.Dados.Banco;
using Clara.Dominio.Contratos;

namespace Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: executar operações administrativas de onboarding, estações, patrimônio e organização do tenant.
/// Origem: painel autenticado e descoberta automática do agente.
/// Destino: ativos, tokens_instalacao, empresas, unidades e departamentos.
/// Dependências: PostgreSQL e geração criptográfica de tokens.
/// Impacto: simplifica a implantação: o administrador escolhe a empresa e o agente descobre os dados técnicos da máquina.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioAdministracao(FabricaConexoesClara fabrica, ConfiguracaoClara configuracao)
{
    public async Task<IReadOnlyCollection<EmpresaBasica>> ListarEmpresasAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = "SELECT id,nome FROM empresas WHERE tenant_id=@tenant AND ativo=true ORDER BY nome";
        var lista = new List<EmpresaBasica>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct)) lista.Add(new EmpresaBasica(leitor.GetGuid(0), leitor.GetString(1)));
        return lista;
    }

    public async Task<IReadOnlyCollection<EmpresaAdministradaResumo>> ListarEmpresasDetalhadasAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql="""SELECT e.id,e.nome,e.documento,e.ativo,(SELECT count(*) FROM ativos a WHERE a.tenant_id=e.tenant_id AND a.empresa_id=e.id AND a.ativo=true) FROM empresas e WHERE e.tenant_id=@tenant ORDER BY e.nome""";
        var lista=new List<EmpresaAdministradaResumo>();await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var q=new NpgsqlCommand(sql,c);q.Parameters.AddWithValue("tenant",tenantId);await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))lista.Add(new(r.GetGuid(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.GetBoolean(3),Convert.ToInt32(r.GetInt64(4))));return lista;
    }

    public async Task<Guid> CriarEmpresaClienteAsync(Guid tenantId,string nome,string? documento,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(nome))throw new ArgumentException("Nome da empresa é obrigatório.");var id=Guid.NewGuid();await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var q=new NpgsqlCommand("INSERT INTO empresas(id,tenant_id,nome,documento) SELECT @id,t.id,@nome,@doc FROM tenants t WHERE t.id=@tenant AND t.ativo=true",c);q.Parameters.AddWithValue("id",id);q.Parameters.AddWithValue("tenant",tenantId);q.Parameters.AddWithValue("nome",nome.Trim());q.Parameters.AddWithValue("doc",(object?)Limpar(documento)??DBNull.Value);if(await q.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("Administradora não encontrada ou inativa.");return id;
    }

    public async Task<(AssinaturaTenantResumo Assinatura, int EstacoesAtivas)> ObterAssinaturaAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COALESCE(s.plano,'Gestão'),COALESCE(s.status,'Ativa'),COALESCE(s.limite_estacoes,15),
                   COALESCE(s.retencao_dias,90),COALESCE(s.armazenamento_gb,5),s.vencimento,s.observacao,
                   (SELECT count(*) FROM ativos a WHERE a.tenant_id=t.id AND a.ativo=true)
            FROM tenants t
            LEFT JOIN assinaturas_tenant s ON s.tenant_id=t.id
            WHERE t.id=@tenant;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct)) throw new InvalidOperationException("Tenant não encontrado.");
        var assinatura = new AssinaturaTenantResumo(tenantId, leitor.GetString(0), leitor.GetString(1), leitor.GetInt32(2), leitor.GetInt32(3), leitor.GetInt32(4), leitor.IsDBNull(5) ? null : leitor.GetFieldValue<DateOnly>(5), leitor.IsDBNull(6) ? null : leitor.GetString(6));
        return (assinatura, Convert.ToInt32(leitor.GetInt64(7)));
    }

    public async Task<TokenInstalacaoGerado> CriarTokenInstalacaoAsync(Guid tenantId, Guid usuarioId, Guid empresaId, string tipoDispositivo = "Windows", CancellationToken ct = default)
    {
        tipoDispositivo = tipoDispositivo.Equals("Android", StringComparison.OrdinalIgnoreCase) ? "Android" : "Windows";
        var numero = RandomNumberGenerator.GetInt32(0, 1_000_000_000).ToString("D9");
        var token = $"{numero[..3]}-{numero[3..6]}-{numero[6..]}";
        var hash = HashSeguro.Sha256Texto(NormalizarCodigo(token));
        var expira = DateTimeOffset.UtcNow.AddMinutes(15);

        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await ValidarAssinaturaParaNovaEstacaoAsync(conexao, tenantId, ct);
        await using var comando = new NpgsqlCommand("""
            INSERT INTO tokens_instalacao(tenant_id,empresa_id,ativo_id,token_hash,expira_em_utc,criado_por_usuario_id,tipo_dispositivo,descricao)
            SELECT @tenant,e.id,NULL,@hash,@expira,@usuario,@tipo,@descricao
            FROM empresas e
            WHERE e.id=@empresa AND e.tenant_id=@tenant AND e.ativo=true;
            """, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("empresa", empresaId);
        comando.Parameters.AddWithValue("hash", hash);
        comando.Parameters.AddWithValue("expira", expira);
        comando.Parameters.AddWithValue("usuario", usuarioId);
        comando.Parameters.AddWithValue("tipo", tipoDispositivo);
        comando.Parameters.AddWithValue("descricao", $"Registro {tipoDispositivo}");
        if (await comando.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException("Empresa não encontrada para este tenant.");
        return new TokenInstalacaoGerado(token, expira, configuracao.Servidor.UrlPublica, tipoDispositivo);
    }

    public async Task<CodigoManutencaoGerado> CriarCodigoManutencaoAsync(Guid tenantId, Guid usuarioId, Guid ativoId, string operacao, string? motivo, CancellationToken ct = default)
    {
        operacao = operacao switch { "Parar" => "Parar", "Reparar" => "Reparar", _ => "Desinstalar" };
        var numero = RandomNumberGenerator.GetInt32(0, 1_000_000_000).ToString("D9");
        var codigo = $"{numero[..3]}-{numero[3..6]}-{numero[6..]}";
        var hash = HashSeguro.Sha256Texto(NormalizarCodigo(codigo));
        var expira = DateTimeOffset.UtcNow.AddMinutes(15);
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand("""
            INSERT INTO codigos_manutencao(tenant_id,ativo_id,operacao,codigo_hash,motivo,expira_em_utc,criado_por_usuario_id)
            SELECT @tenant,a.id,@operacao,@hash,@motivo,@expira,@usuario
            FROM ativos a WHERE a.id=@ativo AND a.tenant_id=@tenant AND a.ativo=true;
            """, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("ativo", ativoId);
        comando.Parameters.AddWithValue("operacao", operacao);
        comando.Parameters.AddWithValue("hash", hash);
        comando.Parameters.AddWithValue("motivo", (object?)Limpar(motivo) ?? DBNull.Value);
        comando.Parameters.AddWithValue("expira", expira);
        comando.Parameters.AddWithValue("usuario", usuarioId);
        if (await comando.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Dispositivo não encontrado para este tenant.");
        return new CodigoManutencaoGerado(codigo, operacao, expira);
    }

    private static string NormalizarCodigo(string codigo) => new(codigo.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public async Task<PainelEstacoes> ListarEstacoesAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT at.id,a.id,at.hostname,COALESCE(a.usuario_windows,at.usuario_windows_detectado),at.numero_serie,
                   at.fabricante,at.modelo,at.sistema_operacional,at.patrimonio,at.responsavel,
                   e.nome,u.nome,d.nome,at.cadastro_completo,a.versao,a.ultima_comunicacao_utc,
                   CASE WHEN a.id IS NULL THEN 'Sem agente'
                        WHEN a.ultima_comunicacao_utc >= now() - (COALESCE(cm.tempo_offline_minutos,@offlineMinutos) * interval '1 minute') THEN 'Online'
                        ELSE 'Offline' END AS status,
                   CASE WHEN a.id IS NULL THEN 'Pendente'
                        WHEN a.ultima_comunicacao_utc < now() - (COALESCE(cm.tempo_offline_minutos,@offlineMinutos) * interval '1 minute') THEN 'Offline'
                        WHEN p.estado=1 THEN 'Ativo'
                        WHEN p.estado=2 THEN 'Ocioso'
                        WHEN p.estado=3 THEN 'Bloqueado'
                        WHEN p.estado=5 THEN 'Reunião'
                        ELSE 'Online' END AS estado_atual,
                   COALESCE(a.processo_atual,p.processo_executavel) AS processo_atual,
                   CASE WHEN loc.tipo='Escritorio' THEN 'Escritório'
                        WHEN loc.tipo='HomeOffice' THEN 'Home office'
                        WHEN loc.tipo='Externo' THEN 'Externo/Remoto'
                        WHEN loc.tipo='Outro' THEN COALESCE(loc.nome,'Outro')
                        ELSE 'Sem classificação' END AS local_trabalho
            FROM ativos at
            JOIN empresas e ON e.id=at.empresa_id AND e.tenant_id=at.tenant_id
            LEFT JOIN unidades u ON u.id=at.unidade_id AND u.tenant_id=at.tenant_id
            LEFT JOIN departamentos d ON d.id=at.departamento_id AND d.tenant_id=at.tenant_id
            LEFT JOIN agentes a ON a.ativo_id=at.id AND a.tenant_id=at.tenant_id AND a.ativo=true
            LEFT JOIN configuracoes_monitoramento cm ON cm.tenant_id=at.tenant_id
            LEFT JOIN LATERAL (
                SELECT estado,processo_executavel FROM periodos_atividade p
                WHERE p.tenant_id=at.tenant_id AND p.ativo_id=at.id
                ORDER BY p.fim_utc DESC LIMIT 1
            ) p ON true
            LEFT JOIN LATERAL (
                SELECT r.tipo,r.nome FROM regras_local_trabalho r
                WHERE r.tenant_id=at.tenant_id AND r.ativo=true AND a.endereco_ip_publico IS NOT NULL
                  AND a.endereco_ip_publico <<= r.faixa_ip
                ORDER BY masklen(r.faixa_ip) DESC LIMIT 1
            ) loc ON true
            WHERE at.tenant_id=@tenant AND at.ativo=true
            ORDER BY at.cadastro_completo, status DESC, at.hostname;
            """;
        var estacoes = new List<EstacaoResumo>();
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using (var comando = new NpgsqlCommand(sql, conexao))
        {
            comando.Parameters.AddWithValue("tenant", tenantId);
            comando.Parameters.AddWithValue("offlineMinutos", configuracao.Servidor.MinutosParaConsiderarOffline);
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            while (await leitor.ReadAsync(ct))
            {
                estacoes.Add(new EstacaoResumo(
                    leitor.GetGuid(0),
                    leitor.IsDBNull(1) ? null : leitor.GetGuid(1),
                    leitor.GetString(2),
                    leitor.IsDBNull(3) ? null : leitor.GetString(3),
                    leitor.IsDBNull(4) ? null : leitor.GetString(4),
                    leitor.IsDBNull(5) ? null : leitor.GetString(5),
                    leitor.IsDBNull(6) ? null : leitor.GetString(6),
                    leitor.IsDBNull(7) ? null : leitor.GetString(7),
                    leitor.IsDBNull(8) ? null : leitor.GetString(8),
                    leitor.IsDBNull(9) ? null : leitor.GetString(9),
                    leitor.GetString(10),
                    leitor.IsDBNull(11) ? null : leitor.GetString(11),
                    leitor.IsDBNull(12) ? null : leitor.GetString(12),
                    leitor.GetBoolean(13),
                    leitor.GetString(16),
                    leitor.IsDBNull(14) ? null : leitor.GetString(14),
                    leitor.IsDBNull(15) ? null : ComoDataHora(leitor, 15),
                    leitor.GetString(17),
                    leitor.IsDBNull(18) ? null : leitor.GetString(18),
                    leitor.GetString(19)));
            }
        }

        var empresas = await ListarOpcoesAsync(conexao, "SELECT id,nome FROM empresas WHERE tenant_id=@tenant AND ativo=true ORDER BY nome", tenantId, ct);
        var unidades = await ListarOpcoesComEmpresaAsync(conexao, "SELECT id,nome,empresa_id FROM unidades WHERE tenant_id=@tenant AND ativo=true ORDER BY nome", tenantId, ct);
        var departamentos = await ListarOpcoesComEmpresaAsync(conexao, "SELECT id,nome,empresa_id FROM departamentos WHERE tenant_id=@tenant AND ativo=true ORDER BY nome", tenantId, ct);
        return new PainelEstacoes(estacoes, empresas, unidades, departamentos);
    }

    public async Task<DetalheEstacao?> ObterEstacaoAsync(Guid tenantId, Guid ativoId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT at.id,at.empresa_id,at.unidade_id,at.departamento_id,at.hostname,
                   COALESCE(a.usuario_windows,at.usuario_windows_detectado),at.numero_serie,at.uuid_equipamento,
                   at.fabricante,at.modelo,at.sistema_operacional,at.arquitetura,at.memoria_total_bytes,at.disco_total_bytes,
                   at.endereco_ip_local,host(at.endereco_ip_publico),at.patrimonio,at.responsavel,at.cadastro_completo,
                   at.primeira_comunicacao_utc,a.ultima_comunicacao_utc,a.versao
            FROM ativos at
            LEFT JOIN agentes a ON a.ativo_id=at.id AND a.tenant_id=at.tenant_id AND a.ativo=true
            WHERE at.tenant_id=@tenant AND at.id=@ativo AND at.ativo=true
            LIMIT 1;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("ativo", ativoId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct)) return null;
        return new DetalheEstacao(
            leitor.GetGuid(0), leitor.GetGuid(1), leitor.IsDBNull(2) ? null : leitor.GetGuid(2), leitor.IsDBNull(3) ? null : leitor.GetGuid(3),
            leitor.GetString(4), leitor.IsDBNull(5) ? null : leitor.GetString(5), leitor.IsDBNull(6) ? null : leitor.GetString(6), leitor.IsDBNull(7) ? null : leitor.GetString(7),
            leitor.IsDBNull(8) ? null : leitor.GetString(8), leitor.IsDBNull(9) ? null : leitor.GetString(9), leitor.IsDBNull(10) ? null : leitor.GetString(10), leitor.IsDBNull(11) ? null : leitor.GetString(11),
            leitor.IsDBNull(12) ? null : leitor.GetInt64(12), leitor.IsDBNull(13) ? null : leitor.GetInt64(13), leitor.IsDBNull(14) ? null : leitor.GetString(14), leitor.IsDBNull(15) ? null : leitor.GetString(15),
            leitor.IsDBNull(16) ? null : leitor.GetString(16), leitor.IsDBNull(17) ? null : leitor.GetString(17), leitor.GetBoolean(18),
            leitor.IsDBNull(19) ? null : ComoDataHora(leitor, 19), leitor.IsDBNull(20) ? null : ComoDataHora(leitor, 20), leitor.IsDBNull(21) ? null : leitor.GetString(21));
    }

    public async Task AtualizarCadastroEstacaoAsync(Guid tenantId, Guid ativoId, Guid empresaId, Guid? unidadeId, Guid? departamentoId, string? patrimonio, string? responsavel, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE ativos SET
                empresa_id=@empresa,
                unidade_id=@unidade,
                departamento_id=@departamento,
                patrimonio=@patrimonio,
                responsavel=@responsavel,
                cadastro_completo=true,
                atualizado_em_utc=now()
            WHERE tenant_id=@tenant AND id=@ativo AND ativo=true
              AND EXISTS (SELECT 1 FROM empresas e WHERE e.id=@empresa AND e.tenant_id=@tenant AND e.ativo=true)
              AND (@unidade IS NULL OR EXISTS (
                    SELECT 1 FROM unidades u WHERE u.id=@unidade AND u.tenant_id=@tenant AND u.empresa_id=@empresa AND u.ativo=true))
              AND (@departamento IS NULL OR EXISTS (
                    SELECT 1 FROM departamentos d WHERE d.id=@departamento AND d.tenant_id=@tenant AND d.empresa_id=@empresa AND d.ativo=true));
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("ativo", ativoId);
        comando.Parameters.AddWithValue("empresa", empresaId);
        comando.Parameters.Add("unidade", NpgsqlDbType.Uuid).Value = (object?)unidadeId ?? DBNull.Value;
        comando.Parameters.Add("departamento", NpgsqlDbType.Uuid).Value = (object?)departamentoId ?? DBNull.Value;
        comando.Parameters.Add("patrimonio", NpgsqlDbType.Varchar).Value = (object?)Limpar(patrimonio) ?? DBNull.Value;
        comando.Parameters.Add("responsavel", NpgsqlDbType.Varchar).Value = (object?)Limpar(responsavel) ?? DBNull.Value;
        if (await comando.ExecuteNonQueryAsync(ct) == 0) throw new InvalidOperationException("Estação não encontrada para este tenant.");
    }

    public async Task<IReadOnlyCollection<OpcaoCadastro>> ListarUnidadesAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        return await ListarOpcoesComEmpresaAsync(conexao, "SELECT id,nome,empresa_id FROM unidades WHERE tenant_id=@tenant AND ativo=true ORDER BY nome", tenantId, ct);
    }

    public async Task<IReadOnlyCollection<OpcaoCadastro>> ListarDepartamentosAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        return await ListarOpcoesComEmpresaAsync(conexao, "SELECT id,nome,empresa_id FROM departamentos WHERE tenant_id=@tenant AND ativo=true ORDER BY nome", tenantId, ct);
    }

    private static async Task<IReadOnlyCollection<OpcaoCadastro>> ListarOpcoesAsync(NpgsqlConnection conexao, string sql, Guid tenantId, CancellationToken ct)
    {
        var lista = new List<OpcaoCadastro>();
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct)) lista.Add(new OpcaoCadastro(leitor.GetGuid(0), leitor.GetString(1)));
        return lista;
    }


    private static async Task<IReadOnlyCollection<OpcaoCadastro>> ListarOpcoesComEmpresaAsync(NpgsqlConnection conexao, string sql, Guid tenantId, CancellationToken ct)
    {
        var lista = new List<OpcaoCadastro>();
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct)) lista.Add(new OpcaoCadastro(leitor.GetGuid(0), leitor.GetString(1), leitor.GetGuid(2)));
        return lista;
    }

    private static DateTimeOffset ComoDataHora(NpgsqlDataReader leitor, int indice)
        => new(DateTime.SpecifyKind(leitor.GetDateTime(indice), DateTimeKind.Utc));

    private static async Task ValidarAssinaturaParaNovaEstacaoAsync(NpgsqlConnection conexao, Guid tenantId, CancellationToken ct)
    {
        const string sql = """
            SELECT COALESCE(s.status,'Ativa'), COALESCE(s.limite_estacoes,15), s.vencimento,
                   (SELECT count(*) FROM ativos a WHERE a.tenant_id=@tenant AND a.ativo=true)
            FROM tenants t
            LEFT JOIN assinaturas_tenant s ON s.tenant_id=t.id
            WHERE t.id=@tenant AND t.ativo=true;
            """;
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct)) throw new InvalidOperationException("Tenant não encontrado ou inativo.");
        var status = leitor.GetString(0);
        var limite = leitor.GetInt32(1);
        var vencimento = leitor.IsDBNull(2) ? (DateOnly?)null : leitor.GetFieldValue<DateOnly>(2);
        var estacoes = Convert.ToInt32(leitor.GetInt64(3));
        if (status is not ("Ativa" or "Trial"))
            throw new InvalidOperationException($"A assinatura está {status.ToLowerInvariant()}. Regularize o plano antes de instalar uma nova estação.");
        if (vencimento.HasValue && vencimento.Value < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new InvalidOperationException("A assinatura está vencida. Atualize o vencimento antes de instalar uma nova estação.");
        if (estacoes >= limite)
            throw new InvalidOperationException($"Limite do plano atingido: {estacoes} de {limite} estações. Aumente o limite para gerar um novo token.");
    }

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

public sealed record EmpresaBasica(Guid Id, string Nome);
public sealed record EmpresaAdministradaResumo(Guid Id,string Nome,string? Documento,bool Ativa,int Dispositivos);
public sealed record TokenInstalacaoGerado(string Token, DateTimeOffset ExpiraEmUtc, string UrlServidor, string TipoDispositivo);
public sealed record CodigoManutencaoGerado(string Codigo, string Operacao, DateTimeOffset ExpiraEmUtc);
