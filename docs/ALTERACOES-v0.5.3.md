# Alterações — Share IT .Clara v0.5.3

## Motivo

A homologação da v0.5.2 confirmou que o problema anterior de Git foi resolvido: o commit local/remoto foi confirmado. Entretanto, o Render continuou respondendo v0.3.2 enquanto o publicador aguardava v0.5.2, evidenciando que depender somente do Auto Deploy não é suficiente neste ambiente.

## Publicador

- Deploy Hook persistente em `C:\ConfigSys\Clara\SegredosClara.local.xml`.
- A variável `CLARA_RENDER_DEPLOY_HOOK` permanece como override temporário e tem precedência sobre o XML.
- O valor do hook nunca aparece em console, log ou diagnóstico.
- Fallback automático reduzido para 90 segundos no XML padrão.
- Tentativa via POST; em HTTP 405 há segunda tentativa via GET.
- Sem hook configurado, o publicador informa exatamente como configurar e continua aguardando o Auto Deploy até o timeout.
- Diagnóstico informa apenas se existe hook, sua origem e se foi acionado, nunca a URL.
- Repetição visual da espera foi reduzida para facilitar leitura da publicação.
- Na primeira execução sem hook, o publicador oferece configuração assistida sem obrigar o usuário; a opção pode ser desativada por `SolicitarDeployHookSeAusente=false`.

## Configuração assistida

Novo utilitário:

`Ferramentas\Configuracao\CONFIGURAR-DEPLOY-HOOK.cmd`

Ele solicita a URL por entrada protegida e atualiza somente o elemento `RenderDeployHook` do arquivo local de segredos.

## Banco

Nenhuma migration foi adicionada. As 6 migrations da v0.5.2 permanecem como baseline desta versão.
