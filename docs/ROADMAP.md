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
