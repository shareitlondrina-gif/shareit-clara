# Clara v0.6.0-RC1-R6 — identidade e publicação unificadas

Esta revisão consolida a identidade do produto como **Clara** e torna `PUBLICAR-CLARA.cmd` o único comando oficial para validar, compilar, testar, empacotar e publicar.

## Identidade

- Produto: **Clara**.
- Solução: `Clara.sln`.
- Projetos/assemblies/namespaces: `Clara.*`.
- Serviço Windows: `Clara.Agente`.
- Pastas novas: `C:\Program Files\Clara` e `C:\ProgramData\Clara`.
- Native Messaging Host: `br.com.clara.agent`.
- Interface, e-mails, logs, instalador, documentação e artefatos usam somente **Clara**.
- O logotipo horizontal antigo foi removido; a interface usa o ícone da Clara acompanhado do nome `Clara` em texto.

As migrations `0001` a `0011` permanecem **imutáveis**, inclusive textos históricos internos, porque já podem ter sido aplicadas e fazem parte da rastreabilidade do banco. A migration `0012_identidade_clara.sql` faz a transição dos objetos ativos que ainda precisavam de normalização. Nenhum componente ativo novo deve usar a identidade anterior.

## Padrão de código

Todo arquivo C# da solução possui cabeçalho com:

- `Programador: Paulo Andrade`;
- objetivo do componente;
- origem dos dados/comandos;
- destino dos dados/comandos;
- boas práticas aplicáveis.

Scripts PowerShell/CMD, JavaScript e views Razor seguem o mesmo princípio. `Directory.Build.props` também define `Authors=Paulo Andrade`, `Company=Clara` e `Product=Clara`.

## Publicação em um único comando

Execute somente:

`PUBLICAR-CLARA.cmd`

O fluxo executa, nesta ordem:

1. validação estrutural e de identidade;
2. conferência das 12 migrations;
3. verificação de arquivos obrigatórios e ausência de segredos;
4. conferência dos cabeçalhos de autoria;
5. geração do Build ID único em `logs\BUILD.txt`, antes da compilação;
6. `dotnet restore`;
7. `dotnet build -c Release`;
8. `dotnet test`;
9. somente com tudo aprovado, empacotamento dos componentes Windows/Web reutilizando o mesmo Build ID;
10. Git/Render conforme ConfigSys;
11. gate online de versão, revisão, Build ID, banco lógico e migrations;
12. navegador aberto somente após a confirmação online do build recém-publicado.

Se qualquer etapa anterior à publicação falhar, Git/Render não são alterados. `Publicar-Clara.ps1` também bloqueia execução direta sem o gate iniciado pelo `PUBLICAR-CLARA.cmd`.

## Banco e compatibilidade

O nome lógico novo é `clara`. O código não depende mais de um nome físico histórico fixo: ele valida o banco configurado em `CLARA_BANCO_NOME`, permitindo transição controlada do ambiente atual para a arquitetura Neon Management → Administradora → Cliente.

A atualização do agente procura instalações anteriores da própria Clara dinamicamente pelo executável `Clara.Agente`, sem expor identidade histórica na interface ou no código ativo.

## Segurança

Segredos continuam fora do Git/ZIP. Evidências ficam fora do PostgreSQL; o banco guarda metadados, hash, autorização, retenção e localização do objeto. Áudio/webcam permanecem bloqueados por padrão.
