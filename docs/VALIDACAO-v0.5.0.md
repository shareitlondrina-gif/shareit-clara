# Validação — Share IT .Clara v0.5.0

## Estado da candidata

Foi executada revisão estática do código, scripts, projeto, extensão, migrations e empacotamento. **Não foi possível executar `dotnet restore`, `dotnet build`, `dotnet test` ou `dotnet publish` neste ambiente porque o SDK .NET não está instalado.**

A v0.5.0 só deve ser marcada como homologada após o publicador oficial concluir no Windows.

## Gate obrigatório

Executar:

```text
C:\SysRelease\Clara\PUBLICAR-CLARA.cmd
```

Confirmar no log:

1. .NET SDK 10 disponível;
2. restore sem erro;
3. build Release sem erro;
4. testes sem erro;
5. publish Web/API;
6. publish Agente;
7. publish Sessão;
8. publish Atualizador;
9. publish NavegadorHost;
10. cópia da NavegadorExtensao;
11. geração `Clara-Agent-v0.5.0.zip` + SHA-256;
12. geração `Clara-Instalador-v0.5.0.zip`;
13. migration `0006` aplicada;
14. `/api/versao` = `0.5.0`;
15. `/api/health/live` com aplicação ok;
16. `/api/health` com `shareit_clara`, banco ok e **6 migrations**.

## Regressão visual/funcional

Na empresa demonstração, conferir:

```text
15 estações
12 online
7 ativas
2 em reunião
3 ociosas
3 offline
```

Validar as telas:

- Agora;
- Estações;
- detalhe da estação;
- Aplicativos;
- Contexto web;
- Home x Escritório;
- Índice Geral;
- Oportunidades;
- Evidências;
- Configurações;
- Administração > Instalação.

Todos os cards/gráficos com detalhamento devem abrir a janela flutuante e listar os itens/estações que compõem o indicador.

## Contexto web — homologação técnica

Em uma estação de teste:

1. instalar `Clara-Instalador-v0.5.0.zip` como administrador;
2. confirmar serviço `ShareIT.Clara.Agente` iniciado;
3. confirmar tarefa `Share IT .Clara - Sessao`;
4. confirmar `C:\Program Files\Share IT\Clara\NavegadorHost\ShareIT.Clara.NavegadorHost.exe`;
5. confirmar manifesto nativo em `C:\ProgramData\ShareIT\Clara\navegador`;
6. confirmar chaves NativeMessagingHosts do Chrome e Edge em HKLM;
7. carregar/distribuir a extensão de homologação com o ID esperado `diidcakejmmckhhbggepbblogonddjml`;
8. habilitar `Monitorar URLs/domínios` no tenant;
9. aguardar sincronização da política;
10. navegar em domínios HTTP/HTTPS de teste;
11. validar recebimento em Contexto web e drill-down;
12. desabilitar a política e confirmar que novos eventos não são persistidos.

Inspecionar payload/log/banco e confirmar que não existem caminho da URL, query string, título, conteúdo, senha, formulário ou texto digitado.

## Atualização automática 0.4.x → 0.5.0

Em estação separada:

1. partir de agente anterior registrado;
2. disponibilizar manifesto v0.5.0;
3. confirmar download e hash;
4. confirmar backup da instalação anterior;
5. confirmar substituição do pacote completo;
6. confirmar serviço e sessão após update;
7. confirmar criação/atualização do registro do Native Messaging Host pelo `ServicoConectorNavegador`;
8. confirmar reporte `Sucesso` da atualização;
9. simular falha controlada e validar rollback, se houver ambiente seguro para isso.

## SaaS / limites

Validar:

- Master vê plano/status/limite sem abrir dashboard operacional do cliente;
- `Ativa`/`Trial` dentro do vencimento e abaixo do limite permite token;
- `Suspensa`/`Cancelada` bloqueia novo token;
- vencida bloqueia novo token;
- limite atingido bloqueia token e criação de novo ativo;
- estação já cadastrada continua autenticando/heartbeat normalmente;
- tenants continuam isolados.

Na demo, 15/15 é intencional: o portal deve mostrar limite cheio e impedir um 16º cadastro enquanto o limite não for aumentado pelo Master.

## Exportação

- exportar Oportunidades com e sem filtros;
- exportar Evidências com e sem filtros;
- abrir os CSVs no Excel;
- validar acentos, separação de colunas e ausência de fórmula executada a partir de texto cadastrado.

## Proteções

- screenshots permanecem desabilitados no servidor;
- Master não recebe acesso operacional ao tenant;
- nenhum segredo, `.env`, credencial ou chave privada de extensão deve estar no pacote;
- token de instalação continua uso único/temporário;
- migrations 0001–0005 devem permanecer byte a byte iguais à v0.4.0;
- a única migration nova é 0006.
