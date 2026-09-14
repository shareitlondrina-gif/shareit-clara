# Alterações - Share IT .Clara v0.5.6

## Objetivo

Transformar a revisão funcional da v0.5.5 em uma versão demonstrável dentro do próprio sistema, com dados consistentes para **Pontos de atenção**, **Insight principal**, **Oportunidades**, **Evidências** e leitura da jornada individual.

## Empresa Demonstração

- o tenant `demonstracao` passa a se auto-reparar quando a carga estiver incompleta ou vier de uma versão anterior;
- a carga fictícia é recriada somente para o GUID fixo da empresa Demonstração;
- 15 estações ficam distribuídas em estados coerentes: 7 ativas, 2 em reunião, 3 ociosas e 3 offline;
- oportunidades foram ampliadas para alimentar os cinco cards principais;
- evidências textuais foram ampliadas com atividade atual, ociosidade, acesso não produtivo, reunião, uso do sistema, comportamento e desempenho;
- Home x Escritório passa a receber diferenças reais na base demonstrativa para que o **Insight principal** deixe de ficar neutro;
- o demo continua 100% fictício e isolado de tenants reais.

## Jornada do colaborador

O detalhe da estação ganhou o bloco **No que trabalhou hoje**, agrupando os aplicativos observados na timeline e mostrando o aplicativo atual.

## Evidências visuais

Nova migration `0007_evidencias_visuais_demonstracao.sql`:

- cria a tabela `evidencias_visuais`;
- armazena somente metadados e caminho do arquivo, não o binário da imagem no PostgreSQL;
- permite associar captura a tenant, estação e pessoa;
- inclui marcação `demonstrativa` para diferenciar conteúdo fictício.

Na empresa Demonstração:

- 6 capturas visuais fictícias são exibidas;
- as imagens são identificadas como **DEMONSTRAÇÃO**;
- Evidências e Detalhe da estação passam a mostrar galeria visual.

> A captura real pelo agente permanece desabilitada por padrão. Antes de habilitá-la em clientes reais, devem ser homologados RBAC, auditoria, retenção, armazenamento e política de privacidade.

## Interface

- galeria **Evidências visuais recentes** na tela Evidências;
- galeria **Capturas recentes** no detalhe da estação;
- bloco **Jornada de hoje / No que trabalhou hoje**;
- ajustes responsivos para as novas galerias;
- Configurações informa explicitamente que a estrutura visual está pronta, mas a captura real ainda está bloqueada por governança.

## Manual

- `docs/MANUAL-USABILIDADE-ILUSTRADO-v0.5.6.md`;
- `docs/MANUAL-USABILIDADE-ILUSTRADO-v0.5.6.pdf`;
- `docs/REVISAO-FUNCIONAL-v0.5.6.md`.

## Banco

- migrations `0001` a `0006` permanecem imutáveis;
- única migration nova: `0007_evidencias_visuais_demonstracao.sql`;
- total esperado após atualização: **7 migrations**.
