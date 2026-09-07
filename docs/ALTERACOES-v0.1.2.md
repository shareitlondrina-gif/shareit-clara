# Share IT .Clara v0.1.2 — Alterações

## Correções principais

- Health check `/api/health` agora é mapeado diretamente no `Program.cs`, sem depender da descoberta de controllers.
- Novos endpoints técnicos: `/api/health/live` e `/api/versao`.
- O publicador valida não apenas HTTP, mas também a versão publicada e o banco `shareit_clara`.
- HTTP 404 no health agora é identificado explicitamente como serviço existente sem a versão esperada do .Clara.
- O `PUBLICAR-CLARA.cmd` não fecha automaticamente; sempre mantém o resumo/erro visível com `pause`.
- Quando a versão online é confirmada, o publicador pode abrir automaticamente a URL do sistema.
- Logs de publicação são gravados em `artifacts\logs`, fora de `C:\ConfigSys\Clara`.
- `render.yaml` usa Virginia, plano Free, `/api/health` e `autoDeploy: true`.
- Configuração exemplo passa a usar `https://shareit-clara.onrender.com` e Git push habilitado por padrão.

## Segurança preservada

- Banco de destino continua obrigatoriamente `shareit_clara`.
- Nenhuma migration é executada em `shareit_visao`.
- Não há DROP/restore automático de banco.
- O banco legado `monitor` não participa deste projeto.

- Compatibilidade com ConfigSys das versões 0.1.0/0.1.1: URLs `*.exemplo.com.br` são tratadas como placeholder e substituídas em runtime pelo endereço `shareit-clara.onrender.com`; se já houver repositório Git local ou URL do GitHub configurada, a publicação automática pode fazer push mesmo que o XML antigo tenha vindo com `ExecutarGitPush=false`.
