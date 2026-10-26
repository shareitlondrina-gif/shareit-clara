# Clara v0.6.0-RC1-R5

Programador: Paulo Andrade

## Correção
- confirma identidade visual somente Clara no layout ativo;
- elimina possibilidade de logo horizontal legado no pacote ativo;
- adiciona `RELEASE.txt` com revisão exata;
- `/api/versao`, `/api/health/live` e `/api/health` passam a informar `revisao`;
- publicador exige versão, revisão exata, Build ID, banco lógico e 12 migrations antes de declarar sucesso;
- imagens de marca recebem versionamento de URL para evitar cache visual antigo;
- migration 0012 permanece responsável por normalizar o tenant/empresa interna para `Clara`.

As migrations 0001-0011 permanecem imutáveis. Nenhuma migration nova foi necessária nesta revisão.
