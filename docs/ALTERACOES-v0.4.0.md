# Alterações — Clara v0.4.0

## Consolidação

Esta versão foi construída sobre a baseline concreta v0.3.2 e concentra o maior conjunto coerente de alterações em uma única entrega, evitando fracionamento artificial.

## Banco

Nova migration `0005_inteligencia_configuracoes_demonstracao.sql`:

- `configuracoes_monitoramento` por tenant;
- `oportunidades`;
- `evidencias_contextuais`;
- `chaves_protecao_dados`;
- módulos Oportunidades, Evidências e Configurações;
- permissões dos perfis Administrador/Padrão.

Migrations `0001`–`0004` não foram modificadas.

## Demonstração

`database/demo/CRIAR-EMPRESA-DEMONSTRACAO.sql` foi ampliado para recriar de forma idempotente o cenário fictício da Horizonte Contabilidade, incluindo 15 estações, locais, histórico, aplicativos, sites fictícios, reuniões, e-mail, oportunidades, evidências e configurações.

`ServicoRelogioDemonstracao` mantém apenas o tenant fixo de demonstração visualmente atual durante apresentações.

## Web/UX

Redesenho das telas:

- Agora;
- Estações;
- detalhe da estação;
- Aplicativos;
- Home x Escritório;
- Índice Geral;
- Oportunidades;
- Evidências;
- Configurações.

Indicadores e gráficos gerenciais usam drill-down por modal através de `/indicadores/detalhes`.

Nenhuma informação técnica anterior foi removida: cadastro e inventário técnico foram reposicionados em seções recolhíveis quando a tela gerencial passou a ter prioridade.

## Inteligência

Novo `RepositorioInteligencia` centraliza:

- oportunidades;
- evidências;
- comparativo por local;
- configurações do tenant;
- contexto gerencial da estação;
- drill-down de estado, aplicativo, local, oportunidade, evidência e componentes do índice.

## Agente e Sessão

- API `GET /api/v1/agentes/politica`;
- agente sincroniza política periodicamente e grava configuração local;
- Sessão relê política local e respeita dias/horários monitorados;
- heartbeat passa a seguir o intervalo do tenant;
- parâmetros web/evidência ficam preparados sem ativar coleta inexistente.

## Segurança/infraestrutura

- key ring do ASP.NET Core Data Protection persistido no PostgreSQL/Neon;
- Master continua separado das telas operacionais de tenant;
- captura de screenshots forçada como desativada nesta baseline;
- sem keylogger, senha, texto digitado, bloqueio de site, controle remoto ou julgamento automático de pessoas.

## Integrações ainda não ativadas

Microsoft, Google e Zoom continuam estruturados, porém dependem de credenciais/OAuth/consentimento. Coleta real de domínio/URL de navegador também permanece pendente de conector autorizado.
