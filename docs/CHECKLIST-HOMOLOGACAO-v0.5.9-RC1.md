# Checklist de homologação — Share IT .Clara v0.5.9-RC1

## Gate local
- [ ] `VALIDAR-ANTES-DE-PUBLICAR.cmd` concluiu sem erro.
- [ ] restore concluído.
- [ ] build Release concluído.
- [ ] test concluído.
- [ ] 9 migrations detectadas.

## Gate online
- [ ] `/api/versao` retorna 0.5.9.
- [ ] Build ID corresponde à execução atual.
- [ ] `/api/health` retorna `shareit_clara`.
- [ ] `/api/health` retorna 9 migrations.
- [ ] GitHub e Render apontam para o mesmo commit.

## Agente
- [ ] instalação nova.
- [ ] registro/token.
- [ ] heartbeat.
- [ ] atividade/ociosidade.
- [ ] contexto web dentro do período permitido.
- [ ] nada de contexto web fora do período de monitoramento.
- [ ] atualização automática.
- [ ] rollback testado.
- [ ] watchdog de sessão testado.

## Gestão
- [ ] Pulso da Operação.
- [ ] capacidade.
- [ ] custos/licenças.
- [ ] adoção de IA sem conteúdo.
- [ ] oportunidades de automação.
- [ ] filtros salvos.
- [ ] relatório agendado.
- [ ] alertas de jornada.
- [ ] retenção.

## Coexistência
- [ ] se houver .VISÃO, diagnóstico executado.
- [ ] ambos os agentes operam sem conflito de serviço/pasta.
- [ ] atualização de um produto não interrompe o outro.
- [ ] cliente sem .VISÃO não visualiza Contexto TI.
