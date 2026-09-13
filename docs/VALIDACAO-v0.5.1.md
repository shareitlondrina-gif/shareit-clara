# Validação — Share IT .Clara v0.5.1

A v0.5.1 deve ser publicada pelo fluxo oficial `PUBLICAR-CLARA.cmd` no Windows.

## Gate obrigatório

O publicador deve confirmar:

1. .NET SDK 10;
2. restore;
3. build Release;
4. testes;
5. publish dos componentes Windows;
6. pacote do agente e instalador;
7. SHA-256 dos artefatos;
8. proteção contra segredos no release;
9. Git sincronizado sem force push;
10. `HEAD` local igual ao SHA da branch remota após push;
11. `/api/health/live` com `versao=0.5.1` e `buildId` igual ao da execução;
12. `/api/health` com o mesmo `buildId`, `banco=ok`, `nomeBanco=shareit_clara` e `migracoes=6`;
13. `/api/versao` com `versao=0.5.1` e o mesmo `buildId`.

## Evidências locais esperadas

Mesmo quando o Render falhar ou permanecer numa versão antiga, devem existir:

```text
PUBLICACAO-ULTIMA.log
DIAGNOSTICO-PUBLICACAO-ULTIMO.txt
artifacts\logs\Publicacao-Clara-v0.5.1-*.log
artifacts\logs\DIAGNOSTICO-PUBLICACAO-v0.5.1-*.txt
artifacts\logs\Publicador-Launcher.log
```

## Interpretação do cenário observado anteriormente

Se Git local/remoto estiverem no mesmo commit da v0.5.1, mas `/api/health/live` continuar respondendo `0.3.2`, o problema fica isolado entre o disparo/configuração do Render e o deploy do Web Service. Nesse caso, revisar no Render:

- repositório conectado ao serviço `shareit-clara`;
- branch conectada (`main`);
- Auto Deploy;
- último deploy e seu status/log;
- Dockerfile/Root Directory usados pelo serviço.

## Deploy Hook opcional

Pode ser configurado localmente pela variável:

```text
CLARA_RENDER_DEPLOY_HOOK
```

Não incluir a URL do hook no Git, ZIP, XML público ou evidências compartilhadas.

## Banco

Esperado: `6` migrations. Não criar nem editar migration para esta correção de publicação.
