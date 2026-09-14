# Share IT .Clara v0.5.3 — candidata

A v0.5.3 preserva todas as funcionalidades da v0.5.2 e fortalece o fluxo GitHub → Render depois da homologação que confirmou o push no GitHub, mas mostrou o serviço online ainda em v0.3.2 durante toda a janela de espera.

## Principais correções

- Deploy Hook do Render pode ser persistido em `C:\ConfigSys\Clara\SegredosClara.local.xml`, fora do código e do Git.
- `CLARA_RENDER_DEPLOY_HOOK` continua aceito como override temporário, mas não é mais necessário para uso normal.
- Helper seguro em `Ferramentas\Configuracao\CONFIGURAR-DEPLOY-HOOK.cmd` grava a URL sem exibi-la de volta na tela.
- Na primeira publicação sem hook, `PUBLICAR-CLARA.cmd` oferece configurá-lo no próprio fluxo; se o usuário recusar, o Auto Deploy continua sendo tentado normalmente.
- Fallback automático do Deploy Hook após 90 segundos, quando o Auto Deploy não troca a versão online.
- URL secreta nunca é escrita nos logs/diagnósticos.
- POST é usado primeiro; HTTP 405 tenta GET como compatibilidade.
- Mensagens repetitivas de espera do Render foram reduzidas: primeiras tentativas e depois resumos periódicos.
- Diagnóstico diferencia: Git confirmado, hook ausente, hook configurado/acionado e Render ainda em build antigo.
- Logs continuam centralizados exclusivamente em `logs\`.

## Publicação

1. Se ainda não houver Deploy Hook persistido, execute `Ferramentas\Configuracao\CONFIGURAR-DEPLOY-HOOK.cmd` uma única vez e cole a URL secreta obtida no Web Service `shareit-clara` do Render.
2. Execute `PUBLICAR-CLARA.cmd`.
3. O publicador compila/testa, envia ao GitHub, confirma o SHA, aguarda Auto Deploy e aciona o hook automaticamente se necessário.
4. A publicação só é aprovada quando versão, Build ID, banco `shareit_clara` e quantidade de migrations coincidirem.

> O pacote não contém a URL real do Deploy Hook nem outros segredos.
