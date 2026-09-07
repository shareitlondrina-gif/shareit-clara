# Share IT .Clara v0.1.2

**Produto novo**, dedicado exclusivamente à inteligência de performance de pessoas em estações Windows. Não reutiliza código, banco, migrations, entidades, Service Desk, projetos, CMDB, RMM ou suporte remoto do antigo Monitor.

## Entrega desta versão

- ASP.NET Core / C# / .NET 10 LTS;
- PostgreSQL lógico novo (`shareit_clara`), com criação automática inclusive quando a homologação compartilha a mesma instância PostgreSQL do .VISÃO;
- multiempresa desde a primeira migration;
- login administrativo por cookie;
- bootstrap inicial de tenant/empresa/administrador por ConfigSys + variáveis de ambiente;
- cadastro de ativo e geração de token temporário de instalação;
- agente Windows Service;
- processo interativo de sessão separado do serviço;
- registro seguro do agente com token de dispositivo protegido por DPAPI;
- heartbeat e online/offline;
- atividade/ociosidade/bloqueio em nível de presença;
- processo em primeiro plano sem capturar conteúdo digitado, senhas ou texto da tela;
- fila offline local;
- timeline das últimas 24 horas;
- dashboard “Agora”;
- atualização automática do agente com download autenticado, SHA-256, backup, atualizador externo e rollback;
- instalador do agente por CMD;
- diagnóstico do agente por CMD;
- `PUBLICAR-CLARA.cmd` como entrada única de compilação/publicação;
- proteção explícita que impede migrations do .Clara em `shareit_visao`, `monitor`, `postgres` ou qualquer banco diferente de `shareit_clara`.

## Estrutura

```text
ShareIT.Clara.sln
src/
  ShareIT.Clara.Dominio
  ShareIT.Clara.Comum
  ShareIT.Clara.Dados
  ShareIT.Clara.Web
  ShareIT.Clara.Agente
  ShareIT.Clara.Sessao
  ShareIT.Clara.Atualizador
database/migrations
Ferramentas/
ConfigSys/
docs/
```

## Configuração inicial do servidor

O publicador cria, se ainda não existirem:

```text
C:\ConfigSys\Clara\ConfiguracaoClara.xml
C:\ConfigSys\Clara\Publicacao\ConfiguracaoPublicacao.xml
```

Defina os segredos por variável de ambiente no servidor/publicação:

```text
CLARA_POSTGRES=<conexão PostgreSQL da instância>
CLARA_ADMIN_SENHA=<senha inicial forte>
CLARA_URL_PUBLICA=https://clara.shareitbrasil.com.br
```

### Render gratuito compartilhando a instância do .VISÃO

Na homologação, `CLARA_POSTGRES` pode receber diretamente a **Internal Database URL** atual do `shareit-visao-db`, mesmo que essa URL termine no banco `shareit_visao`.

O .Clara usa essa conexão somente como ponto administrativo da instância, executa automaticamente:

```text
verifica shareit_clara
→ se não existir: CREATE DATABASE shareit_clara
→ reconecta obrigatoriamente em shareit_clara
→ valida current_database()
→ aplica migrations apenas em shareit_clara
→ cria bootstrap inicial
```

Não é necessário liberar IP público, usar PSQL manualmente, restaurar backup, executar `DROP DATABASE` ou alterar tabelas do `.VISÃO`.

A senha inicial só é utilizada para criar o primeiro administrador se ainda não existir. Ela não é armazenada em texto puro no PostgreSQL.

## Publicação

Execute no Windows:

```text
PUBLICAR-CLARA.cmd
```

O script valida/instala o SDK .NET 10 via winget quando necessário, restaura pacotes, compila, executa a etapa de testes, publica Web/API e componentes Windows, gera o pacote automático do agente, calcula SHA-256, monta o manifesto de atualização e gera:

```text
artifacts\Clara-v0.1.2.zip
```

