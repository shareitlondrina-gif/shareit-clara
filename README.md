# Share IT .Clara v0.3.2

Baseline funcional do **Share IT .Clara**, plataforma multiempresa de inteligência do trabalho e monitoramento autorizado de performance em estações Windows.

## Princípios de produto

- produto independente do Share IT .VISÃO e de qualquer Monitor legado;
- foco exclusivo em jornada digital observada, atividade/ociosidade, contexto de aplicativos, localização de trabalho e indicadores;
- sem Service Desk, projetos, CMDB, RMM técnico ou suporte remoto;
- sem keylogger, captura de senha ou conteúdo digitado;
- sem bloqueio de sites;
- evidências visuais não fazem parte desta entrega e continuam sujeitas a política/RBAC/auditoria nas etapas futuras.

## Correção de compilação

A v0.3.2 preserva as funcionalidades da v0.3.1 e adiciona a correção do fluxo de autenticação observado após o deploy. A Web/API continua respondendo ao liveness antes da inicialização do banco, enquanto o login agora trata explicitamente banco em atualização/falha e sincroniza a senha do Master de bootstrap com o ambiente.


## Correção de autenticação da v0.3.2

A v0.3.2 corrige o cenário observado em produção no qual a página de login era exibida, mas o POST de autenticação retornava HTTP 500 após o envio da senha.

Principais ajustes:
- o login verifica o estado de inicialização do banco antes de consultar tabelas novas;
- falhas de banco/schema durante a autenticação deixam de gerar uma página HTTP 500 genérica e passam a retornar uma mensagem controlada na própria tela de login;
- o administrador Master de bootstrap tem sua senha sincronizada com `CLARA_ADMIN_SENHA` em cada inicialização bem-sucedida, evitando hash antigo/incompatível após mudanças de ambiente;
- foi incluída uma rota de erro real (`/erro`) para o middleware de exceções;
- nenhuma migration publicada foi alterada; permanecem `0001` a `0004`.

## Correção de publicação da v0.3.1

- `/api/health/live` fica disponível assim que a Web/API inicia, sem depender do PostgreSQL;
- o Render passa a usar `/api/health/live` como `healthCheckPath`;
- migrations e bootstrap executam depois que o servidor Web já está respondendo;
- falha de banco não derruba a Web de diagnóstico: `/api/health` retorna 503 e os detalhes ficam nos logs;
- o `PUBLICAR-CLARA.cmd` valida primeiro liveness e depois readiness;
- timeouts do publicador são menores e controlados por uma janela total de tempo;
- o ZIP local é gerado antes da validação online, portanto é preservado mesmo se o Render não confirmar o deploy.

Não há nova migration nesta versão. O esperado continua sendo **4 migrations**.

## Novidades da v0.3.0

### Identidade e experiência

- identidade oficial **Share IT .Clara** aplicada ao sistema;
- nuvem azul da família Share IT como logo e favicon;
- fundo creme/off-white e hierarquia visual revisada;
- navegação lateral preparada para expansão do produto;
- tela **Agora** redesenhada para leitura executiva.

### Onboarding automático da estação

O administrador escolhe somente a empresa e gera um token temporário. Após a instalação, o agente coleta e envia automaticamente, quando disponível:

- hostname;
- usuário Windows detectado;
- número de série;
- UUID do equipamento;
- fabricante e modelo;
- sistema operacional e arquitetura;
- memória total;
- capacidade total de discos locais;
- IP local e IP público observado;
- versão do agente;
- primeira e última comunicação.

O administrador complementa depois somente informações administrativas, como patrimônio, responsável, unidade e departamento.

### Monitoramento e contexto

- heartbeat e online/offline;
- Ativo, Ocioso, Bloqueado e Reunião;
- processo/aplicativo em primeiro plano;
- timeline por estação;
- painel de aplicativos por período;
- classificação configurável de aplicativos como Produtivo, Neutro ou Atenção;
- categorias de atividade por tenant;
- cadastro de faixas IP/CIDR para contexto **Home x Escritório**;
- tela de gestão das estações e inventário descoberto pelo agente.

