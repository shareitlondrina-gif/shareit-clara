# Alterações — Clara v0.5.7

## Objetivo

Transformar a Clara de uma leitura predominantemente online em uma plataforma com histórico, regras operacionais, alertas e governança documentada, mantendo a premissa de que o sistema apresenta fatos e o cliente realiza a interpretação gerencial.

## Interface e preferências

- Tema Claro atualizado para off-white/creme/cinza muito leve, preservando a paleta azul da Clara.
- Tema Escuro em grafite azulado.
- Opção Claro / Escuro / Sistema salva por usuário em **Meu perfil**.

## Jornada e monitoramento

- Jornada de referência global: início/fim aproximados, carga diária esperada, dias e margens antes/depois.
- Período efetivo de monitoramento separado da jornada.
- Configuração específica por estação, com possibilidade de voltar a herdar a regra do tenant.
- Fora do período efetivo, o agente não agrega/persiste telemetria da sessão nem contexto web; heartbeat e inventário técnico continuam independentes.
- Relatórios distinguem produtivo dentro da jornada, ociosidade, não produtivo e produtivo fora da jornada/margens.

## Histórico e relatórios

- Novo módulo **Relatórios e histórico**.
- Períodos rápidos: Hoje, 7, 30 e 90 dias.
- Resumo por estação/responsável e atalhos para Aplicativos, Navegação, Evidências, Oportunidades e Home x Escritório.
- Leitura factual para apoiar investigação, sem concluir automaticamente intenção, infração ou hora extra.

## Alertas e notificações

- Regras por empresa/todos, departamento/equipe ou estação.
- Tipos iniciais: uso não produtivo, ociosidade, produtivo fora da jornada e estação offline.
- Limite em minutos, severidade, canal plataforma/e-mail, perfis/e-mails destinatários e geração opcional de evidência/oportunidade.
- Central de Notificações funcional no sino, com contagem, histórico, marcação de leitura e link de destino.
- Serviço em background avalia regras mesmo sem gestor conectado.

## E-mail

- Fila persistente de e-mails com retentativas.
- Remetente padrão `Clara <no-reply@clara.local>`.
- Reply-To e destinatários adicionais por tenant.
- Credenciais SMTP somente via segredo de ambiente.
- `render.yaml` inclui chaves esperadas de configuração sem valores secretos.

## Acesso

- Perfil de sistema **Supervisor** criado por tenant.
- Supervisor recebe módulos de leitura operacional/inteligência, sem Administração.
- Administração de Usuários permite criar e alterar usuários entre Padrão e Supervisor.

## Governança e Privacidade

- Área **Governança e Privacidade**.
- Upload de documentos com conteúdo, metadados e SHA-256.
- Tipos: política, autorização jurídica, RIPD, comprovante de ciência e outros.
- Solicitação de aprovação jurídica por e-mail/link com token e expiração.
- Registro de decisão, aprovador, cargo, validade e autorização por recurso.
- Registro de ciência do colaborador vinculado à política.
- Recursos restritos: Screenshot, Visualização ao Vivo, Gravação de Tela, Áudio e Webcam.
- Autorizações vencidas deixam de aparecer como vigentes.
- Nesta release, aprovação documental **não habilita captura técnica** dos recursos restritos.

## Documentos-base

Incluídos modelos técnicos/contratuais em `docs/Juridico/` para integração ao fluxo jurídico oficial da Clara e adaptação pelo cliente.

## Banco

- Migration nova: `0008_governanca_jornada_alertas_relatorios_tema.sql`.
- Migrations 0001–0007 preservadas byte a byte.
- Total: 8 migrations.
