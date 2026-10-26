# Modelo-base — Política de Monitoramento, Produtividade e Privacidade no Ambiente de Trabalho

> **Clara — modelo técnico-base.** Este documento deve ser adaptado às condições reais da empresa contratante e aprovado pelo seu Jurídico/Encarregado antes de ser adotado como política interna. A Clara fornece a descrição técnica e os controles de governança; a empresa contratante define finalidade, escopo e fundamento jurídico do tratamento.

## 1. Finalidade

A presente Política disciplina o uso da plataforma Clara para produção de indicadores de atividade, produtividade, continuidade, ociosidade, contexto de uso de aplicativos/sites, disponibilidade de estações e demais evidências autorizadas, com o objetivo de apoiar gestão operacional, dimensionamento de capacidade, treinamento, segurança operacional e melhoria de processos.

A plataforma registra **fatos e contextos observáveis**. A avaliação gerencial, trabalhista ou disciplinar desses fatos pertence exclusivamente à empresa contratante.

## 2. Escopo

A empresa deve indicar previamente:

- pessoas, equipes e estações abrangidas;
- jornada de referência aplicável;
- período efetivo de monitoramento;
- classificação de aplicativos e sites como produtivos, neutros ou não produtivos;
- destinatários de alertas e relatórios;
- recursos adicionais de evidência autorizados;
- prazos de retenção aplicáveis.

A decisão sobre os equipamentos em que o agente será instalado, inclusive natureza corporativa ou não do dispositivo, é de responsabilidade da empresa contratante, que deve possuir as autorizações e fundamentos necessários para a instalação e o tratamento correspondente.

## 3. Jornada de referência e período de monitoramento

A **jornada de referência** é um parâmetro analítico, podendo conter horário aproximado de início e fim, carga horária diária esperada, dias aplicáveis e margens antes/depois. Ela não constitui, por si só, sistema de registro eletrônico de ponto.

O **período de monitoramento** define a janela em que a telemetria configurada pode ser coletada. Fora dessa janela, o agente de sessão deve deixar de coletar a telemetria de atividade configurada.

A plataforma pode evidenciar, sem julgamento automático:

- atividade produtiva durante a jornada;
- ociosidade durante a jornada;
- uso classificado pelo cliente como não produtivo durante a jornada;
- atividade produtiva antes/depois da jornada e das margens configuradas;
- padrões históricos e comparativos.

## 4. Dados e sinais tratados

Conforme configuração e recursos habilitados, a Clara pode tratar:

- identificação da estação e usuário associado;
- horários de comunicação e disponibilidade;
- períodos de atividade/ociosidade/reunião;
- processo/aplicativo em uso e duração;
- domínio/categoria web quando o conector autorizado estiver ativo;
- localização contextual por rede/IP (ex.: escritório, home office, externo);
- evidências contextuais, alertas, oportunidades e histórico;
- screenshots e outros recursos visuais somente quando formalmente autorizados e habilitados pela governança.

A configuração padrão não tem como objetivo registrar senhas, conteúdo digitado, conteúdo de mensagens/e-mails ou conteúdo integral de documentos.

## 5. Classificação de produtividade

A empresa contratante é responsável por configurar a classificação de aplicativos e sites. A Clara utiliza essas regras para apresentar indicadores, sem presumir automaticamente que determinado uso seja pessoal, inadequado ou justificável.

O sistema deve priorizar a apresentação de **aplicativo/site + categoria + horário + duração + classificação configurada**.

## 6. Alertas e notificações

A empresa pode estabelecer regras como:

- ociosidade acima de determinado limite;
- uso não produtivo acima de determinado limite;
- atividade produtiva fora da jornada/margem;
- estação sem comunicação;
- baixa continuidade ou outros sinais homologados.

Os alertas podem ser entregues na plataforma e/ou por e-mail para perfis e destinatários definidos pela empresa. O disparo de um alerta representa a ocorrência de um parâmetro configurado e não uma conclusão disciplinar.

## 7. Recursos de maior invasividade

Screenshots, visualização ao vivo, gravação de tela, áudio e webcam são tratados como recursos de governança reforçada.

- ficam desabilitados por padrão;
- não podem ser liberados unilateralmente por Desenvolvedor, Master ou Administrador;
- dependem de documentação, finalidade, escopo, retenção, autorização jurídica registrada e ciência/política aplicável;
- possuem validade e bloqueio automático quando a autorização expira;
- qualquer acesso futuro a tais recursos deve ser auditável.

Áudio e webcam devem ser tratados como recursos excepcionalíssimos. A existência de autorização administrativa ou jurídica registrada na plataforma não significa habilitação técnica automática nem substitui a análise de necessidade/proporcionalidade do caso concreto.

## 8. Visualização ao vivo

Quando tecnicamente disponibilizada e formalmente autorizada, a visualização ao vivo deve ser somente leitura, sem controle remoto. Ela pode operar sem popup a cada acesso, mas sua possibilidade de uso deve estar previamente informada na política aplicável ao trabalhador. Não se prevê modo clandestino destinado a ocultar a existência do monitoramento do titular.

## 9. Retenção e acesso

A empresa deve definir prazos de retenção adequados à finalidade e restringir o acesso aos dados aos perfis necessários. Evidências de maior sensibilidade devem ter retenção específica e, quando aplicável, mecanismos adicionais de mascaramento, exclusão de áreas/aplicativos sensíveis e auditoria.

## 10. Transparência e ciência

A empresa deve disponibilizar aos trabalhadores informações claras sobre:

- finalidade e escopo do monitoramento;
- período monitorado;
- tipos de sinais/dados tratados;
- recursos visuais eventualmente autorizados;
- prazo de retenção;
- quem pode acessar;
- canais para exercício de direitos e contato com responsável/Encarregado.

A Clara pode registrar a ciência da política por e-mail, assinatura física/digital ou registro administrativo, conforme procedimento definido pela empresa.

## 11. Governança documental

A plataforma pode manter:

- política vigente e versões anteriores;
- anexo técnico da Clara;
- autorização jurídica por recurso;
- RIPD ou avaliação de impacto quando aplicável;
- comprovantes de ciência;
- hash SHA-256 dos documentos armazenados;
- solicitações, decisões e validade das autorizações.

A validação de integridade/assinatura eletrônica confirma aspectos técnicos da assinatura/arquivo quando implementada, mas não substitui a avaliação jurídica do conteúdo.

## 12. Revisão da política

A empresa deve revisar esta política sempre que houver mudança relevante de finalidade, escopo, tipos de dados, recursos de monitoramento, retenção ou grupos abrangidos. A Clara recomenda revisão periódica e renovação das autorizações de recursos restritos.

## 13. Ciência

Ao final do processo adotado pela empresa, deve existir evidência de que a política vigente foi disponibilizada ao trabalhador antes ou no início do monitoramento correspondente, respeitadas as orientações jurídicas da contratante.
