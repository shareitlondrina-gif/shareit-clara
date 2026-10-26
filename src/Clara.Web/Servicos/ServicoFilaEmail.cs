// Clara
// Programador: Paulo Andrade
// Objetivo: executar uma regra de aplicação da Clara.
// Origem: controladores, configuração e serviços hospedados.
// Destino: repositórios, integrações ou eventos da aplicação.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Net;
using System.Net.Mail;
using Npgsql;
using Clara.Dados.Banco;

namespace Clara.Web.Servicos;

/// <summary>
/// Objetivo: enviar a caixa de saída de alertas/aprovações usando credenciais mantidas como segredo do ambiente.
/// Origem: fila_emails.
/// Destino: SMTP configurado no Render/ambiente.
/// Segurança: senha SMTP nunca é persistida no banco ou escrita em log.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ServicoFilaEmail(
    FabricaConexoesClara fabrica,
    IConfiguration configuracao,
    EstadoInicializacaoBanco estadoBanco,
    ILogger<ServicoFilaEmail> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (estadoBanco.ConcluidaComSucesso && TransporteConfigurado())
                    await ProcessarAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha temporária ao processar a fila de e-mails da Clara.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private bool TransporteConfigurado()
        => !string.IsNullOrWhiteSpace(configuracao["CLARA_EMAIL_SMTP_HOST"]);

    private async Task ProcessarAsync(CancellationToken ct)
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);

        long id;
        string destinatario;
        string assunto;
        string corpo;
        Guid? tenantId;

        await using (var transacao = await conexao.BeginTransactionAsync(ct))
        {
            const string selecionar = """
                SELECT id,tenant_id,destinatario,assunto,corpo_texto
                FROM fila_emails
                WHERE status IN ('Pendente','Erro') AND proxima_tentativa_utc<=now() AND tentativas<5
                ORDER BY criado_em_utc
                FOR UPDATE SKIP LOCKED
                LIMIT 1;
                """;
            await using var cmd = new NpgsqlCommand(selecionar, conexao, transacao);
            await using var leitor = await cmd.ExecuteReaderAsync(ct);
            if (!await leitor.ReadAsync(ct))
            {
                await transacao.RollbackAsync(ct);
                return;
            }
            id = leitor.GetInt64(0);
            tenantId = leitor.IsDBNull(1) ? null : leitor.GetGuid(1);
            destinatario = leitor.GetString(2);
            assunto = leitor.GetString(3);
            corpo = leitor.GetString(4);
            await leitor.CloseAsync();

            await using var reservar = new NpgsqlCommand("UPDATE fila_emails SET status='Enviando',tentativas=tentativas+1 WHERE id=@id", conexao, transacao);
            reservar.Parameters.AddWithValue("id", id);
            await reservar.ExecuteNonQueryAsync(ct);
            await transacao.CommitAsync(ct);
        }

        try
        {
            var replyTo = tenantId.HasValue ? await ObterReplyToAsync(tenantId.Value, ct) : null;
            using var mensagem = new MailMessage
            {
                From = new MailAddress(
                    configuracao["CLARA_EMAIL_FROM"] ?? "no-reply@clara.local",
                    configuracao["CLARA_EMAIL_FROM_NAME"] ?? "Clara"),
                Subject = assunto,
                Body = corpo,
                IsBodyHtml = false
            };
            mensagem.To.Add(destinatario);
            if (!string.IsNullOrWhiteSpace(replyTo)) mensagem.ReplyToList.Add(replyTo);

            var host = configuracao["CLARA_EMAIL_SMTP_HOST"]!;
            var porta = int.TryParse(configuracao["CLARA_EMAIL_SMTP_PORT"], out var p) ? p : 587;
            var ssl = !bool.TryParse(configuracao["CLARA_EMAIL_SMTP_SSL"], out var s) || s;
            using var smtp = new SmtpClient(host, porta) { EnableSsl = ssl };
            var usuario = configuracao["CLARA_EMAIL_SMTP_USER"];
            var senha = configuracao["CLARA_EMAIL_SMTP_PASSWORD"];
            if (!string.IsNullOrWhiteSpace(usuario)) smtp.Credentials = new NetworkCredential(usuario, senha);
            await smtp.SendMailAsync(mensagem);

            await AtualizarStatusAsync(id, "Enviado", null, true, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Falha ao enviar e-mail da fila {EmailId}: {TipoErro}", id, ex.GetType().Name);
            await AtualizarStatusAsync(id, "Erro", ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message, false, ct);
        }
    }

    private async Task<string?> ObterReplyToAsync(Guid tenantId, CancellationToken ct)
    {
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT reply_to FROM configuracoes_email_tenant WHERE tenant_id=@tenant", conexao);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        return Convert.ToString(await cmd.ExecuteScalarAsync(ct));
    }

    private async Task AtualizarStatusAsync(long id, string status, string? erro, bool enviado, CancellationToken ct)
    {
        const string sql = """
            UPDATE fila_emails
            SET status=@status,ultimo_erro=@erro,
                enviado_em_utc=CASE WHEN @enviado THEN now() ELSE enviado_em_utc END,
                proxima_tentativa_utc=CASE WHEN @enviado THEN proxima_tentativa_utc ELSE now()+interval '10 minutes' END
            WHERE id=@id;
            """;
        await using var conexao = fabrica.Criar();
        await conexao.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conexao);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("enviado", enviado);
        cmd.Parameters.AddWithValue("erro", (object?)erro ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
