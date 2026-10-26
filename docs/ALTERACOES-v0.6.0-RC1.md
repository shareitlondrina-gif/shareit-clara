# Alterações — Clara v0.6.0-RC1

## Fundação de dados
- Migration 0010 sem alteração das migrations históricas 0001–0009.
- Entidade canônica `dispositivos`, com backfill de computadores existentes.
- Catálogo `clara_infra_bancos` e `clara_provisionamentos` para transição Management → Administradora → Cliente.

## Cadastro
- Master cadastra Administradoras.
- Administradora cadastra Empresas.
- Todo dispositivo exige código administrativo de registro, uso único, 15 minutos.
- Código diferencia Windows/Android.

## Manutenção
- Código por máquina/operação para Parar, Reparar e Desinstalar.
- Endpoint autenticado do agente consome o código uma única vez.
- Scripts oficiais `MANUTENCAO-CLARA.cmd` e `Manutencao-Clara.ps1` incluídos no instalador.

## Evidências / armazenamento
- Binário fora do PostgreSQL.
- R2/S3 compatível implementado com bucket privado.
- Local disponível apenas para homologação/VPS persistente.
- SharePoint, Google Drive e HostGator/SFTP catalogados para conectores posteriores.
- Credenciais cifradas por AES-256-GCM usando chave-raiz somente no backend.
- Upload de screenshot autenticado preparado em `/api/v1/agentes/evidencias/imagem` e leitura protegida em `/evidencias/arquivo/{id}`.

## RC1-R2 — correção do gate de publicação
- `VALIDAR-ANTES-DE-PUBLICAR.cmd` também disponível na raiz como launcher.
- `PUBLICAR-CLARA.cmd` executa obrigatoriamente o gate de validação antes de iniciar a publicação.
- se restore/build/test ou qualquer validação falhar, a publicação é interrompida.
