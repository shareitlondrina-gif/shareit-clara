# Clara v0.3.1 - Alterações

## Correção de deploy e diagnóstico no Render

A v0.3.1 é uma correção intermediária porque a publicação da v0.3.0 compilou e foi enviada, mas o endpoint `/api/health` não confirmou a baseline dentro do tempo de espera do publicador.

Alterações:

- a Web/API sobe antes da inicialização do PostgreSQL;
- `/api/health/live` responde sem depender de banco, migrations ou bootstrap;
- `render.yaml` usa `/api/health/live` como health check da plataforma;
- migrations e bootstrap continuam automáticos, mas uma falha não encerra imediatamente a Web/API;
- `/api/health` permanece como readiness completo e só retorna sucesso quando banco `clara` e migrations estão válidos;
- erros de inicialização são gravados nos logs do serviço sem expor detalhes de conexão no endpoint público;
- `PUBLICAR-CLARA.cmd` diferencia liveness e readiness;
- o publicador usa timeouts menores e respeita uma janela total de validação;
- o ZIP local é criado antes da espera pelo Render e não é perdido quando a validação online falha.

## Banco

- nenhuma migration nova;
- migrations publicadas `0001` a `0004` permanecem imutáveis;
- quantidade esperada: **4**.
