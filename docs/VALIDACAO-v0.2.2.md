# Validação — Clara v0.3.0

## Baseline de origem

A v0.3.0 evolui a baseline online v0.1.2. Não utiliza código do Monitor legado, módulos do VISÃO, Service Desk, projetos, CMDB ou suporte remoto.

## Validações estruturais executadas na geração

- `VERSAO.txt` = `0.3.0`;
- 7 projetos C# presentes e referenciados pela solution;
- 108 arquivos na baseline;
- 50 arquivos C# / aproximadamente 3.470 linhas C#;
- XML de configuração válido;
- JSON válido;
- imagens de identidade presentes em `wwwroot/img` e `docs/IdentidadeVisual`;
- `favicon.ico`, PNGs 32/64/192/512 e manifest presentes;
- `.gitignore` e `.dockerignore` excluem artefatos, ZIPs e arquivos locais de segredos;
- nenhuma credencial local/arquivo de segredo conhecido está incluído no pacote-fonte;
- destino oficial de banco permanece `clara`;
- health check continua validando explicitamente `current_database() = clara`;
- publicador valida versão online e quantidade esperada de migrations;
- publicador sincroniza com a branch remota antes do commit/push e não usa `--force`.
- fluxo Git seguro foi simulado com repositório remoto contendo histórico prévio: novo commit ficou acima da baseline remota e o push foi aceito sem sobrescrever histórico.

## Migrations

A baseline contém:

```text
0001_base_operacional.sql
0002_indices_dashboard.sql
0003_contexto_inventario_integracoes.sql
```

As migrations `0001` e `0002` foram comparadas byte a byte com o ZIP oficial da v0.1.2 e permanecem idênticas.

SHA-256 preservados:

```text
0001: ed572fe355fbd2ca9cbac5cbaef4879fa1d5d09f0cd4bfef883e201a362a31fd
0002: 92c622cff008b5efb736e4d507ca21fb82ed554bf102f5e175bfbd2b38c89d21
```

A migration `0003` adiciona onboarding automático, inventário, aplicativos/sites, regras de local, integrações, agenda, reuniões e métricas agregadas de e-mail sem reescrever o histórico.

## Escopo funcional incluído

- nova identidade visual Clara;
- fundo creme/off-white e navegação lateral;
- onboarding automático de estação;
- coleta de hostname, usuário Windows, serial, UUID, fabricante, modelo, Windows, arquitetura, memória, disco e IP;
- complemento administrativo posterior de patrimônio/responsável/unidade/departamento;
- estados Ativo/Ocioso/Bloqueado/Reunião/Offline;
- timeline;
- painel e classificação de aplicativos;
- fundação de sites por domínio;
- regras Home x Escritório por CIDR;
- Índice Geral preliminar + Confiança dos Dados;
- fundação persistente e visual para Microsoft, Google e Zoom;
- estruturas para agenda, participação em reunião e métricas agregadas de e-mail;
- atualização automática do agente com manifesto, SHA-256, backup e rollback;
- Docker/Render com `libgssapi-krb5-2` e forwarded headers para proxy.

## Compilação definitiva

O ambiente de geração desta entrega não possui o SDK .NET instalado e não tem acesso de rede para baixar NuGet/SDK. Por isso, a compilação real não foi executada aqui.

A validação definitiva ocorre no Windows pelo único ponto de entrada:

```text
PUBLICAR-CLARA.cmd
```

O publicador interrompe o fluxo se `restore`, `build`, `test` ou qualquer `publish` falhar. Após o Git push, aguarda o Render e só considera a publicação saudável quando `/api/health` confirma simultaneamente:

- versão `0.3.0`;
- banco `clara`;
- banco `ok`;
- **3 migrations aplicadas**.

## Critérios de homologação v0.3.0

1. `/api/health` deve responder `aplicacao=ok`, `banco=ok`, `nomeBanco=clara` e `migracoes=3`;
2. `/api/versao` deve responder `0.3.0`;
3. login deve carregar a nova identidade visual;
4. tela Agora deve abrir sem agentes e com a nova hierarquia visual;
5. geração de instalação deve exigir somente a empresa;
6. primeiro agente instalado deve criar/identificar a estação e preencher inventário automaticamente;
7. tela Estações deve permitir completar patrimônio, responsável, unidade e departamento;
8. atividade deve alimentar timeline e painel de aplicativos;
9. classificação de aplicativo deve persistir por tenant;
10. regras Home x Escritório devem aceitar CIDR válido e classificar o local;
11. Índice Geral deve abrir de forma explicável e acompanhado da confiança dos dados;
12. atualização automática deve reconhecer a v0.3.0, validar SHA-256 e preservar rollback;
13. publicação a partir de pasta nova/sem `.git` deve sincronizar o histórico remoto e realizar push fast-forward sem `--force`.
