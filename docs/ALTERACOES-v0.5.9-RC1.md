# Alterações — Clara v0.5.9-RC1

## Objetivo

Consolidar o núcleo operacional/gerencial da Clara após a v0.5.8-RC2 e reduzir o risco de uma nova tentativa de publicação chegar a Git/Render com erro local não percebido.

## Alterações desta versão

- versão da aplicação passa para `0.5.9`;
- nenhuma migration nova; total permanece em 9;
- novo validador local `Ferramentas/Validacao/VALIDAR-ANTES-DE-PUBLICAR.cmd`;
- o validador confirma SDK .NET 10, arquivos obrigatórios, ausência de segredos locais, 9 migrations, restore, build e test;
- novo diagnóstico de coexistência Clara + VISÃO, somente leitura, registrando serviços, pastas e processos sem alterar a estação;
- publicador deixa explícito no log que o gate de build ocorre antes de Git/Render;
- correção da RC2 (`PossuiSobrescrita`) permanece incorporada;
- Pulso, retenção, alertas de jornada, relatórios agendados, filtros salvos, timezone, watchdog e integração opcional VISÃO permanecem preservados.

## Pendências deliberadamente fora desta candidata

- captura real de screenshots, live view e gravação de tela;
- áudio/webcam, sujeitos a caso excepcional e governança reforçada;
- OAuth real de Microsoft 365, Google Workspace e Zoom;
- assinatura criptográfica de pacote com chave privada de produção.

Esses itens não impedem a homologação do núcleo da Clara, mas impedem declarar que **todo o roadmap avançado** está concluído.
