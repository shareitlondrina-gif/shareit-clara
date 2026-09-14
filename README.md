# Share IT .Clara v0.5.4 — correção Render/Docker

A v0.5.4 preserva todas as funcionalidades da v0.5.3 e corrige o erro real identificado no build Docker do Render: após `cd /out/pacote`, o Dockerfile tentava copiar scripts de `Ferramentas` usando caminho relativo, fazendo o build procurar os arquivos no diretório errado. A versão também remove o warning `NomeCategoria` não utilizada e adiciona validações preventivas dos arquivos exigidos pelo instalador.

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

## Correção principal da v0.5.4

O Deploy Log da v0.5.3 mostrou que os projetos .NET eram publicados corretamente, mas o Docker falhava ao copiar `Ferramentas/Instalacao/INSTALAR-AGENTE.cmd` e `Instalar-Agente.ps1`. O comando era executado depois de `cd /out/pacote`, então o caminho relativo apontava para o local errado. A v0.5.4 usa caminhos absolutos `/src/Ferramentas/...` e valida esses arquivos antes do empacotamento.
