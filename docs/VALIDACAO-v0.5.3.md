# Validação — Share IT .Clara v0.5.3

## Preparação única

Se o Auto Deploy do Render não estiver efetivamente ligado ao serviço, execute:

`Ferramentas\Configuracao\CONFIGURAR-DEPLOY-HOOK.cmd`

O arquivo esperado depois da configuração é:

`C:\ConfigSys\Clara\SegredosClara.local.xml`

Ele deve possuir `RenderDeployHook` preenchido, sem versionar ou compartilhar esse valor.

## Homologação

Execute somente `PUBLICAR-CLARA.cmd` e confirme:

1. restore/build/test concluídos;
2. SHA local/remoto do GitHub confirmado;
3. publicador informa que o Deploy Hook persistente está disponível, sem mostrar a URL;
4. se o Auto Deploy não assumir em aproximadamente 90 segundos, aparece uma única ação de fallback do Deploy Hook;
5. `/api/health/live` responde `versao=0.5.3` e o Build ID da execução;
6. `/api/health` responde banco `shareit_clara`, status `ok` e 6 migrations;
7. `/api/versao` devolve a mesma versão/Build ID;
8. a publicação encerra com Render `ONLINE E VALIDADO`.

## Logs

Todos os arquivos permanecem em `logs\`:

- `PUBLICACAO-ULTIMA.log`
- `DIAGNOSTICO-PUBLICACAO-ULTIMO.txt` quando necessário
- `BUILD.txt`
- `Publicacao-Clara-v0.5.3-AAAAMMDD-HHMMSS.log`
- `DIAGNOSTICO-PUBLICACAO-v0.5.3-AAAAMMDD-HHMMSS.txt` quando necessário
- `Publicador-Launcher.log`

Nenhum log deve conter a URL do Deploy Hook.
