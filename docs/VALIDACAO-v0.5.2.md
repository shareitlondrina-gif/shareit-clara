# Validação — Clara v0.5.2

Executar a homologação no Windows por `PUBLICAR-CLARA.cmd`.

## Gate obrigatório

1. .NET SDK 10 disponível;
2. restore, build Release e testes concluídos;
3. componentes Windows e Web/API publicados;
4. pacote do agente e hashes gerados;
5. nenhum segredo incluído nos artefatos;
6. sincronização Git sem force push;
7. `git push` concluído com código 0;
8. confirmação do SHA remoto feita com retentativas quando disponível;
9. indisponibilidade transitória de `ls-remote` não pode encerrar a publicação se o push concluiu;
10. divergência real/persistente de SHA deve continuar bloqueando;
11. `/api/health/live` deve responder `versao=0.5.2` e `buildId` da execução;
12. `/api/health` deve responder o mesmo build, `banco=ok`, `nomeBanco=clara` e `migracoes=6`;
13. `/api/versao` deve responder `versao=0.5.2` e o mesmo Build ID.

## Logs esperados

Somente dentro de `logs\`:

```text
logs\BUILD.txt
logs\PUBLICACAO-ULTIMA.log
logs\DIAGNOSTICO-PUBLICACAO-ULTIMO.txt   (quando houver diagnóstico)
logs\Publicador-Launcher.log
logs\Publicacao-Clara-v0.5.2-*.log
logs\DIAGNOSTICO-PUBLICACAO-v0.5.2-*.txt (quando houver diagnóstico)
```

Não deve existir `artifacts\logs`. Não devem existir logs/diagnósticos de publicação soltos na raiz.

## Cenário que motivou a correção

O teste da v0.5.1 comprovou que o push podia terminar corretamente e o SHA remoto acabar igual ao `HEAD`, enquanto a confirmação imediata ainda falhava. A v0.5.2 deve continuar para a etapa Render nesse caso, em vez de gerar falso erro de Git.

## Banco

Esperado: 6 migrations. Não criar nem editar migration nesta correção.
