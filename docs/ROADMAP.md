# Roadmap consolidado — Share IT .Clara

O projeto prefere marcos maiores, acumulando alterações relacionadas para evitar microversões.

## v0.1.2 — Baseline operacional — HOMOLOGADA

- C#/ASP.NET Core/.NET 10;
- PostgreSQL `shareit_clara`;
- login/multiempresa inicial;
- Windows Service + processo de sessão;
- token de instalação;
- heartbeat e atividade básica;
- atualização automática com hash/backup/rollback;
- GitHub → Render → health.

## v0.3.2 — Base concreta anterior — CONCLUÍDA

- onboarding/inventário automático;
- empresas/tenants/perfis/Master;
- aplicativos/classificação;
- Home x Escritório;
- fundação Microsoft/Google/Zoom;
- Índice Geral preliminar;
- demo inicial;
- correções de publicação/Render.

## v0.4.0 — Demonstração gerencial e inteligência — BASE CONSOLIDADA

- layout alinhado aos mockups aprovados;
- Agora, Estações, detalhe, Aplicativos, Home x Escritório e Índice;
- Oportunidades/Evidências persistentes;
- Configurações por tenant;
- drill-down por janela flutuante;
- demo 15 estações;
- política sincronizada com o agente;
- Data Protection persistida no PostgreSQL/Neon;
- migration 0005;
- screenshots desativados.

## v0.5.0 — Distribuição, contexto web e SaaS — CANDIDATA À HOMOLOGAÇÃO

- extensão Chrome/Edge + Native Messaging Host;
- coleta autorizada **somente de domínio**, sem conteúdo/teclas/senhas;
- fila offline e API de contexto web;
- validação da política também no servidor;
- tela Contexto web com classificação e drill-down;
- Índice Geral com componente web e cobertura/confiança;
- portal de instalação do agente com download, QR e token;
- instalador e pacote de atualização incluindo NavegadorHost/extensão;
- autorregistro do host após atualização automática;
- plano/status/limite/retensão/storage/vencimento por tenant;
- bloqueio de novos cadastros conforme assinatura;
- demo atualizada para 0006 e v0.5.0;
- exportação CSV de Oportunidades/Evidências;
- migration 0006.

## v0.5.2 — Robustez do publicador e centralização de logs — CANDIDATA À HOMOLOGAÇÃO

- preserva integralmente a v0.5.1;
- corrige falso negativo após `git push` bem-sucedido;
- adiciona retentativas na confirmação `ls-remote`;
- não classifica indisponibilidade transitória do GitHub como falha do push;
- mantém divergência real de SHA como erro bloqueante;
- trata HTTP 503 do Render como estado transitório durante a janela de deploy;
- centraliza logs, diagnósticos e marcador de build em `logs/`;
- mantém o gate final de versão, Build ID, banco e migrations;
- sem migration nova.

## v0.5.1 — Publicação rastreável e diagnóstico Render — CANDIDATA À HOMOLOGAÇÃO

- preserva todas as funcionalidades da v0.5.0;
- log completo em `artifacts/logs` e cópia `PUBLICACAO-ULTIMA.log` na raiz;
- diagnóstico automático `DIAGNOSTICO-PUBLICACAO-ULTIMO.txt`;
- confirmação SHA local x branch remota após push;
- Build ID único por execução publicado nos endpoints técnicos;
- gate de homologação exige versão e Build ID exatos;
- Deploy Hook opcional como fallback quando configurado;
- nenhuma migration nova; `0001` a `0006` permanecem imutáveis.

## Próximo marco — Integrações corporativas reais

Dependências externas impedem homologação puramente por código:

- Microsoft 365 / Outlook / Teams via OAuth/consentimento;
- Google Workspace / Gmail / Meet via OAuth/consentimento;
- Zoom via OAuth/consentimento;
- comparação reunião prevista x realizada;
- métricas agregadas de e-mail sem corpo da mensagem;
- estratégia de distribuição gerenciada da extensão Chrome/Edge.

