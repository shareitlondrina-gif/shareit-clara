# Share IT .Clara v0.6.0-RC1-R3 — fundação definitiva

Primeiro marco de consolidação rumo à 1.0. Esta release preserva as migrations 0001–0009, mantém a `0010_dispositivos_armazenamento_manutencao.sql` e adiciona a `0011_perfil_usuario_identidade_visual.sql`.

## Principais mudanças

- Master passa a representar a camada de plataforma e a UI trata os tenants como **Administradoras**.
- Administradora pode cadastrar suas próprias **Empresas administradas**.
- Registro de dispositivo exige **código administrativo de 9 dígitos**, uso único e validade de 15 minutos.
- Código identifica Windows ou Android; Windows já consome o código no fluxo atual. Android fica reservado para o enrolamento corporativo da v0.7.
- Nova entidade `dispositivos` prepara a visão unificada Windows/Android sem remover `ativos` nesta RC.
- Manutenção oficial (Parar/Reparar/Desinstalar) usa código temporário por máquina/operação e registra autorização.
- Evidências reais passam a ter metadados no PostgreSQL e binário fora do banco.
- Administração > Armazenamento permite configurar R2/S3 compatível ou Local; SharePoint, Google Drive e HostGator/SFTP ficam catalogados como conectores opcionais posteriores.
- Credenciais de armazenamento são cifradas AES-256-GCM; a chave-raiz permanece no backend em `CLARA_COFRE_MASTER_KEY_BASE64`.
- Fundação de catálogo `Management -> Administradora -> Cliente` adicionada para a transição ao padrão Neon multibanco do VISÃO.
- Perfil do usuário ampliado com foto, nome, cargo e telefone; e-mail de login permanece somente leitura.
- Menu do usuário no topo passa a oferecer **Perfil** e **Sair**.
- Paleta revisada por semântica: ativo/sucesso verde, trial/informação azul, atenção/suspensão âmbar, erro/cancelamento vermelho e neutro/inativo cinza.
- Tema escuro corrigido para não misturar cards brancos com títulos claros e para manter contraste de formulários, tabelas e rodapé.

## Segurança

Nunca grave `CLARA_COFRE_MASTER_KEY_BASE64`, Access Key, Secret Key, tokens ou connection strings no Git/ZIP. O bucket de evidências deve ser privado. A Clara entrega arquivos pela rota autenticada `/evidencias/arquivo/{id}`.

## Limite desta RC

A separação física de cada Administradora/Cliente em projeto/banco Neon individual foi **preparada no schema**, mas o roteamento operacional multibanco ainda não é ativado nesta RC para evitar uma migração destrutiva. O próximo passo da v0.6 é provisionar e migrar os bancos com validação de contagem/rollback antes de desligar `shareit_clara` compartilhado.


## Fluxo recomendado antes de publicar

Na raiz do pacote:

1. execute `VALIDAR-ANTES-DE-PUBLICAR.cmd`;
2. somente se todas as validações forem aprovadas, execute `PUBLICAR-CLARA.cmd`;
3. em caso de erro, não publique até corrigir o gate apontado.

O validador também permanece em `Ferramentas/Validacao/` para compatibilidade com os scripts existentes.
