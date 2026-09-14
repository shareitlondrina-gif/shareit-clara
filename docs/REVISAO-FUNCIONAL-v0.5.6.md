# Revisão funcional — Share IT .Clara v0.5.6

## Objetivo desta revisão

Consolidar a leitura funcional da .Clara com foco no que o cliente final mais quer enxergar:

1. **quem está trabalhando agora**;
2. **em que está trabalhando**;
3. **o que trabalhou hoje**;
4. **quais evidências comprovam isso**;
5. **quais oportunidades gerenciais podem ser acionadas**.

Esta revisão foi feita comparando as telas aprovadas de referência com as capturas mais recentes do ambiente de demonstração.

---

## Para que serve a tela de Integrações

As integrações com **Microsoft 365 / Outlook / Teams**, **Google Workspace / Gmail / Meet** e **Zoom** servem para **enriquecer o contexto** dos indicadores e evidências, principalmente em três frentes:

- **Agenda e presença**: entender quando a pessoa está em reunião, fora do computador ou em compromisso agendado;
- **Correlação de atividade**: diferenciar ocioso real de baixa interação local causada por reunião/call;
- **Métricas complementares**: agregar dados de reunião, participação e uso sem armazenar conteúdo sensível.

> **Importante:** a .Clara não depende das integrações para funcionar. O motor principal já pode gerar valor usando dados do agente local, heartbeat, aplicativos, navegação, timeline, status e localização (home x escritório). As integrações entram como **camada de contexto**.

---

## Matriz de indicadores e aderência visual

### 1) Tela **Agora**

**Indicadores esperados**

- Online
- Ativos
- Reunião
- Ociosos
- Offline
- Cadastro pendente
- Estado das estações
- Comparativo Home x Escritório
- Estações (últimas comunicações)
- Mais utilizados
- Pontos de atenção identificados
- Insights do ambiente

**Situação da revisão**

- A estrutura da tela está correta;
- Os cards operacionais principais estão previstos;
- Os blocos **Pontos de atenção identificados** e **Insights do ambiente** precisam estar sempre coerentes com as evidências geradas no período;
- Na demonstração, estes blocos não podem ficar zerados se o resto da base estiver simulando atividade real.

**Conclusão:** presente, porém **dependente de dados demo consistentes**.

---

### 2) Tela **Estações**

**Indicadores esperados**

- Todas
- Online
- Ativas
- Reunião
- Ociosas
- Offline
- Pendentes
- Tabela por estação com responsável, estado, atividade atual, local, patrimônio, última comunicação, versão e ações

**Situação da revisão**

- Aderência boa;
- Tela responde diretamente à pergunta “**quem está trabalhando agora?**”;
- Deve ser mantida como visão operacional principal do gestor.

**Conclusão:** aderente e prioritária.

---

### 3) Tela **Detalhe da estação**

**Indicadores esperados**

- Status atual
- Tempo produtivo hoje
- Tempo ocioso hoje
- Local predominante
- Aplicativo principal
- Índice de foco
- Pontos de atenção
- Timeline do dia
- Principais alertas de hoje
- Evidências
- Oportunidades

**Situação da revisão**

- Aderência muito boa ao objetivo do produto;
- Esta tela é a que mais suporta a visão “**o que a pessoa fez hoje**”;
- Recomenda-se fortalecer ainda mais os painéis/abas de **Evidências** e **Oportunidades** dentro do detalhe da estação;
- Onde houver prints capturados, eles devem ser acessíveis a partir daqui.

**Conclusão:** é uma das telas mais importantes da .Clara.

---

### 4) Tela **Home x Escritório**

**Indicadores esperados**

- Escritório
- Home office
- Externo/Remoto
- Sem classificação
- Gráfico de produtividade, foco e ociosidade
- Insight principal
- Redes conhecidas
- Estações recentes com local identificado automaticamente
- Principais insights e recomendações por local

**Situação da revisão**

- Estrutura correta;
- O gráfico comparativo está previsto;
- O **Insight principal** precisa ser sempre narrativo e coerente com os números;
- O módulo **Oportunidades por local** deve aparecer populado em demonstração sempre que houver diferença relevante entre os ambientes.

**Conclusão:** aderente, mas pede **narrativa gerencial mais forte e base demo coerente**.

---

### 5) Tela **Oportunidades**

**Indicadores esperados**

- Ociosidade alta
- Sites não produtivos
- Baixa continuidade
- Possível necessidade de treinamento
- Baixo uso de sistema principal
- Tabela de oportunidades com pessoa, estação, categoria, evidência resumida, impacto, recomendação e status

**Situação da revisão**

- Estrutura existe e é adequada;
- Em demonstração, não pode permanecer zerada quando existem fatos simulados na base;
- A regra de ouro é: **evidência gera oportunidade acionável**.

