# Share IT .Clara v0.5.2 — candidata

Baseline consolidada do **Share IT .Clara**, plataforma multiempresa de monitoramento autorizado e inteligência de contexto do trabalho em estações Windows.

A v0.5.2 preserva integralmente a v0.5.1 e corrige o falso negativo observado após um `git push` bem-sucedido. A confirmação do SHA remoto agora possui tentativas, tolera indisponibilidade transitória do GitHub e, quando o push já retornou sucesso mas `ls-remote` está temporariamente indisponível, a publicação segue para o gate online do Render em vez de encerrar incorretamente. Todos os arquivos de log/diagnóstico da publicação ficam concentrados na pasta `logs`.

> Estado desta pasta: **código-fonte candidato para validação**. Este ambiente não possui o `.NET SDK 10`; portanto restore/build/test/publish não foram executados aqui. A homologação deve ser concluída no Windows oficial com `PUBLICAR-CLARA.cmd`.

## Correções principais da v0.5.2

Na homologação da v0.5.1 o `git push` foi concluído e `origin/main` terminou no mesmo SHA do `HEAD`, porém a consulta imediata `git ls-remote` falhou de forma transitória e o publicador encerrou antes de chegar ao ciclo normal de espera do Render. A v0.5.2 corrige esse comportamento.

- confirmação do SHA remoto com até 6 tentativas e intervalo controlado;
- diferença real e persistente entre SHA local/remoto continua sendo erro bloqueante;
- indisponibilidade transitória de `ls-remote` após `git push` com exit code 0 vira aviso e a confirmação é deferida ao gate online;
- HTTP 503 do Render durante a janela de publicação é tratado como estado transitório de reinício/inicialização, sem falso diagnóstico imediato;
- o gate final continua exigindo `versao + buildId + banco + migrations`;
- todos os logs/diagnósticos ficam em `logs\`;
- `logs\BUILD.txt` é o marcador de build da fonte e é publicado como `BUILD.txt` dentro do Web/API;
- `logs\PUBLICACAO-ULTIMA.log`, `logs\DIAGNOSTICO-PUBLICACAO-ULTIMO.txt`, logs com timestamp e `logs\Publicador-Launcher.log` não ficam espalhados pela raiz nem em `artifacts\logs`;
- arquivos locais de log/diagnóstico são ignorados pelo Git; somente `logs/BUILD.txt` é versionado para produzir um novo build identificável.

Não há migration nova nesta versão. As migrations `0001` a `0006` permanecem imutáveis.

## Princípios preservados

- produto independente do Share IT .VISÃO e de qualquer Monitor legado;
- foco em jornada digital observada, atividade/ociosidade, contexto de aplicativos, localização de trabalho e indicadores explicáveis;
- nenhuma remoção deliberada das funcionalidades da baseline anterior;
- sem keylogger, senha, conteúdo digitado, formulários ou conteúdo de páginas;
- sem bloqueio de sites e sem controle remoto;
- fatos observáveis separados de interpretações/recomendações;
- indicadores apoiam gestão e não substituem avaliação humana contextual;
- tenant Master da plataforma não navega nos dados operacionais dos clientes.

## Base visual e inteligência preservadas da v0.4.0

Continuam as telas revisadas conforme os mockups aprovados:

- **Agora**: Online, Ativos, Reunião, Ociosos, Offline, Cadastro pendente, distribuição das estações, comparativo Home x Escritório, últimas estações, aplicativos, oportunidades e evidências;
- **Estações** e **Detalhe da estação**: estado, atividade, local, patrimônio, responsável, timeline, oportunidades/evidências e detalhes técnicos;
- **Aplicativos**: uso, participação, classificação e categoria;
- **Home x Escritório**: Escritório, Home office, Externo/Remoto e Sem classificação;
- **Índice Geral**: score, confiança, composição e explicação dos pesos;
- **Oportunidades** e **Evidências**: listas persistentes, filtros e status;
- **Configurações**: políticas próprias de cada tenant.

Cards, barras e indicadores gerenciais continuam clicáveis e abrem **janela flutuante com a lista de estações/itens que geraram o número** por meio de `/indicadores/detalhes`.

## Contexto web real — Chrome e Edge

A v0.5.0 adiciona um conector real com arquitetura de privacidade mínima:

```text
Chrome/Edge (extensão MV3)
       │
       │ somente hostname/domínio ativo + navegador
       ▼
