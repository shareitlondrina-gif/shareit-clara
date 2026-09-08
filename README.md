# Share IT .Clara v0.2.2

Baseline funcional do **Share IT .Clara**, plataforma multiempresa de inteligência do trabalho e monitoramento autorizado de performance em estações Windows.

## Princípios de produto

- produto independente do Share IT .VISÃO e de qualquer Monitor legado;
- foco exclusivo em jornada digital observada, atividade/ociosidade, contexto de aplicativos, localização de trabalho e indicadores;
- sem Service Desk, projetos, CMDB, RMM técnico ou suporte remoto;
- sem keylogger, captura de senha ou conteúdo digitado;
- sem bloqueio de sites;
- evidências visuais não fazem parte desta entrega e continuam sujeitas a política/RBAC/auditoria nas etapas futuras.

## Correção de compilação

A v0.2.2 mantém o escopo funcional da v0.2.1 e corrige o fluxo Git do publicador para sincronizar com o histórico remoto antes do push, sem usar `--force`. Nenhuma migration foi modificada.

## Novidades da v0.2.2

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

A v0.2.2 inclui um **Índice Geral preliminar e explicável**, separado da confiança dos dados. O índice usa apenas sinais disponíveis e mostra os pesos aplicados. Ele não deve ser interpretado como julgamento automático sobre pessoas.

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

Consulte também `docs/ALTERACOES-v0.2.2.md`, `docs/VALIDACAO-v0.2.2.md` e `docs/ROADMAP.md`.