**Conclusão:** presente, porém **precisa ser populada e coerente com Agora/Evidências**.

---

### 6) Tela **Evidências**

**Indicadores esperados**

- Produtividade observada
- Períodos ociosos
- Acessos não produtivos
- Uso do sistema principal
- Reuniões
- Linha do tempo de evidências
- Filtros por período, pessoa, categoria e gravidade
- Exportação

**Situação da revisão**

- Estrutura prevista corretamente;
- A tela deve sustentar a proposta de “**gestão com evidência**”;
- Tudo que alimentar Oportunidades precisa nascer aqui como fato observável.

**Conclusão:** tela-chave para o valor da solução.

---

### 7) Tela **Integrações**

**Situação da revisão**

- Tela correta como base estrutural;
- Deve deixar claro que a base funcional já existe;
- Os conectores reais dependem de credenciais, consentimento e homologação dos provedores.

**Conclusão:** útil como visão de evolução e governança.

---

## Indicadores estratégicos que precisam ficar sempre visíveis

A revisão consolidou os seguintes grupos de informação como **obrigatórios**:

### Operacional imediato
- Quem está online
- Quem está ativo
- Quem está em reunião
- Quem está ocioso
- Aplicativo atual
- Local atual/predominante

### Jornada do dia
- O que fez hoje
- Timeline do dia
- Tempo produtivo
- Tempo ocioso
- Aplicativo principal
- Reuniões
- Trocas de aplicativo

### Prova / comprovação
- Evidência por horário
- Evidência por pessoa/estação
- Evidência de site/aplicativo
- Evidência de ociosidade
- Evidência de baixa continuidade
- **Prints de tela**, quando o recurso estiver habilitado pela política do cliente

### Gestão / ação
- Oportunidades
- Índice geral
- Índices por ambiente
- Insight principal
- Pontos de atenção identificados
- Insights do ambiente

---

## Regra de negócio recomendada: Evidência x Oportunidade

### Evidência
É um **fato observado**.

Exemplos:
- “Bruno acessou YouTube por 27 min”;
- “João ficou 1h42 ocioso em horário comercial”;
- “Carla alternou 18 vezes de aplicativo em 1 hora”.

### Oportunidade
É a **interpretação acionável** desse fato.

Exemplos:
- “Reforçar política de uso e orientar foco nas atividades”;
- “Conversar sobre ajuste de rotina e redistribuição de atividades”;
- “Treinamento prático no ERP e acompanhamento inicial”.

> **Regra sugerida:** toda oportunidade deve ter pelo menos uma evidência associada.

---

## Gaps priorizados para a próxima evolução funcional

### Prioridade alta
1. Popular corretamente a empresa Demonstração em **Agora**, **Oportunidades**, **Evidências** e **Home x Escritório**;
2. Garantir coerência entre telas (o mesmo fato deve refletir em mais de um módulo);
3. Fortalecer a tela de Evidências com drill-down e acesso a prints;
4. Fortalecer o detalhe da estação como visão operacional do dia;
5. Tornar o bloco **Insight principal** mais narrativo e explicativo.

### Prioridade média
6. Tela/aba dedicada para screenshots, com filtro por período/pessoa/estação;
7. Resumo executivo por Hoje / 7 dias / 30 dias;
8. Explanação clara do papel das Integrações;
9. Manual comercial e manual operacional do administrador.

---

## Conclusão da revisão

A .Clara está com a **arquitetura de telas correta** e cobre os pilares certos do produto. O principal ajuste agora não é “inventar novas telas”, mas **reforçar o valor prático**:

- mostrar **quem está trabalhando**;
- mostrar **no que está trabalhando**;
- mostrar **o que trabalhou hoje**;
- exibir **evidências concretas**;
- transformar fatos em **oportunidades gerenciais**.

Esta revisão acompanha o manual ilustrado desta versão.


---

## Implementação consolidada na v0.5.6

A v0.5.6 transforma os pontos da revisão em uma entrega demonstrável:

- a empresa **Horizonte Contabilidade - Demonstração** passa a se auto-reparar quando a carga gerencial estiver vazia/desatualizada;
- **Pontos de atenção**, **Oportunidades**, **Evidências** e **Insight principal** passam a receber uma carga fictícia mais rica e coerente;
- o detalhe da estação ganhou o bloco **No que trabalhou hoje**, consolidando os aplicativos observados na timeline;
- Evidências e detalhe da estação ganharam a galeria **Evidências visuais recentes**;
- foi criada a migration `0007_evidencias_visuais_demonstracao.sql`;
- foram incluídas seis capturas 100% fictícias para apresentação da experiência de screenshots;
- a captura real permanece desabilitada por padrão até homologação de privacidade, RBAC, auditoria, retenção e armazenamento.
