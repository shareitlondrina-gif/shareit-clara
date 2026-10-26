// Clara
// Programador: Paulo Andrade
// Objetivo: executar uma regra de aplicação da Clara.
// Origem: controladores, configuração e serviços hospedados.
// Destino: repositórios, integrações ou eventos da aplicação.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Clara.Dados.Banco;

namespace Clara.Web.Servicos;

public sealed record ConfiguracaoArmazenamentoResumo(string Provedor,string? Endpoint,string? ContainerBucket,string PastaBase,string Regiao,string Status,DateTimeOffset? UltimoTesteUtc,string? UltimoErro,bool CofreConfigurado);
public sealed record EvidenciaArmazenada(Guid Id,string MimeType,long Tamanho,string Sha256,string ChaveObjeto);

/// <summary>
/// Objetivo: manter binários de evidências fora do PostgreSQL e servir arquivos somente após autorização da Clara.
/// Provedores ativos nesta RC: R2/S3 compatível e Local (homologação). SharePoint/Google Drive/HostGator ficam catalogados para conector posterior.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServicoArmazenamentoEvidencias(FabricaConexoesClara fabrica, CriptografiaSegredosClara crypto, IHttpClientFactory httpFactory, IWebHostEnvironment ambiente)
{
    private sealed record CredencialS3(string AccessKeyId,string SecretAccessKey);
    private sealed record Config(Guid TenantId,string Provedor,string? Endpoint,string? Bucket,string Pasta,string Regiao,string? SegredoId);

    public async Task<ConfiguracaoArmazenamentoResumo> ObterConfiguracaoAsync(Guid tenantId,CancellationToken ct=default)
    {
        await using var c=fabrica.Criar();await c.OpenAsync(ct);
        await using var q=new NpgsqlCommand("""
            SELECT provedor,endpoint,container_bucket,pasta_base,regiao,status,ultimo_teste_utc,ultimo_erro
            FROM configuracoes_armazenamento WHERE tenant_id=@t
            """,c);q.Parameters.AddWithValue("t",tenantId);
        await using var r=await q.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct)) return new("R2S3",null,null,"clara/evidencias","auto","NaoConfigurado",null,null,crypto.Configurada);
        return new(r.GetString(0),r.IsDBNull(1)?null:r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.IsDBNull(6)?null:new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(6),DateTimeKind.Utc)),r.IsDBNull(7)?null:r.GetString(7),crypto.Configurada);
    }

    public async Task SalvarConfiguracaoAsync(Guid tenantId,string provedor,string? endpoint,string? bucket,string? pasta,string? regiao,string? accessKey,string? secretKey,CancellationToken ct=default)
    {
        provedor = provedor switch { "Local"=>"Local","SharePoint"=>"SharePoint","GoogleDrive"=>"GoogleDrive","HostGatorSftp"=>"HostGatorSftp",_=>"R2S3" };
        pasta = NormalizarChave(string.IsNullOrWhiteSpace(pasta)?"clara/evidencias":pasta!);
        regiao = string.IsNullOrWhiteSpace(regiao)?"auto":regiao.Trim();
        string? segredoId=null;
        await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
        if(provedor=="R2S3" && (!string.IsNullOrWhiteSpace(accessKey)||!string.IsNullOrWhiteSpace(secretKey)))
        {
            if(string.IsNullOrWhiteSpace(accessKey)||string.IsNullOrWhiteSpace(secretKey))throw new ArgumentException("Informe Access Key ID e Secret Access Key.");
            var segredo=JsonSerializer.Serialize(new CredencialS3(accessKey.Trim(),secretKey.Trim()));var e=crypto.Cifrar(segredo);
            segredoId=$"storage:{tenantId:N}";
            await using var s=new NpgsqlCommand("""
                INSERT INTO segredos_integracao(segredo_id,tenant_id,finalidade,cipher_base64,nonce_base64,tag_base64)
                VALUES(@id,@t,'Armazenamento',@c,@n,@g)
                ON CONFLICT(segredo_id) DO UPDATE SET cipher_base64=excluded.cipher_base64,nonce_base64=excluded.nonce_base64,tag_base64=excluded.tag_base64,atualizado_em_utc=now();
                """,c,tx);s.Parameters.AddWithValue("id",segredoId);s.Parameters.AddWithValue("t",tenantId);s.Parameters.AddWithValue("c",e.Cipher);s.Parameters.AddWithValue("n",e.Nonce);s.Parameters.AddWithValue("g",e.Tag);await s.ExecuteNonQueryAsync(ct);
        }
        else
        {
            await using var old=new NpgsqlCommand("SELECT segredo_id FROM configuracoes_armazenamento WHERE tenant_id=@t",c,tx);old.Parameters.AddWithValue("t",tenantId);segredoId=await old.ExecuteScalarAsync(ct) as string;
        }
        var status=provedor is "R2S3" or "Local"?"Configurado":"ConectorPendente";
        await using var q=new NpgsqlCommand("""
            INSERT INTO configuracoes_armazenamento(tenant_id,provedor,endpoint,container_bucket,pasta_base,regiao,segredo_id,status,ultimo_erro,atualizado_em_utc)
            VALUES(@t,@p,@e,@b,@f,@r,@s,@status,NULL,now())
            ON CONFLICT(tenant_id) DO UPDATE SET provedor=excluded.provedor,endpoint=excluded.endpoint,container_bucket=excluded.container_bucket,pasta_base=excluded.pasta_base,regiao=excluded.regiao,segredo_id=excluded.segredo_id,status=excluded.status,ultimo_erro=NULL,atualizado_em_utc=now();
            """,c,tx);q.Parameters.AddWithValue("t",tenantId);q.Parameters.AddWithValue("p",provedor);q.Parameters.AddWithValue("e",(object?)endpoint?.Trim()??DBNull.Value);q.Parameters.AddWithValue("b",(object?)bucket?.Trim()??DBNull.Value);q.Parameters.AddWithValue("f",pasta);q.Parameters.AddWithValue("r",regiao);q.Parameters.AddWithValue("s",(object?)segredoId??DBNull.Value);q.Parameters.AddWithValue("status",status);await q.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task TestarAsync(Guid tenantId,CancellationToken ct=default)
    {
        var cfg=await CarregarAsync(tenantId,ct);string? erro=null;
        try
        {
            var dados=Encoding.UTF8.GetBytes("clara-storage-test");var chave=$"{cfg.Pasta}/_teste/{Guid.NewGuid():N}.txt";
            await GravarObjetoAsync(cfg,chave,"text/plain",dados,ct);await ExcluirObjetoAsync(cfg,chave,ct);
        }
        catch(Exception ex){erro=ex.Message;throw;}
        finally
        {
            await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var q=new NpgsqlCommand("UPDATE configuracoes_armazenamento SET status=@s,ultimo_teste_utc=now(),ultimo_erro=@e,atualizado_em_utc=now() WHERE tenant_id=@t",c);q.Parameters.AddWithValue("s",erro is null?"Ativo":"Erro");q.Parameters.AddWithValue("e",(object?)erro??DBNull.Value);q.Parameters.AddWithValue("t",tenantId);await q.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<EvidenciaArmazenada> GravarEvidenciaAsync(Guid tenantId,Guid ativoId,string titulo,string? descricao,string mimeType,Stream conteudo,DateTimeOffset capturadaEm,CancellationToken ct=default)
    {
        if(mimeType is not ("image/png" or "image/jpeg" or "image/webp"))throw new InvalidOperationException("Formato de evidência não permitido.");
        using var ms=new MemoryStream();await conteudo.CopyToAsync(ms,ct);if(ms.Length<=0||ms.Length>15*1024*1024)throw new InvalidOperationException("A imagem deve possuir entre 1 byte e 15 MB.");
        var bytes=ms.ToArray();var sha=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();var ext=mimeType=="image/png"?"png":mimeType=="image/webp"?"webp":"jpg";var id=Guid.NewGuid();
        var cfg=await CarregarAsync(tenantId,ct);var chave=$"{cfg.Pasta}/{tenantId:N}/{capturadaEm:yyyy/MM/dd}/{id:N}.{ext}";await GravarObjetoAsync(cfg,chave,mimeType,bytes,ct);
        await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var q=new NpgsqlCommand("""
            INSERT INTO evidencias_visuais(id,tenant_id,ativo_id,pessoa,tipo,titulo,descricao,arquivo_relativo,capturada_em_utc,demonstrativa,provedor_armazenamento,chave_objeto,mime_type,tamanho_bytes,sha256)
            SELECT @id,@t,@a,COALESCE(at.responsavel,at.usuario_windows_detectado),'Screenshot',@titulo,@descricao,@url,@captura,false,@provedor,@chave,@mime,@tam,@sha
            FROM ativos at WHERE at.id=@a AND at.tenant_id=@t;
            """,c);q.Parameters.AddWithValue("id",id);q.Parameters.AddWithValue("t",tenantId);q.Parameters.AddWithValue("a",ativoId);q.Parameters.AddWithValue("titulo",string.IsNullOrWhiteSpace(titulo)?"Captura autorizada":titulo.Trim());q.Parameters.AddWithValue("descricao",(object?)descricao?.Trim()??DBNull.Value);q.Parameters.AddWithValue("url",$"/evidencias/arquivo/{id}");q.Parameters.AddWithValue("captura",capturadaEm);q.Parameters.AddWithValue("provedor",cfg.Provedor);q.Parameters.AddWithValue("chave",chave);q.Parameters.AddWithValue("mime",mimeType);q.Parameters.AddWithValue("tam",bytes.LongLength);q.Parameters.AddWithValue("sha",sha);if(await q.ExecuteNonQueryAsync(ct)!=1){await ExcluirObjetoAsync(cfg,chave,ct);throw new InvalidOperationException("Dispositivo não pertence ao tenant autenticado.");}
        return new(id,mimeType,bytes.LongLength,sha,chave);
    }

    public async Task<int> ExcluirExpiradasAsync(Guid tenantId,int dias,CancellationToken ct=default)
    {
        dias=Math.Clamp(dias,1,3650);var itens=new List<(Guid Id,string Chave)>();await using(var c=fabrica.Criar()){await c.OpenAsync(ct);await using var q=new NpgsqlCommand("SELECT id,chave_objeto FROM evidencias_visuais WHERE tenant_id=@t AND demonstrativa=false AND capturada_em_utc < now()-(@dias*interval '1 day') AND chave_objeto IS NOT NULL",c);q.Parameters.AddWithValue("t",tenantId);q.Parameters.AddWithValue("dias",dias);await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))itens.Add((r.GetGuid(0),r.GetString(1)));}
        if(itens.Count==0)return 0;var cfg=await CarregarAsync(tenantId,ct);var removidos=0;
        foreach(var item in itens){await ExcluirObjetoAsync(cfg,item.Chave,ct);await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var q=new NpgsqlCommand("DELETE FROM evidencias_visuais WHERE id=@id AND tenant_id=@t",c);q.Parameters.AddWithValue("id",item.Id);q.Parameters.AddWithValue("t",tenantId);removidos+=await q.ExecuteNonQueryAsync(ct);}
        return removidos;
    }

    public async Task<(Stream Conteudo,string MimeType)?> LerEvidenciaAsync(Guid tenantId,Guid evidenciaId,CancellationToken ct=default)
    {
        await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var q=new NpgsqlCommand("SELECT provedor_armazenamento,chave_objeto,mime_type FROM evidencias_visuais WHERE id=@id AND tenant_id=@t AND demonstrativa=false",c);q.Parameters.AddWithValue("id",evidenciaId);q.Parameters.AddWithValue("t",tenantId);await using var r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct)||r.IsDBNull(1))return null;var chave=r.GetString(1);var mime=r.IsDBNull(2)?"application/octet-stream":r.GetString(2);await r.DisposeAsync();var cfg=await CarregarAsync(tenantId,ct);return(await LerObjetoAsync(cfg,chave,ct),mime);
    }

    private async Task<Config> CarregarAsync(Guid tenantId,CancellationToken ct)
    {
        await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var q=new NpgsqlCommand("SELECT provedor,endpoint,container_bucket,pasta_base,regiao,segredo_id FROM configuracoes_armazenamento WHERE tenant_id=@t",c);q.Parameters.AddWithValue("t",tenantId);await using var r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new InvalidOperationException("Configure o armazenamento em Administração → Armazenamento antes de gravar evidências.");return new(tenantId,r.GetString(0),r.IsDBNull(1)?null:r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.GetString(3),r.GetString(4),r.IsDBNull(5)?null:r.GetString(5));
    }

    private async Task<CredencialS3> CredencialAsync(Config cfg,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(cfg.SegredoId))throw new InvalidOperationException("Credencial R2/S3 não configurada.");await using var c=fabrica.Criar();await c.OpenAsync(ct);await using var q=new NpgsqlCommand("SELECT cipher_base64,nonce_base64,tag_base64 FROM segredos_integracao WHERE segredo_id=@id AND tenant_id=@t",c);q.Parameters.AddWithValue("id",cfg.SegredoId);q.Parameters.AddWithValue("t",cfg.TenantId);await using var r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new InvalidOperationException("Segredo de armazenamento não encontrado.");return JsonSerializer.Deserialize<CredencialS3>(crypto.Decifrar(r.GetString(0),r.GetString(1),r.GetString(2)),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidOperationException("Credencial de armazenamento inválida.");
    }

    private async Task GravarObjetoAsync(Config cfg,string chave,string mime,byte[] dados,CancellationToken ct)
    {
        if(cfg.Provedor=="Local"){var root=Environment.GetEnvironmentVariable("CLARA_ARMAZENAMENTO_LOCAL_ROOT")??Path.Combine(ambiente.ContentRootPath,"dados-evidencias");var path=CaminhoLocal(root,chave);Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllBytesAsync(path,dados,ct);return;}
        if(cfg.Provedor!="R2S3")throw new NotSupportedException($"O conector {cfg.Provedor} está catalogado, mas será ativado após a fundação R2/S3.");
        using var req=await RequisicaoS3Async(cfg,HttpMethod.Put,chave,dados,mime,ct);using var res=await httpFactory.CreateClient().SendAsync(req,ct);if(!res.IsSuccessStatusCode)throw new InvalidOperationException($"Armazenamento R2/S3 recusou o upload ({(int)res.StatusCode}).");
    }
    private async Task<Stream> LerObjetoAsync(Config cfg,string chave,CancellationToken ct)
    {
        if(cfg.Provedor=="Local"){var root=Environment.GetEnvironmentVariable("CLARA_ARMAZENAMENTO_LOCAL_ROOT")??Path.Combine(ambiente.ContentRootPath,"dados-evidencias");return File.OpenRead(CaminhoLocal(root,chave));}
        using var req=await RequisicaoS3Async(cfg,HttpMethod.Get,chave,Array.Empty<byte>(),null,ct);var res=await httpFactory.CreateClient().SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);if(!res.IsSuccessStatusCode){res.Dispose();throw new FileNotFoundException("Evidência não encontrada no armazenamento externo.");}var ms=new MemoryStream();await res.Content.CopyToAsync(ms,ct);res.Dispose();ms.Position=0;return ms;
    }
    private async Task ExcluirObjetoAsync(Config cfg,string chave,CancellationToken ct)
    {
        if(cfg.Provedor=="Local"){var root=Environment.GetEnvironmentVariable("CLARA_ARMAZENAMENTO_LOCAL_ROOT")??Path.Combine(ambiente.ContentRootPath,"dados-evidencias");var p=CaminhoLocal(root,chave);if(File.Exists(p))File.Delete(p);return;}
        if(cfg.Provedor!="R2S3")return;using var req=await RequisicaoS3Async(cfg,HttpMethod.Delete,chave,Array.Empty<byte>(),null,ct);using var res=await httpFactory.CreateClient().SendAsync(req,ct);if(!res.IsSuccessStatusCode&&res.StatusCode!=System.Net.HttpStatusCode.NotFound)throw new InvalidOperationException($"Falha ao excluir objeto R2/S3 ({(int)res.StatusCode}).");
    }

    private async Task<HttpRequestMessage> RequisicaoS3Async(Config cfg,HttpMethod metodo,string chave,byte[] corpo,string? mime,CancellationToken ct)
    {
        if(!Uri.TryCreate(cfg.Endpoint,UriKind.Absolute,out var endpoint)||string.IsNullOrWhiteSpace(cfg.Bucket))throw new InvalidOperationException("Endpoint e bucket R2/S3 são obrigatórios.");var cred=await CredencialAsync(cfg,ct);var agora=DateTimeOffset.UtcNow;var amz=agora.ToString("yyyyMMdd'T'HHmmss'Z'",CultureInfo.InvariantCulture);var data=agora.ToString("yyyyMMdd",CultureInfo.InvariantCulture);var regiao=string.IsNullOrWhiteSpace(cfg.Regiao)?"auto":cfg.Regiao;var key=string.Join('/',chave.Split('/',StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));var uri=new Uri(endpoint.ToString().TrimEnd('/')+"/"+Uri.EscapeDataString(cfg.Bucket)+"/"+key);var payloadHash=Hex(SHA256.HashData(corpo));var headers=$"host:{uri.Host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amz}";const string signed="host;x-amz-content-sha256;x-amz-date";var canonical=$"{metodo.Method}\n{uri.AbsolutePath}\n\n{headers}\n\n{signed}\n{payloadHash}";var scope=$"{data}/{regiao}/s3/aws4_request";var stringToSign=$"AWS4-HMAC-SHA256\n{amz}\n{scope}\n{Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))}";var kDate=Hmac(Encoding.UTF8.GetBytes("AWS4"+cred.SecretAccessKey),data);var kRegion=Hmac(kDate,regiao);var kService=Hmac(kRegion,"s3");var kSigning=Hmac(kService,"aws4_request");var signature=Hex(Hmac(kSigning,stringToSign));var req=new HttpRequestMessage(metodo,uri);req.Headers.TryAddWithoutValidation("x-amz-date",amz);req.Headers.TryAddWithoutValidation("x-amz-content-sha256",payloadHash);req.Headers.TryAddWithoutValidation("Authorization",$"AWS4-HMAC-SHA256 Credential={cred.AccessKeyId}/{scope}, SignedHeaders={signed}, Signature={signature}");if(metodo==HttpMethod.Put){req.Content=new ByteArrayContent(corpo);req.Content.Headers.ContentType=new MediaTypeHeaderValue(mime??"application/octet-stream");}return req;
    }
    private static byte[] Hmac(byte[] key,string data){using var h=new HMACSHA256(key);return h.ComputeHash(Encoding.UTF8.GetBytes(data));}
    private static string Hex(byte[] b)=>Convert.ToHexString(b).ToLowerInvariant();
    private static string NormalizarChave(string s)=>string.Join('/',s.Replace('\\','/').Split('/',StringSplitOptions.RemoveEmptyEntries).Where(x=>x is not "." and not ".."));
    private static string CaminhoLocal(string root,string chave){var full=Path.GetFullPath(Path.Combine(root,NormalizarChave(chave).Replace('/',Path.DirectorySeparatorChar)));var basePath=Path.GetFullPath(root)+Path.DirectorySeparatorChar;if(!full.StartsWith(basePath,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Chave de armazenamento inválida.");return full;}
}