### Inteligência inicial

A v0.3.0 inclui um **Índice Geral preliminar e explicável**, separado da confiança dos dados. O índice usa apenas sinais disponíveis e mostra os pesos aplicados. Ele não deve ser interpretado como julgamento automático sobre pessoas.

### Integrações corporativas

O banco e a interface já possuem fundação para:

- Microsoft 365 / Outlook / Teams;
- Google Workspace / Gmail / Meet;
- Zoom.

Também existem estruturas de dados para eventos de agenda, participações em reuniões e métricas agregadas de e-mail. **A sincronização externa real não é ativada automaticamente nesta versão**, pois depende das credenciais de aplicativo e do consentimento administrativo de cada tenant. Segredos dessas integrações não devem ser gravados nas tabelas de configuração pública.

### Atualização automática do agente

Permanece requisito estrutural e funcional:

1. agente consulta manifesto;
2. identifica versão mais nova;
3. baixa pacote autenticado;
4. valida SHA-256;
5. inicia atualizador fora da pasta instalada;
6. faz backup local;
7. substitui arquivos;
8. reinicia serviço/sessão;
9. restaura a versão anterior em caso de falha;
10. reporta o resultado ao servidor.

## Arquitetura

```text
Windows
  ├─ ShareIT.Clara.Agente     Windows Service
  │    ├─ heartbeat
  │    ├─ fila offline
  │    ├─ inventário
  │    ├─ sincronização
  │    └─ atualização automática
  │
  └─ ShareIT.Clara.Sessao     sessão interativa
       ├─ atividade/ociosidade
       ├─ bloqueio
       ├─ app em primeiro plano
       └─ detecção inicial de apps de reunião
              │
              └─ Named Pipe
                    ↓
             ShareIT.Clara.Agente
                    ↓ HTTPS/JSON
             ShareIT.Clara.Web/API
                    ↓
               PostgreSQL
              shareit_clara
```

## Banco

Banco lógico oficial:

```text
shareit_clara
```

Em homologação ele pode compartilhar a mesma **instância física PostgreSQL** usada pelo .VISÃO, mas o banco lógico é totalmente separado. O preparador valida o destino antes de aplicar migrations.

Migrations desta baseline:

```text
0001_base_operacional.sql
0002_indices_dashboard.sql
0003_contexto_inventario_integracoes.sql
```

Migrations publicadas são imutáveis.

## Render / GitHub homologados

```text
GitHub:
https://github.com/shareitlondrina-gif/shareit-clara.git

Render Web:
https://shareit-clara.onrender.com

Health:
https://shareit-clara.onrender.com/api/health

Versão:
https://shareit-clara.onrender.com/api/versao
```

O serviço Web deve permanecer na mesma região do PostgreSQL durante a homologação.

## Variáveis do Render

A conexão do banco fica no ambiente do Render, e não em XML versionado:

```text
CLARA_POSTGRES
CLARA_ADMIN_EMAIL
CLARA_ADMIN_SENHA
CLARA_EXECUTAR_MIGRACOES=true
CLARA_TENANT_NOME=Share IT
CLARA_TENANT_IDENTIFICADOR=shareit
CLARA_URL_PUBLICA=https://shareit-clara.onrender.com
```

`CLARA_POSTGRES` pode apontar para a conexão interna da instância física compartilhada. O código troca/valida o destino para `shareit_clara` antes das migrations.

## Caminhos locais oficiais

Fonte/release/publicação:

```text
C:\SysRelease\Clara
```

Configuração persistente local:

```text
C:\ConfigSys\Clara
```

Instalação nas estações:

```text
C:\Program Files\Share IT\Clara
```

Dados persistentes do agente:

```text
C:\ProgramData\ShareIT\Clara
```

## Publicação

Entrada única:

