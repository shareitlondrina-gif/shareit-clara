# Validação — Clara v0.4.0

## Estado do pacote

A revisão desta entrega incluiu validação estática do conteúdo, preservação das migrations publicadas, revisão de rotas/contratos e conferência do cenário de demonstração.

**Este ambiente de preparação não possui o .NET SDK instalado**, portanto não foi possível executar `dotnet restore`, `dotnet build`, `dotnet test` nem publicar a aplicação aqui. A v0.4.0 não deve ser marcada como homologada até passar pelo publicador oficial em Windows.

## Gate obrigatório

Executar na pasta oficial:

```text
C:\SysRelease\Clara\PUBLICAR-CLARA.cmd
```

O resultado deve confirmar:

1. `.NET SDK 10`;
2. `dotnet restore` sem erro;
3. `dotnet build -c Release` sem erro;
4. testes sem erro;
5. publish de Web, Agente, Sessão e Atualizador;
6. aplicação da migration `0005` no banco lógico `clara`;
7. `/api/health/live` com aplicação `ok`;
8. `/api/health` com banco `clara`, banco `ok` e **5 migrations**;
9. `/api/versao` retornando `0.4.0`;
10. login Master;
11. criação/atualização da empresa demonstração;
12. login `demonstracao@clara.local`;
13. navegação Agora/Estações/Aplicativos/Home x Escritório/Índice/Oportunidades/Evidências/Configurações;
14. clique em cards/gráficos abrindo a janela flutuante com lista;
15. alteração de configuração e sincronização da política por um agente de homologação;
16. restart do serviço Render sem invalidar os cookies existentes além do comportamento normal de autenticação.

## Validação visual recomendada

Na empresa demonstração, conferir como referência:

```text
15 estações
12 online
7 ativas
2 em reunião
3 ociosas
3 offline
```

A soma dos subestados não deve ser interpretada como soma independente de Online: Ativo/Reunião/Ocioso são estados internos das estações que estão online, enquanto Offline é exclusão por heartbeat.

## Proteções a validar

- Master não deve abrir dashboards de dados operacionais de clientes;
- tenant A não pode consultar/editar tenant B;
- `Captura de screenshots` permanece desativada mesmo se houver tentativa manual no POST;
- nenhum token/senha/segredo deve aparecer em release, log ou repositório;
- `chaves_protecao_dados` não deve ser exposta por tela administrativa;
- migrations `0001`–`0004` devem permanecer byte a byte inalteradas em relação à baseline publicada.
