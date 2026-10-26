# Alterações — Clara v0.5.8-RC

## Objetivo
Fechar os principais gaps identificados na revisão final da Clara antes da baseline comercial 1.0, sem tornar o produto dependente do VISÃO.

## Gestão executiva
- Novo **Pulso da Operação**.
- Capacidade: Subutilizado / Equilibrado / Sobrecarregado.
- Padrões repetitivos candidatos a automação.
- Custos/licenças de software informados pelo cliente e leitura de possível subutilização.
- Adoção de IA por ferramenta/domínio e duração, sem leitura de conteúdo.
- Referências operacionais configuráveis.

## Clara + VISÃO
- Integração opcional por tenant.
- Flags separadas: produto/serviço contratado e integração técnica ativa.
- Token dedicado armazenado somente como SHA-256.
- Endpoint de entrada de contexto técnico com rate limiting.
- Menu e bloco de contexto TI invisíveis quando VISÃO não estiver contratado/ativo.
- A Clara permanece totalmente funcional sozinha.

## Histórico e relatórios
- Período personalizado de até 366 dias.
- Filtros salvos para períodos rápidos ou personalizados.
- Relatórios recorrentes Diário / Semanal / Mensal enviados pela fila de e-mail.
- Resumo executivo com Pulso da Operação e indicadores de jornada.

## Jornada e alertas
Novos tipos:
- Início tardio;
- Saída antecipada;
- Intervalo longo;
- Ausência de atividade;
- Carga abaixo da referência;
- Carga acima da referência.

Continuam existentes:
- Ociosidade;
- Uso não produtivo;
- Produtivo fora da jornada;
- Estação offline.

A Clara apresenta fatos; não conclui falta, fraude, hora extra autorizada ou infração trabalhista.

## Privacidade e retenção
- Retenção separada para telemetria, evidências, notificações, e-mails e auditoria.
- Serviço automático diário de expurgo com registro da execução.
- Exclusões de aplicativos/domínios por tenant/departamento.
- Exclusões são levadas ao agente: domínio excluído não é persistido; aplicativo excluído perde o contexto do nome antes da persistência, preservando apenas atividade/ociosidade.

## Agente Windows
- Timezone da política enviado ao agente.
- Watchdog da sessão interativa.
- Atualização automática com validação estrutural do pacote.
- Marcador local de saúde após iniciar a versão nova.
- Confirmação de serviço RUNNING antes de considerar sucesso.
- Rollback quando a versão nova não confirma saúde.
- Limpeza de backups e artefatos antigos.
- Mutex `Global\Clara.ProductUpdate` preparado para coordenação entre produtos Clara.
- Detecção conservadora de diretório de atualização recente do VISÃO para postergar atualização da Clara.

## E-mail / saúde
- `/api/health/live` informa se o host SMTP está configurado, sem revelar credenciais.

## Banco
- Nova migration `0009_fechamento_operacional_coexistencia_gestao.sql`.
- Total esperado: 9 migrations.
- 0001..0008 preservadas byte a byte.
