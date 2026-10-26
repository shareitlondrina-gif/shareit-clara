# Configuração de e-mail — Clara v0.5.7

## Remetente padrão

`Clara <no-reply@clara.local>`

A v0.5.7 prepara a aplicação para usar esse endereço como identidade padrão de envio. A caixa/endereço e o transporte SMTP precisam ser criados/configurados no provedor de e-mail do domínio `clarabrasil.com.br`.

## Variáveis no Render

Configure no serviço `clara`:

- `CLARA_EMAIL_SMTP_HOST` — host SMTP;
- `CLARA_EMAIL_SMTP_PORT` — padrão sugerido 587;
- `CLARA_EMAIL_SMTP_SSL` — `true` quando o provedor exigir TLS;
- `CLARA_EMAIL_SMTP_USER` — usuário/conta SMTP;
- `CLARA_EMAIL_SMTP_PASSWORD` — segredo SMTP;
- `CLARA_EMAIL_FROM` — `no-reply@clara.local`;
- `CLARA_EMAIL_FROM_NAME` — `Clara`.

Nunca versionar senha SMTP, senha de aplicação ou token de API.

## Tenant

O administrador do cliente pode configurar:

- Reply-To próprio da empresa;
- destinatários adicionais;
- e-mails externos nas regras de alerta.

Assim, um gestor/Jurídico/RH pode receber mensagens sem necessariamente ter uma conta de acesso à plataforma.

## Entregabilidade

Antes de produção, configurar adequadamente SPF, DKIM e DMARC do domínio/remetente conforme o provedor utilizado. A tela Governança possui ação de e-mail de teste para validar o transporte depois da configuração.
