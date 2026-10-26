# Clara v0.3.0 — Alterações

## Correções da v0.3.0

- corrigido o contrato `OpcaoCadastro` para aceitar o vínculo opcional com `EmpresaId`, usado pelos seletores de unidade e departamento;
- corrigido o parâmetro de IP público no repositório de agentes (`ipPublico`), eliminando referência a identificador inexistente;
- publicador Git agora consulta e sincroniza `origin/main` antes de criar o commit da release;
- pastas de release sem `.git` passam a herdar o histórico remoto por `fetch` + `reset --mixed`, preservando os arquivos atuais e evitando `fetch first` / `non-fast-forward`;
- o fluxo não utiliza `git push --force`, preservando o histórico do repositório;
- nenhuma migration foi alterada; `0001`, `0002` e `0003` permanecem imutáveis.


## Produto e identidade

- nova identidade visual aplicada, baseada na família Clara;
- logo horizontal e ícone/favicons oficiais;
- fundo creme/off-white e navegação lateral;
- tela Agora reorganizada para leitura rápida.

## Agente e estação

- onboarding sem cadastro manual de hostname;
- token agora nasce vinculado à empresa, não a uma estação previamente cadastrada;
- criação/localização automática do ativo no primeiro registro;
- coleta automática de hostname, usuário, serial, UUID, fabricante, modelo, Windows, arquitetura, memória, disco e IP local;
- registro do IP público observado pelo servidor;
- cache de inventário para reduzir custo de coleta;
- correção do cálculo de ociosidade do Windows para preservar o wrap do contador nativo;
- estado Reunião inicial quando app de reunião conhecido está em primeiro plano.

## Administração

- tela Estações;
- inventário técnico somente leitura;
- complemento administrativo de patrimônio, responsável, empresa, unidade e departamento;
- status de cadastro completo/incompleto.

## Contexto e analytics

- painel de aplicativos;
- classificação Produtivo / Neutro / Atenção;
- categorias iniciais de atividade;
- regras de local de trabalho por faixa IP/CIDR;
- classificação visual Home x Escritório / Externo;
- Índice Geral preliminar, transparente e acompanhado de Confiança dos Dados.

## Integrações

- fundação persistente para Microsoft, Google e Zoom;
- tabelas para agenda, participações em reunião e métricas agregadas de e-mail;
- painel de status das integrações;
- sincronização externa real ainda depende de credenciais e consentimento administrativo do tenant.

## Banco

Nova migration imutável:

```text
0003_contexto_inventario_integracoes.sql
```

As migrations `0001` e `0002` permanecem preservadas.

## Infraestrutura

- Docker runtime instala `libgssapi-krb5-2` para eliminar a dependência ausente observada no Render;
- forwarded headers configurados para execução atrás do proxy do Render;
- health continua validando explicitamente `clara`;
- atualização automática do agente permanece integrada ao fluxo de publicação.
