# Checklist de homologação — Clara v0.5.8-RC

## Publicação
- [ ] `PUBLICAR-CLARA.cmd` conclui sem erro.
- [ ] GitHub `main` confirma o commit publicado.
- [ ] Render publica o mesmo commit.
- [ ] `/api/versao` = 0.5.8 + Build ID correto.
- [ ] `/api/health/live` = aplicação OK.
- [ ] `/api/health` = banco `clara`, 9 migrations.

## Funcional
- [ ] Agora, Estações, Aplicativos, Navegação, Home x Escritório.
- [ ] Evidências e Oportunidades coerentes.
- [ ] Pulso da Operação abre e apresenta leitura explicável.
- [ ] Capacidade, padrões repetitivos, licenças e adoção de IA não quebram quando não há dados.
- [ ] Relatórios: Hoje/7/30/90 e período personalizado.
- [ ] Filtro salvo abre o mesmo período.
- [ ] Relatório agendado entra na fila e e-mail é entregue.
- [ ] Sino/deep-link funciona.

## Jornada / timezone
- [ ] Política do tenant chega ao agente com fuso horário.
- [ ] Notebook em fuso Windows diferente respeita o fuso configurado pela empresa/estação.
- [ ] Fora do período de monitoramento não há telemetria de sessão/navegação.
- [ ] Alertas de início tardio, saída antecipada, intervalo, ausência e carga são fatos, não ponto eletrônico.

## Privacidade / retenção
- [ ] Aplicativo excluído não grava nome/contexto.
- [ ] Domínio excluído não é persistido.
- [ ] Retenção expurga dados vencidos e registra `execucoes_retencao`.
- [ ] Documentos jurídicos/autorização não são apagados pela retenção automática.

## Agente / atualização
- [ ] Serviço inicia automaticamente.
- [ ] Watchdog restaura processo de sessão quando aplicável.
- [ ] Atualização baixa e valida SHA-256.
- [ ] Pacote incompleto é rejeitado.
- [ ] Versão nova cria marcador local de saúde.
- [ ] Falha de saúde dispara rollback.
- [ ] Backups antigos são reduzidos à quantidade prevista.

## Coexistência VISÃO
- [ ] Clara funciona com VISÃO ausente.
- [ ] Cliente sem VISÃO não vê Contexto TI.
- [ ] Cliente com VISÃO contratado + integração ativa vê Contexto TI.
- [ ] Contexto recebido aparece somente no tenant correto.
- [ ] Atualizaçãa Clara é postergada quando manutenção recente do VISÃO é detectada.
- [ ] Teste conjunto por um dia útil sem conflito relevante.

## Gate
Somente após todos os itens críticos acima serem aprovados a v0.5.8-RC deve ser promovida para baseline v1.0.