Native Messaging Host
ShareIT.Clara.NavegadorHost
       │ Named Pipe local
       ▼
ShareIT.Clara.Agente
       │ fila offline + política do tenant
       ▼
HTTPS /api/v1/agentes/navegacao
       │
       ▼
PostgreSQL / sites_utilizados
```

O conector **não envia** caminho da URL, query string, título da página, conteúdo, senha, formulário ou texto digitado.

A coleta só é persistida quando `Monitorar URLs/domínios` está habilitado no tenant. A regra é validada no agente **e novamente no servidor**, evitando que um cliente local alterado force a gravação quando a política estiver desativada.

Componentes:

```text
src/ShareIT.Clara.NavegadorExtensao
src/ShareIT.Clara.NavegadorHost
src/ShareIT.Clara.Agente/Telemetria/ServidorPipeNavegacao.cs
src/ShareIT.Clara.Agente/Telemetria/FilaNavegacao.cs
```

A extensão da baseline é adequada para **homologação/implantação corporativa controlada**. Distribuição automática em larga escala por política do Chrome/Edge ainda depende da estratégia de publicação da extensão (loja privada/gerenciada ou mecanismo corporativo equivalente).

## Tela Contexto web

A área de Aplicativos ganhou acesso a **Contexto web**, sem poluir o menu principal. A tela mostra:

- domínios observados no período;
- estações com contexto web;
- participação produtiva e de atenção;
- duração por domínio e número de estações;
- classificação `Produtivo`, `Neutro` ou `Atenção`;
- categoria livre configurável pelo tenant;
- regras persistidas;
- drill-down por domínio/classificação.

A configuração **Gerenciar sites e categorias** aponta para essa tela.

## Índice Geral com contexto web

O Índice Geral passa a considerar até quatro componentes, conforme disponibilidade real dos dados:

1. atividade observada;
2. continuidade;
3. contexto produtivo de aplicativos explicitamente classificados;
4. contexto produtivo web explicitamente classificado.

Os pesos são redistribuídos quando um componente ainda não possui cobertura suficiente. A confiança considera cobertura das estações e cobertura das classificações. O detalhamento de `Contexto produtivo web` abre a lista que compôs o indicador.

## Portal de instalação do agente

Administradores passam a ter **Administração → Instalação**, com:

- plano/status do tenant;
- estações consumidas x limite contratado;
- download do instalador publicado;
- QR Code para o endereço autenticado de download;
- geração de token temporário de uso único;
- URL do servidor e comando de instalação;
- orientação do conector web.

O publicador gera:

```text
Clara-Agent-v<versao>.zip
Clara-Instalador-v<versao>.zip
```

O instalador contém:

```text
Agente/
Sessao/
Atualizador/
NavegadorHost/
NavegadorExtensao/
INSTALAR-AGENTE.cmd
Instalar-Agente.ps1
DIAGNOSTICAR-AGENTE.cmd
Diagnosticar-Agente.ps1
```

O agente atualizado automaticamente também executa a rotina de registro do Native Messaging Host no próximo início, permitindo que estações existentes recebam o novo componente sem reinstalação completa do serviço.

## Controle comercial SaaS por tenant

A migration `0006_distribuicao_navegacao_saas.sql` adiciona `assinaturas_tenant` com:

- plano;
- status (`Ativa`, `Trial`, `Suspensa`, `Cancelada`);
- limite de estações;
- retenção em dias;
- armazenamento contratado;
- vencimento;
- observação administrativa.

O Master pode administrar essas informações sem acessar o conteúdo operacional do tenant.

Novos tokens/novas estações são bloqueados quando:

- assinatura não está `Ativa` ou `Trial`;
- assinatura está vencida;
- limite de estações foi atingido.

Heartbeats de estações já cadastradas não são descartados automaticamente por uma suspensão comercial, preservando diagnóstico e rastreabilidade da operação existente.

## Exportações analíticas

As telas **Oportunidades** e **Evidências** agora exportam CSV filtrado, em UTF-8 com BOM e separador `;`, adequado ao Excel em PT-BR. O conteúdo é neutralizado contra interpretação de fórmulas em células textuais.

## Empresa demonstração

Script idempotente:

```text
database/demo/CRIAR-EMPRESA-DEMONSTRACAO.sql
```

Requer migrations até `0006` e recria a fotografia fictícia em relação a `NOW()`.

Cenário de referência:

```text
Empresa: Horizonte Contabilidade
15 estações
12 online
7 ativas
2 em reunião
3 ociosas
3 offline
Plano: Demonstração / Trial / limite 15
Contexto web habilitado
```

Inclui telemetria fictícia, aplicativos, sites/domínios, regras de classificação, locais de trabalho, oportunidades, evidências e dados estruturais de agenda/reuniões/e-mail.

Login demonstrativo:

```text
E-mail: demonstracao@clara.local
Senha:  demonstracao
```

Nunca usar essa conta/tenant como produção.

## Banco

Banco lógico oficial:

```text
shareit_clara
```

Migrations:

```text
0001_base_operacional.sql
0002_indices_dashboard.sql
0003_contexto_inventario_integracoes.sql
0004_multiempresa_perfis_acesso.sql
0005_inteligencia_configuracoes_demonstracao.sql
0006_distribuicao_navegacao_saas.sql
```

As migrations **0001 a 0006 permanecem byte a byte imutáveis nesta v0.5.1**. A `0006` foi introduzida pela v0.5.0; a v0.5.1 não altera o schema.

## Capturas de tela / tela ao vivo / gravação

Continuam **desativadas** nesta baseline. A UI pode manter a estrutura de configuração, mas o POST do servidor força `CapturaScreenshots=false`.

Ativação real exige antes:

- RBAC explícito;
- auditoria de acesso;
- política e ciência organizacional;
- retenção e descarte;
- armazenamento protegido;
- revisão jurídica/organizacional.

## Integrações externas ainda pendentes

A base estrutural de Microsoft 365, Google Workspace e Zoom permanece, mas a sincronização real exige credenciais de aplicativo, consentimento administrativo e homologação dos provedores. A v0.5.0 **não simula OAuth real**.

## Infraestrutura

Padrão:

```text
GitHub → versionamento
Render → Web/API
Neon → PostgreSQL
```

Data Protection permanece persistida no PostgreSQL/Neon, sem depender do filesystem efêmero do Render.

Caminhos Windows:

```text
Fonte/release:          C:\SysRelease\Clara
Configuração:           C:\ConfigSys\Clara
Instalação da estação:  C:\Program Files\Share IT\Clara
Dados do agente:        C:\ProgramData\ShareIT\Clara
```

## Publicação

Entrada única:

```text
PUBLICAR-CLARA.cmd
```

O publicador deve executar restore/build/test, publicar Web, Agente, Sessão, Atualizador e NavegadorHost, copiar a extensão, gerar pacotes/SHA-256, publicar o instalador no Web, validar segredos, sincronizar Git sem force push e confirmar Render/health/migrations.

Gate esperado para esta versão:

```text
/api/versao      -> 0.5.1 + buildId exato desta execução
/api/health      -> 0.5.1 + mesmo buildId + shareit_clara / banco ok / 6 migrations
/api/health/live -> 0.5.1 + mesmo buildId + aplicação ok
```

## Pendências após a v0.5.1

- build/test/publish e homologação física desta candidata no Windows oficial;
- se o Render continuar servindo build antigo, usar o diagnóstico gerado para revisar o vínculo real do Web Service com o repositório/branch ou o deploy que falhou;
- estratégia corporativa de distribuição automática da extensão Chrome/Edge;
- conectores Microsoft/Google/Zoom reais com OAuth e consentimento;
- aprofundamento por função/perfil do Índice Geral sem julgamento automático;
- screenshots/tela ao vivo/gravação somente após governança completa.

Veja `docs/ALTERACOES-v0.5.1.md`, `docs/VALIDACAO-v0.5.1.md`, os documentos históricos da v0.5.0 e `docs/ROADMAP.md`.
