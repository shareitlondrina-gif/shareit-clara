# Validação — Share IT .Clara v0.5.8-RC

## Escopo

Candidata de fechamento técnico/comercial antes da baseline v1.0.

## Validação estática concluída

- versão 0.5.8 e build RC coerentes;
- 9 migrations;
- migrations 0001 a 0008 preservadas byte a byte em relação à v0.5.7;
- nova migration 0009 isolada;
- JSON/XML válidos;
- componentes de Pulso, retenção, relatórios agendados, integração opcional .VISÃO e watchdog presentes;
- regras de logs mantidas na pasta `logs`;
- nenhum arquivo real de segredo incluído;
- Dockerfile mantém a correção de caminhos absolutos `/src/Ferramentas/...`;
- integração .VISÃO condicionada a produto contratado + integração ativa.

O relatório detalhado está em `docs/VALIDACAO-ESTATICA-v0.5.8-RC.txt`.

## Gate obrigatório no Windows

Este ambiente de geração não possui .NET SDK 10/PowerShell. A RC só pode ser homologada após `PUBLICAR-CLARA.cmd` executar no Windows e comprovar:

1. restore/build/testes;
2. geração dos componentes Windows;
3. push/commit GitHub;
4. deploy Render;
5. `/api/versao` = `0.5.8` + Build ID correto;
6. `/api/health` = banco `shareit_clara` + 9 migrations;
7. atualização/rollback do agente;
8. SMTP e relatório agendado;
9. retenção e timezone;
10. coexistência física com .VISÃO quando aplicável.

## Assinatura de pacote

Nesta RC a integridade continua protegida por SHA-256, além de backup, health pós-atualização e rollback. Assinatura criptográfica com chave privada de produção ainda não está ativa e deverá ser tratada como hardening posterior se a infraestrutura de chaves for provisionada.