Git push e deploy hook podem ser ligados em `ConfiguracaoPublicacao.xml`. O segredo do deploy hook deve ser fornecido pela variável `CLARA_RENDER_DEPLOY_HOOK`.

Quando o Web/API inicia no Render com `CLARA_CRIAR_BANCO=true`, a preparação do banco acontece automaticamente pela rede interna antes das migrations. O `PUBLICAR-CLARA.cmd` dispara o fluxo de build/deploy e o health check confirma que o banco efetivamente aberto é `shareit_clara`.

Para GitHub/Render, o `Dockerfile` recompila Web/API e também cross-publica Agente/Sessão/Atualizador para `win-x64`, gera o ZIP do agente, calcula o SHA-256 e incorpora o manifesto/pacote na imagem do servidor. Assim a atualização automática continua funcionando mesmo quando o deploy é feito a partir do repositório.

## Instalação de um agente

No painel Web:

1. informe empresa, hostname, responsável e patrimônio;
2. gere o token temporário, válido por 30 minutos;
3. no pacote do agente execute como Administrador:

```text
INSTALAR-AGENTE.cmd <TOKEN> https://seu-clara.exemplo.com.br
```

O instalador cria:

```text
C:\Program Files\Share IT\Clara
C:\ProgramData\ShareIT\Clara
Serviço: ShareIT.Clara.Agente
Tarefa:  Share IT .Clara - Sessao
```

## Atualização automática

Fluxo implementado:

```text
Agente consulta manifesto autenticado
→ identifica versão superior
→ baixa pacote autenticado
→ valida SHA-256
→ copia atualizador para fora da instalação
→ atualizador para serviço e sessão
→ cria backup local
→ substitui arquivos
→ reinicia serviço/sessão
→ em falha restaura backup
→ nova execução reporta resultado ao servidor
```

O pacote de atualização **não é publicado como arquivo estático aberto**; o download passa por endpoint autenticado do agente.

## Proteção do banco

Esta baseline possui três barreiras:

1. o nome configurado precisa ser exatamente `shareit_clara`;
2. a fábrica de conexões sempre troca o database de destino para `shareit_clara`, mesmo quando `CLARA_POSTGRES` aponta inicialmente para `shareit_visao`;
3. antes de qualquer migration, o migrador executa `SELECT current_database()` e interrompe a aplicação se o resultado não for `shareit_clara`.

A aplicação não possui `DROP DATABASE` e não executa migrations no banco `shareit_visao`.

## Limites desta versão

- não existe Android;
- não existe keylogger;
- não existe captura de senha ou conteúdo digitado;
- não existe controle remoto;
- não existe suporte remoto;
- não existe bloqueio de sites;
- integrações Microsoft/Google/Zoom entram na v0.2.0;
- Índice Geral/analytics executivos entram na v0.3.0;
- screenshots/live/gravação/evidências entram na v1.0.0.

## Observação de compilação desta entrega

A estrutura, XML/JSON e balanceamento sintático dos arquivos C# foram validados no ambiente de geração. Esse ambiente não possui o SDK .NET instalado e não permite acesso direto ao NuGet, portanto a compilação real deve ser executada pelo `PUBLICAR-CLARA.cmd` em um Windows com internet/winget antes da homologação.


## Publicação online — v0.1.2

O endpoint oficial de saúde é `https://shareit-clara.onrender.com/api/health`.
Também existem `/api/health/live` (processo Web) e `/api/versao` (versão implantada).

O `PUBLICAR-CLARA.cmd` agora mantém a janela aberta, grava log em `artifacts\logs`, valida se a versão online é exatamente a de `VERSAO.txt` e abre o navegador quando a publicação é confirmada.

Para deploy automático, o Web Service do Render precisa estar conectado ao mesmo repositório GitHub/branch do projeto. O `render.yaml` utiliza `autoDeploy: true`.