## Marco analítico posterior

Pendências recuperadas do planejamento original que continuam válidas e não devem ser esquecidas:

- contexto/classificação por função, cargo, departamento ou perfil configurável pelo cliente;
- benchmark por equipe/função somente quando estatisticamente adequado;
- confiança e explicação ainda mais detalhadas;
- foco, continuidade, fragmentação e interrupções com critérios transparentes;
- carga de comunicação e capacidade recuperável, somente a partir de sinais agregados e explicáveis;
- custos/cenários e utilização de software quando houver dados de licenciamento/custo confiáveis;
- identificação de processos repetitivos e oportunidades de automação, sem capturar conteúdo sensível;
- indicadores de adoção de IA somente quando a origem dos dados estiver explicitamente integrada/autorizada;
- detecção de fricção digital e possível necessidade de treinamento baseada em sinais transparentes;
- nenhuma decisão automática sobre desempenho humano.

## Evidências visuais — somente após governança

Somente depois de RBAC, auditoria, política, retenção, armazenamento protegido e revisão jurídica/organizacional:

- screenshots configuráveis;
- tela ao vivo somente leitura;
- gravação de tela configurável;
- storage externo conforme arquitetura aprovada.

Áudio/câmera permanecem desabilitados por padrão e fora de ativação automática.

## Fora do escopo permanente

- keylogger;
- senhas/conteúdo digitado;
- captura de formulários;
- controle/suporte remoto;
- Service Desk;
- projetos;
- CMDB/RMM técnico;
- bloqueio de sites;
- julgamento automático de pessoas.

## v0.5.3 — Deploy Render resiliente — CANDIDATA À HOMOLOGAÇÃO

- Deploy Hook persistente e secreto fora do repositório.
- Fallback automático quando o Auto Deploy não assume o novo commit.
- Configurador local protegido para o hook.
- Diagnóstico de publicação mais objetivo e menor repetição de mensagens.
- Sem alterações de schema: 6 migrations.

## v0.5.4 — Correção do build Docker no Render — CANDIDATA À HOMOLOGAÇÃO

- Corrigir caminhos relativos dos scripts do instalador após `cd /out/pacote` no Dockerfile.
- Usar `/src/Ferramentas/...` para cópias executadas no build Linux do Render.
- Validar arquivos obrigatórios antes do build local e dentro do Docker.
- Remover warning `NomeCategoria` não utilizada.
- Sem alterações de banco/migrations.


## v0.5.6 - Demonstração gerencial e evidências visuais

- auto-reparo da empresa Demonstração;
- oportunidades/evidências/pontos de atenção populados;
- Insight principal com base demonstrativa diferenciada por local;
- “No que trabalhou hoje” no detalhe da estação;
- migration 0007 para metadados de evidências visuais;
- galeria de capturas fictícias no demo;
- captura real continua pendente de RBAC, auditoria, retenção/storage e governança.


## v0.5.7 — Jornada, histórico, alertas e governança — CANDIDATA À HOMOLOGAÇÃO

- tema Claro/Escuro/Sistema por usuário;
- jornada de referência e período efetivo de monitoramento;
- sobrescrita de jornada por estação;
- Relatórios e Histórico;
- alertas parametrizados e Central de Notificações;
- fila de e-mail com remetente padrão da Clara;
- perfil Supervisor;
- Governança e Privacidade com documentos, SHA-256, ciência e aprovação jurídica;
- migration 0008;
- recursos restritos continuam tecnicamente bloqueados até homologação específica.


## v0.5.9-RC1 — fechamento do núcleo

- preserva a v0.5.8-RC2 e suas 9 migrations;
- adiciona validação local separada antes da publicação;
- adiciona diagnóstico de coexistência .Clara + .VISÃO;
- formaliza o gate para v0.6.0-RC e v1.0.0;
- não libera evidências restritas nem conectores OAuth externos.
