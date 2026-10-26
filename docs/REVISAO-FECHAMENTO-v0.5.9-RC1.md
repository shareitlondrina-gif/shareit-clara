# Revisão de fechamento — Share IT .Clara v0.5.9-RC1

## Status do núcleo

| Área | Situação para homologação |
|---|---|
| Agora / Estações / Aplicativos | Implementado; validar em ambiente real |
| Histórico / Relatórios | Implementado; validar períodos/filtros/exportações |
| Jornada / período de monitoramento | Implementado; validar timezone e exceções |
| Alertas / Notificações | Implementado; validar regras/deep-links/e-mail |
| Pulso da Operação | Implementado |
| Capacidade / utilização / IA / automação | Implementado como leitura gerencial factual |
| Retenção | Serviço automático implementado; validar expurgo real |
| Atualização do agente | Implementada com backup/rollback/watchdog; validar end-to-end |
| .Clara + .VISÃO | Independentes; correlação opcional condicionada à contratação |
| SMTP | Estrutura pronta; depende de credencial/provedor real |
| Governança | Fundação implementada |

## Itens ainda não concluídos

1. Evidências restritas reais: screenshots, live view e gravação de tela.
2. Áudio/webcam: manter bloqueados até implementação e validação excepcional.
3. Conectores corporativos Microsoft/Google/Zoom: dependem de credenciais e OAuth reais.
4. Assinatura criptográfica dos pacotes: hardening dependente de chave privada de produção.
5. Homologação física da coexistência com .VISÃO, atualização automática e rollback.

## Caminho proposto

- **v0.5.9-RC1 (esta versão):** fechar e homologar o núcleo.
- **v0.6.0-RC:** evidências restritas reais + conectores externos disponíveis para homologação, mantendo áudio/webcam bloqueados por padrão.
- **v1.0.0:** promoção da baseline comercial após homologação.

Se a v0.5.9-RC1 revelar somente bugs de compilação/runtime, eles devem ser tratados como RC2/RC3 do mesmo marco, sem criar novas funcionalidades.
