# Alterações — Share IT .Clara v0.5.0

## Objetivo da versão

Concentrar o máximo possível das pendências concretas da Clara após a revisão visual v0.4.0, sem perder telas, indicadores ou comportamentos já existentes.

## Entregas principais

### 1. Contexto web real e limitado a domínio

- novo `ShareIT.Clara.NavegadorHost` para Native Messaging;
- nova extensão Manifest V3 para Chrome/Edge;
- transmissão somente de domínio ativo, navegador, usuário Windows e intervalo agregado;
- Named Pipe `ShareIT.Clara.Navegador` entre sessão e Windows Service;
- fila offline própria no agente;
- endpoint `POST /api/v1/agentes/navegacao`;
- persistência em `sites_utilizados`;
- política `MonitorarUrlsDominios` validada localmente e também no servidor;
- sanitização/limites de domínio, usuário, navegador e timestamps.

Não são coletados caminho da URL, query string, título, conteúdo da página, senha, formulário ou texto digitado.

### 2. Contexto web no painel

- nova tela `/navegacao-web` acessível a partir de Aplicativos;
- KPIs de domínios, cobertura, contexto produtivo e atenção;
- classificação e categoria persistidas por domínio;
- gerenciamento das regras de sites;
- drill-down por domínio e classificação;
- link de Configurações para gerenciamento de sites/categorias.

### 3. Índice Geral ampliado

- composição dinâmica com atividade, continuidade, contexto de aplicativos e contexto web;
- redistribuição transparente de pesos quando faltam classificações;
- confiança com cobertura de estações e classificações;
- drill-down do componente web.

### 4. Portal de instalação

- novo módulo `instalacao-agente`;
- página `Administração > Instalação`;
- resumo do plano e consumo de estações;
- download autenticado do instalador publicado;
- QR Code do endereço de download;
- token temporário de uso único;
- comando pronto de instalação;
- compatibilidade da rota de token antiga redirecionada ao novo portal.

### 5. Pacote do agente ampliado

O `PUBLICAR-CLARA.cmd` / `Publicar-Clara.ps1` passa a publicar:

- Agente;
- Sessão;
- Atualizador;
- NavegadorHost;
- NavegadorExtensao;
- instalador completo.

O ZIP de atualização automática contém todos esses diretórios. O Windows Service registra novamente o Native Messaging Host após uma atualização bem-sucedida.

### 6. Controle SaaS

Migration `0006_distribuicao_navegacao_saas.sql`:

- `assinaturas_tenant`;
- plano/status;
- limite de estações;
- retenção;
- armazenamento;
- vencimento;
- observação;
- índices para consultas de navegação;
- módulo de instalação para administradores.

O Master pode alterar os parâmetros comerciais. Geração de token e criação efetiva de novo ativo validam status, vencimento e limite.

### 7. Empresa demonstração atualizada

`database/demo/CRIAR-EMPRESA-DEMONSTRACAO.sql` passa a:

- exigir migration 0006;
- identificar agentes como v0.5.0;
- criar/atualizar assinatura `Demonstração / Trial / 15`;
- manter contexto web habilitado;
- manter sites/regras e fotografia gerencial relativa a `NOW()`.

### 8. Exportações

- Oportunidades: CSV com filtros atuais;
- Evidências: CSV com filtros atuais;
- UTF-8 BOM + `;` para uso amigável no Excel PT-BR;
- neutralização de conteúdo textual que poderia ser interpretado como fórmula.

## Compatibilidade e preservação

- migrations 0001–0005 não foram alteradas;
- layout e indicadores da v0.4.0 são preservados;
- o processo de atualização automática continua com hash, backup e rollback;
- `Directory.Build.props` fixa a versão de assembly em 0.5.0 nesta baseline; o publicador também injeta `-p:Version=$Versao`;
- integração Microsoft/Google/Zoom continua estrutural até existirem credenciais/consentimento reais;
- screenshots continuam forçados como desabilitados pelo servidor.

## Estado

Candidato a homologação. O ambiente de preparação não possui `.NET SDK 10`, então ainda falta executar o gate oficial de restore/build/test/publish no Windows.
