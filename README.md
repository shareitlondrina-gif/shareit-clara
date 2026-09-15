# Share IT .Clara v0.5.8-RC — fechamento operacional e gestão executiva

A v0.5.8-RC é a candidata de fechamento técnico/comercial antes da baseline 1.0. Ela mantém a .Clara independente de qualquer outro produto Share IT e acrescenta recursos de gestão, histórico, retenção, privacidade e autorrecuperação do agente.

## Destaques

- **Pulso da Operação**: visão executiva com produtividade observada, disponibilidade, ociosidade, não produtividade, atividade produtiva fora da jornada, prioridades e evidências.
- **Capacidade operacional**: leitura factual de subutilização, equilíbrio e sobrecarga em relação à carga diária de referência.
- **Padrões repetitivos**: sinaliza rotinas digitais recorrentes candidatas a simplificação/automação, sem concluir automaticamente que o processo deve ser automatizado.
- **Utilização de licenças**: quantidade contratada, custo informado pelo cliente, usuários observados e potencial de revisão.
- **Adoção de IA**: identifica ferramenta/domínio e duração de uso de ChatGPT, Copilot, Gemini, Claude e Cursor, sem ler prompts ou conteúdo.
- **Relatórios e histórico**: Hoje / 7 / 30 / 90 dias e período personalizado, filtros salvos e relatórios recorrentes por e-mail.
- **Alertas de jornada**: início tardio, saída antecipada, intervalo longo, ausência de atividade, carga abaixo/acima da referência, além de ociosidade, não produtividade, atividade produtiva fora da jornada e estação offline.
- **Retenção automática**: expurgo diário conforme a política do tenant, com auditoria da execução.
- **Exclusões de privacidade**: aplicativos/domínios que não devem compor o contexto de monitoramento, globalmente ou por departamento.
- **Timezone no agente**: o período de monitoramento é calculado no fuso configurado pela empresa/estação, não apenas no fuso do Windows.
- **Agente reforçado**: watchdog da sessão, validação pós-atualização, marcador local de saúde, rollback e limpeza de backups/artefatos antigos.
- **Coexistência com .VISÃO**: a .Clara posterga atualização quando detecta atualização recente do .VISÃO e usa uma trava global preparada para coordenação entre produtos Share IT.
- **Integração opcional com .VISÃO**: somente aparece quando o cliente também contratou o produto/serviço de TI e a integração foi explicitamente ativada. Clientes somente .Clara não veem nem dependem dessa integração.

## Regra comercial da integração .VISÃO

```text
Cliente somente .Clara
→ .Clara funciona integralmente sozinha
→ Contexto TI não aparece

Cliente .Clara + .VISÃO/serviço de TI contratado
→ Master marca o produto como contratado
→ integração técnica pode ser ativada
→ Contexto TI aparece somente para esse tenant
→ Pulso da Operação pode correlacionar fatos operacionais com contexto técnico recebido
```

A integração não transfere ao cliente funcionalidades de suporte de TI que ele não contratou.

## Jornada x período de monitoramento

- **Jornada de referência**: usada para leitura gerencial e alertas.
- **Período efetivo de monitoramento**: janela em que a sessão/navegação pode coletar telemetria autorizada.
- Fora do período efetivo, a atividade de sessão e o contexto web não são persistidos. Heartbeat/inventário técnico continuam independentes.

## Recursos restritos

A governança criada na v0.5.7 permanece válida. Screenshots reais, live view, gravação de tela, áudio e webcam continuam tecnicamente bloqueados nesta candidata; autorização documental por si só não ativa esses recursos.

## E-mail

Remetente padrão previsto:

`Share IT .Clara <monitoramento.clara@shareitbrasil.com.br>`

O transporte depende das credenciais SMTP configuradas por segredo de ambiente. O cliente pode definir Reply-To e destinatários adicionais.

## Banco

- Banco lógico protegido: `shareit_clara`.
- Migrations esperadas na v0.5.8-RC: **9**.
- Migrations `0001` a `0008`: preservadas byte a byte em relação à v0.5.7.
- Nova migration: `0009_fechamento_operacional_coexistencia_gestao.sql`.

## Publicação

1. Extraia a release em uma pasta nova.
2. Preserve `C:\ConfigSys\Clara`.
3. Execute `PUBLICAR-CLARA.cmd`.
4. Aguarde restore/build/testes, GitHub, Render e validação final.
5. A publicação só deve ser homologada quando `/api/versao` e `/api/health` confirmarem versão `0.5.8`, Build ID da execução, banco `shareit_clara` e **9 migrations**.

## Gate para v1.0

A v0.5.8-RC deve ser homologada em Windows com .NET SDK 10, incluindo teste físico de coexistência .Clara + .VISÃO, atualização automática/rollback, SMTP, retenção, timezone e relatórios agendados. Após esse gate, a baseline pode evoluir para v1.0.
