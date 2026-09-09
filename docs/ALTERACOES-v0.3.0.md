# Share IT .Clara v0.3.0 - Alterações

## Multiempresa e acesso

- usuário inicial do bootstrap promovido para `Master`;
- nova área Master para criação de clientes;
- cada cliente novo recebe tenant próprio e empresa principal;
- criação do primeiro Administrador pelo Master;
- Administrador cria usuários do próprio tenant;
- perfil Padrão atribuído inicialmente aos usuários comuns;
- matriz de módulos por perfil;
- claims de módulos carregadas no login;
- rotas e navegação respeitam as permissões do perfil.

## Demonstração

- script `database/demo/CRIAR-EMPRESA-DEMONSTRACAO.sql`;
- botão Master para criar/atualizar a demonstração;
- 15 estações fictícias e 28 dias de telemetria aproximada;
- aplicativos, sites, reuniões, e-mail, locais e integrações fictícias;
- login `demonstracao@clara.local` / `demonstracao`.

## Banco

Nova migration: `0004_multiempresa_perfis_acesso.sql`.
Migrations anteriores não foram alteradas.
