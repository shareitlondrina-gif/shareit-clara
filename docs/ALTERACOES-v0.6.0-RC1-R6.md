# Clara v0.6.0-RC1-R6

Programador: Paulo Andrade

## Correção do gate único de publicação

- Corrigida a ordem de criação do Build ID.
- `logs/BUILD.txt` agora é criado pelo gate local **antes** de `dotnet restore/build/test`.
- `Clara.Web` pode incorporar `BUILD.txt` durante a compilação sem erro MSB3030.
- O publicador não cria um segundo identificador: ele valida e reutiliza exatamente o Build ID que foi compilado.
- O mesmo ID percorre: validação local -> build -> pacote -> Git/Render -> `/api/versao` e `/api/health/live` -> gate online.
- `PUBLICAR-CLARA.cmd` continua sendo o único comando operacional.
- Se qualquer gate falhar, nenhuma publicação é considerada concluída e o navegador não é aberto como sucesso.

## Identidade

- Mantida identidade ativa somente `Clara`.
- Migrations históricas 0001-0011 permanecem imutáveis.
- Migration 0012 permanece a migration de normalização da identidade ativa.