```text
PUBLICAR-CLARA.cmd
```

O publicador executa restore, build, testes, publish dos componentes Windows, pacote de atualização do agente, SHA-256, validação de segredos, Git push, aguarda Render, valida health e versão online, gera ZIP final e abre o navegador quando a publicação estiver saudável.

Configuração persistente do publicador:

```text
C:\ConfigSys\Clara\Publicacao\ConfiguracaoPublicacao.xml
```

Repositório esperado:

```text
https://github.com/shareitlondrina-gif/shareit-clara.git
```

## Instalação do agente

No painel **Agora**:

1. selecionar a empresa;
2. clicar em **Gerar instalação**;
3. copiar o token temporário;
4. na estação, executar como Administrador:

```text
INSTALAR-AGENTE.cmd TOKEN https://shareit-clara.onrender.com
```

A estação é criada/identificada automaticamente. Depois, em **Estações**, completar patrimônio, responsável, unidade e departamento.

## Segurança de escopo

Não implementar no .Clara:

- keylogger;
- conteúdo digitado;
- captura de senha;
- controle remoto de mouse/teclado;
- suporte remoto;
- Service Desk;
- gestão de projetos;
- CMDB/RMM técnico;
- bloqueio de sites;
- julgamento automático de desempenho humano.

## Próximos marcos

- ampliar contexto de sites/navegadores;
- ativar autorização e sincronização Microsoft/Google/Zoom após disponibilização das credenciais de aplicativo;
- evoluir agenda, reuniões e métricas de e-mail;
- aprofundar Índice Geral, confiança, foco, fragmentação, comunicação, capacidade, custos, oportunidades e adoção de IA;
- depois: screenshots, tela ao vivo somente leitura, gravação e armazenamento externo com governança.

Consulte também `docs/ALTERACOES-v0.3.0.md`, `docs/VALIDACAO-v0.3.0.md` e `docs/ROADMAP.md`.

## v0.3.0 - Multiempresa, Master e demonstração

A v0.3.0 consolida a operação SaaS multiempresa:

- o primeiro usuário configurado no bootstrap (`CLARA_ADMIN_EMAIL`) passa a ser **Master**;
- somente o Master cria novos clientes/empresas;
- cada nova empresa recebe um **tenant isolado**, empresa principal, perfis padrão, categorias e integrações próprias;
- o Master informa o primeiro administrador da empresa no momento do cadastro;
- o Administrador da empresa cria os demais usuários que terão acesso à plataforma;
- novos usuários recebem inicialmente o perfil **Padrão**;
- cada perfil possui matriz de módulos permitidos; alterações passam a valer no próximo login;
- acesso direto às rotas também é validado por módulo, não apenas ocultado no menu.

### Área Master

```text
/master/empresas
```

Permite cadastrar empresa, CNPJ/documento e o primeiro administrador.

### Administração do cliente

```text
/administracao/usuarios
/administracao/perfis
```

O administrador cria usuários e ajusta os módulos do perfil Padrão. O perfil Administrador é de sistema e mantém acesso administrativo completo.

### Empresa demonstração

O pacote inclui o script idempotente:

```text
database/demo/CRIAR-EMPRESA-DEMONSTRACAO.sql
```

O Master também possui o botão **Criar / atualizar demonstração**, que executa esse script no banco `shareit_clara`.

Login demonstrativo:

```text
E-mail: demonstracao@clara.local
Senha:  demonstracao
```

A demonstração gera dados totalmente fictícios para 15 estações, incluindo atividade/ociosidade, aplicativos, sites, Home x Escritório, reuniões, integrações e métricas agregadas de e-mail. A tela Agora também apresenta distribuição visual dos estados e barras de uso dos principais aplicativos. Nunca utilizar a conta demonstração como conta real de produção.

### Migration

```text
0004_multiempresa_perfis_acesso.sql
```

As migrations 0001, 0002 e 0003 permanecem imutáveis.
