using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;
using ShareIT.Clara.Comum.Seguranca;
using ShareIT.Clara.Dados.Banco;
using ShareIT.Clara.Dominio.Contratos;
using ShareIT.Clara.Dominio.Modelos;

namespace ShareIT.Clara.Dados.Repositorios;

/// <summary>
/// Objetivo: concentrar registro automático, autenticação, heartbeat, inventário, telemetria e atualização dos agentes Windows.
/// Origem: endpoints autenticados da API do Share IT .Clara.
/// Destino: ativos, agentes, periodos_atividade e atualizacoes_agente.
/// Dependências: PostgreSQL, HashSeguro e contratos do domínio.
/// Impacto: sustenta Agent → API → PostgreSQL → Dashboard e elimina cadastro técnico manual da estação.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class RepositorioAgentes(FabricaConexoesClara fabrica)
{
    public async Task<RegistroAgenteResposta?> RegistrarAsync(RegistroAgenteSolicitacao solicitacao, string? enderecoIpPublico, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(solicitacao.TokenInstalacao) || string.IsNullOrWhiteSpace(solicitacao.Inventario.Hostname)) return null;
        var tokenHash = HashSeguro.Sha256Texto(solicitacao.TokenInstalacao);
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        const string buscarToken = """
            SELECT t.tenant_id, t.empresa_id, t.ativo_id
            FROM tokens_instalacao t
            WHERE t.token_hash=@hash AND t.usado_em_utc IS NULL AND t.expira_em_utc > now()
            FOR UPDATE;
            """;
        Guid tenantId;
        Guid empresaId;
        Guid? ativoIdToken;
        await using (var comando = new NpgsqlCommand(buscarToken, conexao, transacao))
        {
            comando.Parameters.AddWithValue("hash", tokenHash);
            await using var leitor = await comando.ExecuteReaderAsync(ct);
            if (!await leitor.ReadAsync(ct)) return null;
            tenantId = leitor.GetGuid(0);
            empresaId = leitor.GetGuid(1);
            ativoIdToken = leitor.IsDBNull(2) ? null : leitor.GetGuid(2);
        }

        var ativoId = ativoIdToken ?? await LocalizarOuCriarAtivoAsync(conexao, transacao, tenantId, empresaId, solicitacao.Inventario, enderecoIpPublico, ct);
        await AtualizarInventarioAtivoAsync(conexao, transacao, tenantId, ativoId, empresaId, solicitacao.Inventario, enderecoIpPublico, ct);

        var identificadorAgente = Guid.NewGuid();
        var tokenDispositivo = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenDispositivoHash = HashSeguro.Sha256Texto(tokenDispositivo);

        const string inserirAgente = """
            INSERT INTO agentes(
                tenant_id,ativo_id,identificador_agente,token_hash,versao,hostname,usuario_windows,
                sistema_operacional,fabricante,modelo,numero_serie,uuid_equipamento,arquitetura,
                memoria_total_bytes,disco_total_bytes,endereco_ip_local,endereco_ip_publico,
                ultima_comunicacao_utc,status)
            VALUES(
                @tenant,@ativo,@identificador,@token,@versao,@hostname,@usuario,
                @so,@fabricante,@modelo,@serie,@uuid,@arquitetura,
                @memoria,@disco,@ipLocal,CAST(@ipPublico AS inet),now(),'Online')
            ON CONFLICT(tenant_id,ativo_id) DO UPDATE SET
                identificador_agente=EXCLUDED.identificador_agente,
                token_hash=EXCLUDED.token_hash,
                versao=EXCLUDED.versao,
                hostname=EXCLUDED.hostname,
                usuario_windows=EXCLUDED.usuario_windows,
                sistema_operacional=EXCLUDED.sistema_operacional,
                fabricante=EXCLUDED.fabricante,
                modelo=EXCLUDED.modelo,
                numero_serie=EXCLUDED.numero_serie,
                uuid_equipamento=EXCLUDED.uuid_equipamento,
                arquitetura=EXCLUDED.arquitetura,
                memoria_total_bytes=EXCLUDED.memoria_total_bytes,
                disco_total_bytes=EXCLUDED.disco_total_bytes,
                endereco_ip_local=EXCLUDED.endereco_ip_local,
                endereco_ip_publico=EXCLUDED.endereco_ip_publico,
                ultima_comunicacao_utc=now(),
                status='Online',
                ativo=true,
                atualizado_em_utc=now();
            """;
        await using (var comando = new NpgsqlCommand(inserirAgente, conexao, transacao))
        {
            AdicionarParametrosInventario(comando, tenantId, ativoId, solicitacao.VersaoAgente, solicitacao.Inventario, enderecoIpPublico);
            comando.Parameters.AddWithValue("identificador", identificadorAgente);
            comando.Parameters.AddWithValue("token", tokenDispositivoHash);
            await comando.ExecuteNonQueryAsync(ct);
        }

        await using (var usarToken = new NpgsqlCommand("UPDATE tokens_instalacao SET usado_em_utc=now(), ativo_id=@ativo WHERE token_hash=@hash", conexao, transacao))
        {
            usarToken.Parameters.AddWithValue("hash", tokenHash);
            usarToken.Parameters.AddWithValue("ativo", ativoId);
            await usarToken.ExecuteNonQueryAsync(ct);
        }

        await transacao.CommitAsync(ct);
        return new RegistroAgenteResposta(identificadorAgente, tokenDispositivo, ativoId, tenantId);
    }

    public async Task<ContextoAgente?> AutenticarAsync(Guid identificador, string token, CancellationToken ct = default)
    {
        var hash = HashSeguro.Sha256Texto(token);
        const string sql = """
            SELECT id, tenant_id, ativo_id
            FROM agentes
            WHERE identificador_agente=@identificador AND token_hash=@hash AND ativo=true
            LIMIT 1;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("identificador", identificador);
        comando.Parameters.AddWithValue("hash", hash);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        return await leitor.ReadAsync(ct)
            ? new ContextoAgente(leitor.GetGuid(0), leitor.GetGuid(1), leitor.GetGuid(2))
            : null;
    }

    public async Task RegistrarHeartbeatAsync(ContextoAgente contexto, HeartbeatAgente heartbeat, string? enderecoIpPublico, CancellationToken ct = default)
    {
        const string agenteSql = """
            UPDATE agentes SET
                versao=@versao,
                hostname=@hostname,
                usuario_windows=@usuario,
                sistema_operacional=@so,
                fabricante=@fabricante,
                modelo=@modelo,
                numero_serie=@serie,
                uuid_equipamento=@uuid,
                arquitetura=@arquitetura,
                memoria_total_bytes=@memoria,
                disco_total_bytes=@disco,
                endereco_ip_local=@ipLocal,
                endereco_ip_publico=CAST(@ipPublico AS inet),
                processo_atual=@processo,
                ultima_comunicacao_utc=@momento,
                status='Online',
                atualizado_em_utc=now()
            WHERE id=@agente AND tenant_id=@tenant;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        var exclusoes = await CarregarExclusoesAsync(conexao, contexto, ct);
        var processoAtual = AplicativoExcluido(exclusoes, heartbeat.ProcessoAtual) ? null : heartbeat.ProcessoAtual;
        await using var transacao = await conexao.BeginTransactionAsync(ct);
        await using (var comando = new NpgsqlCommand(agenteSql, conexao, transacao))
        {
            AdicionarParametrosInventario(comando, contexto.TenantId, contexto.AtivoId, heartbeat.VersaoAgente, heartbeat.Inventario, enderecoIpPublico);
            comando.Parameters.AddWithValue("momento", heartbeat.MomentoUtc);
            comando.Parameters.AddWithValue("processo", (object?)processoAtual ?? DBNull.Value);
            comando.Parameters.AddWithValue("agente", contexto.AgenteId);
            await comando.ExecuteNonQueryAsync(ct);
        }
        await AtualizarInventarioAtivoAsync(conexao, transacao, contexto.TenantId, contexto.AtivoId, null, heartbeat.Inventario, enderecoIpPublico, ct);
        await transacao.CommitAsync(ct);
    }

    public async Task InserirPeriodosAsync(ContextoAgente contexto, IEnumerable<PeriodoAtividade> periodos, CancellationToken ct = default)
    {
        var lista = periodos.Where(p => p.FimUtc >= p.InicioUtc).OrderBy(p => p.InicioUtc).ToList();
        if (lista.Count == 0) return;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        var exclusoes = await CarregarExclusoesAsync(conexao, contexto, ct);
        // Exclusão de aplicativo remove o contexto do processo, mas preserva o fato de atividade/ociosidade.
        lista = lista.Select(p => AplicativoExcluido(exclusoes, p.ProcessoExecutavel)
            ? p with { ProcessoExecutavel = null }
            : p).ToList();
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        const string sql = """
            INSERT INTO periodos_atividade(tenant_id,ativo_id,agente_id,usuario_windows,estado,inicio_utc,fim_utc,processo_executavel)
            VALUES(@tenant,@ativo,@agente,@usuario,@estado,@inicio,@fim,@processo);
            """;
        foreach (var periodo in lista)
        {
            await using var comando = new NpgsqlCommand(sql, conexao, transacao);
            comando.Parameters.AddWithValue("tenant", contexto.TenantId);
            comando.Parameters.AddWithValue("ativo", contexto.AtivoId);
            comando.Parameters.AddWithValue("agente", contexto.AgenteId);
            comando.Parameters.AddWithValue("usuario", periodo.UsuarioWindows);
            comando.Parameters.AddWithValue("estado", (short)periodo.Estado);
            comando.Parameters.AddWithValue("inicio", periodo.InicioUtc);
            comando.Parameters.AddWithValue("fim", periodo.FimUtc);
            comando.Parameters.AddWithValue("processo", (object?)periodo.ProcessoExecutavel ?? DBNull.Value);
            await comando.ExecuteNonQueryAsync(ct);
        }

        var ultimo = lista[^1];
        await using (var atualizar = new NpgsqlCommand("UPDATE agentes SET processo_atual=@processo, usuario_windows=@usuario, ultima_comunicacao_utc=GREATEST(COALESCE(ultima_comunicacao_utc,@momento),@momento), status='Online', atualizado_em_utc=now() WHERE id=@agente AND tenant_id=@tenant", conexao, transacao))
        {
            atualizar.Parameters.AddWithValue("processo", (object?)ultimo.ProcessoExecutavel ?? DBNull.Value);
            atualizar.Parameters.AddWithValue("usuario", ultimo.UsuarioWindows);
            atualizar.Parameters.AddWithValue("momento", ultimo.FimUtc);
            atualizar.Parameters.AddWithValue("agente", contexto.AgenteId);
            atualizar.Parameters.AddWithValue("tenant", contexto.TenantId);
            await atualizar.ExecuteNonQueryAsync(ct);
        }

        await transacao.CommitAsync(ct);
    }

    public async Task InserirNavegacaoAsync(ContextoAgente contexto, IEnumerable<PeriodoNavegacao> periodos, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);

        // A política também é validada no servidor. Um cliente alterado localmente não pode
        // forçar persistência de navegação quando o tenant desabilitou essa coleta.
        await using (var politica = new NpgsqlCommand(
            "SELECT COALESCE(monitorar_urls_dominios,false) FROM configuracoes_monitoramento WHERE tenant_id=@tenant", conexao))
        {
            politica.Parameters.AddWithValue("tenant", contexto.TenantId);
            var permitido = await politica.ExecuteScalarAsync(ct);
            if (permitido is not bool habilitado || !habilitado) return;
        }

        var agora = DateTimeOffset.UtcNow;
        var lista = periodos
            .Where(p => p.FimUtc >= p.InicioUtc)
            .Where(p => p.InicioUtc >= agora.AddDays(-7) && p.FimUtc <= agora.AddMinutes(5))
            .Where(p => p.FimUtc - p.InicioUtc <= TimeSpan.FromMinutes(10))
            .Select(p => new { Periodo = p, Dominio = NormalizarDominio(p.Dominio) })
            .Where(x => x.Dominio is not null)
            .Select(x => x.Periodo with
            {
                Dominio = x.Dominio!,
                Navegador = LimparLimitado(x.Periodo.Navegador, 40) ?? "Chromium",
                UsuarioWindows = LimparLimitado(x.Periodo.UsuarioWindows, 200) ?? "desconhecido"
            })
            .OrderBy(p => p.InicioUtc)
            .Take(1000)
            .ToList();
        var exclusoes = await CarregarExclusoesAsync(conexao, contexto, ct);
        lista = lista.Where(p => !DominioExcluido(exclusoes, p.Dominio)).ToList();
        if (lista.Count == 0) return;

        await using var transacao = await conexao.BeginTransactionAsync(ct);
        const string sql = """
            INSERT INTO sites_utilizados(tenant_id,ativo_id,agente_id,usuario_windows,dominio,navegador,inicio_utc,fim_utc)
            VALUES(@tenant,@ativo,@agente,@usuario,@dominio,@navegador,@inicio,@fim);
            """;
        foreach (var periodo in lista)
        {
            await using var comando = new NpgsqlCommand(sql, conexao, transacao);
            comando.Parameters.AddWithValue("tenant", contexto.TenantId);
            comando.Parameters.AddWithValue("ativo", contexto.AtivoId);
            comando.Parameters.AddWithValue("agente", contexto.AgenteId);
            comando.Parameters.AddWithValue("usuario", periodo.UsuarioWindows);
            comando.Parameters.AddWithValue("dominio", periodo.Dominio);
            comando.Parameters.AddWithValue("navegador", periodo.Navegador);
            comando.Parameters.AddWithValue("inicio", periodo.InicioUtc);
            comando.Parameters.AddWithValue("fim", periodo.FimUtc);
            await comando.ExecuteNonQueryAsync(ct);
        }
        await transacao.CommitAsync(ct);
    }


    private static async Task<Dictionary<string, HashSet<string>>> CarregarExclusoesAsync(NpgsqlConnection conexao, ContextoAgente contexto, CancellationToken ct)
    {
        const string sql="""
            SELECT x.tipo,lower(trim(x.valor))
            FROM exclusoes_monitoramento x
            JOIN ativos a ON a.id=@ativo AND a.tenant_id=@tenant
            WHERE x.tenant_id=@tenant AND x.ativo=true
              AND (x.departamento_id IS NULL OR x.departamento_id=a.departamento_id);
            """;
        var retorno=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Aplicativo"] = new(StringComparer.OrdinalIgnoreCase),
            ["Dominio"] = new(StringComparer.OrdinalIgnoreCase)
        };
        await using var cmd=new NpgsqlCommand(sql,conexao);cmd.Parameters.AddWithValue("tenant",contexto.TenantId);cmd.Parameters.AddWithValue("ativo",contexto.AtivoId);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))
        {
            var tipo=r.GetString(0);var valor=r.GetString(1);
            if(retorno.TryGetValue(tipo,out var set)&&!string.IsNullOrWhiteSpace(valor))set.Add(valor);
        }
        return retorno;
    }

    private static bool AplicativoExcluido(Dictionary<string,HashSet<string>> exclusoes,string? processo)
    {
        if(string.IsNullOrWhiteSpace(processo))return false;
        var original=processo.Trim();var nome=Path.GetFileName(original);
        return exclusoes["Aplicativo"].Any(x=>string.Equals(x,original,StringComparison.OrdinalIgnoreCase)||string.Equals(x,nome,StringComparison.OrdinalIgnoreCase));
    }

    private static bool DominioExcluido(Dictionary<string,HashSet<string>> exclusoes,string dominio)
    {
        var d=dominio.Trim().Trim('.').ToLowerInvariant();
        return exclusoes["Dominio"].Any(x=>{var e=x.Trim().Trim('.').ToLowerInvariant();return d==e||d.EndsWith("."+e,StringComparison.OrdinalIgnoreCase);});
    }

    public async Task RegistrarStatusAtualizacaoAsync(ContextoAgente contexto, StatusAtualizacaoAgente status, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO atualizacoes_agente(tenant_id,agente_id,versao_origem,versao_destino,status,detalhe,momento_utc)
            VALUES(@tenant,@agente,@origem,@destino,@status,@detalhe,@momento);
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("tenant", contexto.TenantId);
        comando.Parameters.AddWithValue("agente", contexto.AgenteId);
        comando.Parameters.AddWithValue("origem", status.VersaoOrigem);
        comando.Parameters.AddWithValue("destino", status.VersaoDestino);
        comando.Parameters.AddWithValue("status", status.Status);
        comando.Parameters.AddWithValue("detalhe", (object?)status.Detalhe ?? DBNull.Value);
        comando.Parameters.AddWithValue("momento", status.MomentoUtc);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Guid> LocalizarOuCriarAtivoAsync(NpgsqlConnection conexao, NpgsqlTransaction transacao, Guid tenantId, Guid empresaId, InventarioEquipamento inventario, string? ipPublico, CancellationToken ct)
    {
        Guid? existente = null;
        if (!string.IsNullOrWhiteSpace(inventario.NumeroSerie))
        {
            await using var localizarSerie = new NpgsqlCommand("SELECT id FROM ativos WHERE tenant_id=@tenant AND lower(numero_serie)=lower(@serie) AND ativo=true LIMIT 1", conexao, transacao);
            localizarSerie.Parameters.AddWithValue("tenant", tenantId);
            localizarSerie.Parameters.AddWithValue("serie", inventario.NumeroSerie.Trim());
            existente = (Guid?)(await localizarSerie.ExecuteScalarAsync(ct));
        }
        if (existente is null)
        {
            await using var localizarHost = new NpgsqlCommand("SELECT id FROM ativos WHERE tenant_id=@tenant AND lower(hostname)=lower(@hostname) AND ativo=true LIMIT 1", conexao, transacao);
            localizarHost.Parameters.AddWithValue("tenant", tenantId);
            localizarHost.Parameters.AddWithValue("hostname", inventario.Hostname.Trim());
            existente = (Guid?)(await localizarHost.ExecuteScalarAsync(ct));
        }
        if (existente is not null) return existente.Value;

        await ValidarCapacidadeNovoAtivoAsync(conexao, transacao, tenantId, ct);

        const string inserir = """
            INSERT INTO ativos(
                tenant_id,empresa_id,hostname,numero_serie,uuid_equipamento,fabricante,modelo,sistema_operacional,
                arquitetura,memoria_total_bytes,disco_total_bytes,usuario_windows_detectado,endereco_ip_local,endereco_ip_publico,
                primeira_comunicacao_utc,atualizado_em_utc,cadastro_completo)
            VALUES(
                @tenant,@empresa,@hostname,@serie,@uuid,@fabricante,@modelo,@so,
                @arquitetura,@memoria,@disco,@usuario,@ipLocal,CAST(@ipPublico AS inet),
                now(),now(),false)
            RETURNING id;
            """;
        await using var comando = new NpgsqlCommand(inserir, conexao, transacao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("empresa", empresaId);
        AdicionarParametrosInventarioSemVersao(comando, inventario, ipPublico);
        return (Guid)(await comando.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Não foi possível criar o ativo automaticamente."));
    }

    private static async Task AtualizarInventarioAtivoAsync(NpgsqlConnection conexao, NpgsqlTransaction transacao, Guid tenantId, Guid ativoId, Guid? empresaId, InventarioEquipamento inventario, string? ipPublico, CancellationToken ct)
    {
        const string sql = """
            UPDATE ativos SET
                empresa_id=COALESCE(@empresa,empresa_id),
                hostname=@hostname,
                numero_serie=COALESCE(@serie,numero_serie),
                uuid_equipamento=COALESCE(@uuid,uuid_equipamento),
                fabricante=COALESCE(@fabricante,fabricante),
                modelo=COALESCE(@modelo,modelo),
                sistema_operacional=COALESCE(@so,sistema_operacional),
                arquitetura=COALESCE(@arquitetura,arquitetura),
                memoria_total_bytes=COALESCE(@memoria,memoria_total_bytes),
                disco_total_bytes=COALESCE(@disco,disco_total_bytes),
                usuario_windows_detectado=COALESCE(@usuario,usuario_windows_detectado),
                endereco_ip_local=COALESCE(@ipLocal,endereco_ip_local),
                endereco_ip_publico=COALESCE(CAST(@ipPublico AS inet),endereco_ip_publico),
                primeira_comunicacao_utc=COALESCE(primeira_comunicacao_utc,now()),
                atualizado_em_utc=now(),
                ativo=true
            WHERE id=@ativo AND tenant_id=@tenant;
            """;
        await using var comando = new NpgsqlCommand(sql, conexao, transacao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("ativo", ativoId);
        comando.Parameters.Add("empresa", NpgsqlDbType.Uuid).Value = (object?)empresaId ?? DBNull.Value;
        AdicionarParametrosInventarioSemVersao(comando, inventario, ipPublico);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static void AdicionarParametrosInventario(NpgsqlCommand comando, Guid tenantId, Guid ativoId, string versao, InventarioEquipamento inventario, string? ipPublico)
    {
        comando.Parameters.AddWithValue("tenant", tenantId);
        comando.Parameters.AddWithValue("ativo", ativoId);
        comando.Parameters.AddWithValue("versao", versao);
        AdicionarParametrosInventarioSemVersao(comando, inventario, ipPublico);
    }

    private static void AdicionarParametrosInventarioSemVersao(NpgsqlCommand comando, InventarioEquipamento inventario, string? ipPublico)
    {
        comando.Parameters.AddWithValue("hostname", inventario.Hostname.Trim());
        comando.Parameters.Add("usuario", NpgsqlDbType.Varchar).Value = (object?)Limpar(inventario.UsuarioWindows) ?? DBNull.Value;
        comando.Parameters.Add("so", NpgsqlDbType.Varchar).Value = (object?)Limpar(inventario.SistemaOperacional) ?? DBNull.Value;
        comando.Parameters.Add("fabricante", NpgsqlDbType.Varchar).Value = (object?)Limpar(inventario.Fabricante) ?? DBNull.Value;
        comando.Parameters.Add("modelo", NpgsqlDbType.Varchar).Value = (object?)Limpar(inventario.Modelo) ?? DBNull.Value;
        comando.Parameters.Add("serie", NpgsqlDbType.Varchar).Value = (object?)Limpar(inventario.NumeroSerie) ?? DBNull.Value;
        comando.Parameters.Add("uuid", NpgsqlDbType.Varchar).Value = (object?)Limpar(inventario.UuidEquipamento) ?? DBNull.Value;
        comando.Parameters.Add("arquitetura", NpgsqlDbType.Varchar).Value = (object?)Limpar(inventario.Arquitetura) ?? DBNull.Value;
        comando.Parameters.Add("memoria", NpgsqlDbType.Bigint).Value = (object?)inventario.MemoriaTotalBytes ?? DBNull.Value;
        comando.Parameters.Add("disco", NpgsqlDbType.Bigint).Value = (object?)inventario.DiscoTotalBytes ?? DBNull.Value;
        comando.Parameters.Add("ipLocal", NpgsqlDbType.Varchar).Value = (object?)Limpar(inventario.EnderecoIpLocal) ?? DBNull.Value;
        comando.Parameters.Add("ipPublico", NpgsqlDbType.Varchar).Value = (object?)Limpar(ipPublico) ?? DBNull.Value;
    }

    private static async Task ValidarCapacidadeNovoAtivoAsync(NpgsqlConnection conexao, NpgsqlTransaction transacao, Guid tenantId, CancellationToken ct)
    {
        // Serializa cadastros de novas estações do mesmo tenant. O segundo comando é executado
        // depois do lock e, em READ COMMITTED, enxerga uma contagem atualizada após eventual concorrente.
        await using (var trava = new NpgsqlCommand("SELECT id FROM tenants WHERE id=@tenant AND ativo=true FOR UPDATE", conexao, transacao))
        {
            trava.Parameters.AddWithValue("tenant", tenantId);
            if (await trava.ExecuteScalarAsync(ct) is null)
                throw new InvalidOperationException("Tenant indisponível para registro da estação.");
        }

        const string sql = """
            SELECT COALESCE(s.status,'Ativa'), COALESCE(s.limite_estacoes,15), s.vencimento,
                   (SELECT count(*) FROM ativos a WHERE a.tenant_id=@tenant AND a.ativo=true)
            FROM tenants t
            LEFT JOIN assinaturas_tenant s ON s.tenant_id=t.id
            WHERE t.id=@tenant;
            """;
        await using var comando = new NpgsqlCommand(sql, conexao, transacao);
        comando.Parameters.AddWithValue("tenant", tenantId);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct)) throw new InvalidOperationException("Tenant indisponível para registro da estação.");
        var status = leitor.GetString(0);
        var limite = leitor.GetInt32(1);
        var vencimento = leitor.IsDBNull(2) ? (DateOnly?)null : leitor.GetFieldValue<DateOnly>(2);
        var total = Convert.ToInt32(leitor.GetInt64(3));
        if (status is not ("Ativa" or "Trial")) throw new InvalidOperationException("Assinatura sem permissão para novas estações.");
        if (vencimento.HasValue && vencimento.Value < DateOnly.FromDateTime(DateTime.UtcNow)) throw new InvalidOperationException("Assinatura vencida para novas estações.");
        if (total >= limite) throw new InvalidOperationException($"Limite de estações do plano atingido ({total}/{limite}).");
    }

    private static string? NormalizarDominio(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var dominio = valor.Trim().Trim('.').ToLowerInvariant();
        if (dominio.StartsWith("www.", StringComparison.Ordinal)) dominio = dominio[4..];
        if (dominio.Length > 500 || dominio.Contains('/') || dominio.Contains('\\') || dominio.Contains(':') || dominio.Contains(' ')) return null;
        return Uri.CheckHostName(dominio) is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6 ? dominio : null;
    }

    private static string? LimparLimitado(string? valor, int limite)
    {
        var limpo = Limpar(valor);
        if (limpo is null) return null;
        return limpo.Length <= limite ? limpo : limpo[..limite];
    }

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

public sealed record ContextoAgente(Guid AgenteId, Guid TenantId, Guid AtivoId);
