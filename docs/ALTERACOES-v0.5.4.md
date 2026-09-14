# Alterações — Share IT .Clara v0.5.4

## Motivo

A homologação da v0.5.3 confirmou todo o fluxo até o Render: build local, GitHub, Auto Deploy e Deploy Hook. O deploy do Render falhou durante o build Docker com `cp: cannot stat` para os scripts do instalador.

A causa foi identificada no Dockerfile: após executar `cd /out/pacote`, o mesmo `RUN` continuava e tentava copiar `Ferramentas/Instalacao/...` e `Ferramentas/Diagnostico/...` com caminhos relativos. No Linux do Render, esses caminhos passavam a ser procurados a partir de `/out/pacote`, apesar de os arquivos existirem em `/src/Ferramentas`.

## Correções

- Dockerfile passa a copiar os scripts por caminhos absolutos `/src/Ferramentas/...`.
- Dockerfile valida antecipadamente a presença dos quatro scripts exigidos pelo instalador.
- Publicador Windows também valida os quatro arquivos antes de iniciar build/publicação, evitando enviar uma release incompleta ao GitHub.
- Removida a função local `NomeCategoria` não utilizada em `Views/Painel/Index.cshtml`, eliminando o warning CS8321 observado no build.
- Mantidos Deploy Hook persistente, fallback automático e centralização dos logs da v0.5.3.

## Banco

Nenhuma migration foi adicionada. As 6 migrations da v0.5.3 permanecem intactas.
