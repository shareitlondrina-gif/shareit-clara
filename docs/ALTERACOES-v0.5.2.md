# Alterações — Share IT .Clara v0.5.2

## Objetivo

A v0.5.2 é uma versão de correção e robustez do processo de publicação. Ela preserva todas as funcionalidades da v0.5.1 e corrige o falso erro observado quando o `git push` conclui, mas a consulta de confirmação do SHA remoto falha temporariamente.

## Git / GitHub

- `git push` continua sem `--force`;
- o `HEAD` local continua sendo o commit esperado;
- a confirmação com `git ls-remote` passa a ter até 6 tentativas;
- se o GitHub responder com SHA diferente de forma persistente, a publicação falha;
- se `git push` retornar sucesso e somente a confirmação `ls-remote` estiver indisponível em todas as tentativas, o publicador registra aviso e segue para o gate online do Render;
- o diagnóstico registra se o SHA remoto foi confirmado imediatamente após o push.

## Render

- HTTP 503 durante a janela de deploy é tratado como estado transitório de reinício/inicialização;
- o publicador continua aguardando até o tempo máximo configurado;
- a publicação só termina como validada quando o Web/API informa a versão e o Build ID desta execução e o health completo confirma `shareit_clara` e a quantidade esperada de migrations;
- Deploy Hook continua opcional como fallback.

## Organização de logs

Todos os arquivos operacionais de publicação passam para a pasta `logs` na raiz da release:

```text
logs\
  BUILD.txt
  PUBLICACAO-ULTIMA.log
  DIAGNOSTICO-PUBLICACAO-ULTIMO.txt
  Publicador-Launcher.log
  Publicacao-Clara-v0.5.2-AAAAMMDD-HHMMSS.log
  DIAGNOSTICO-PUBLICACAO-v0.5.2-AAAAMMDD-HHMMSS.txt
```

Não são mais usados `artifacts\logs`, `PUBLICACAO-ULTIMA.log` na raiz ou `DIAGNOSTICO-PUBLICACAO-ULTIMO.txt` na raiz.

`logs/BUILD.txt` permanece versionado porque identifica a compilação publicada. Os demais arquivos locais da pasta `logs` são ignorados pelo Git para não enviar evidências de execução ao repositório. O projeto Web copia `logs/BUILD.txt` como `BUILD.txt` no publish para manter os endpoints de versão/health compatíveis.

## Banco

Nenhuma migration nova. Migrations `0001` a `0006` permanecem inalteradas.
