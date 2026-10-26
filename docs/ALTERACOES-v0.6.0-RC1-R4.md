# Alterações — Clara v0.6.0-RC1-R4

**Programador:** Paulo Andrade

## Identidade

- Removida a identidade anterior de todos os componentes ativos.
- Solução, projetos, namespaces e assemblies renomeados para `Clara.*`.
- Serviço Windows renomeado para `Clara.Agente`.
- Pastas padrão simplificadas para `C:\Program Files\Clara` e `C:\ProgramData\Clara`.
- Native Messaging Host passa a `br.com.clara.agent`.
- UI usa ícone + nome `Clara`, sem wordmark anterior.
- Migrations 0001–0011 preservadas byte a byte por imutabilidade histórica.
- Migration 0012 normaliza objetos ativos do banco para a identidade Clara.

## Engenharia

- Todos os 85 arquivos C# recebem cabeçalho de autoria e fluxo: objetivo, origem, destino e boas práticas.
- `Directory.Build.props` define Paulo Andrade como autor e Clara como produto/empresa do assembly.
- Scripts operacionais e views recebem identificação equivalente.
- Configurações de repositório e URL pública deixam de inventar valores padrão: devem vir do ConfigSys/ambiente real.

## Publicação

- `PUBLICAR-CLARA.cmd` é o único comando oficial na raiz.
- O comando executa o validador PowerShell diretamente.
- O gate executa restore, build Release e test antes de liberar publicação.
- O publicador PowerShell recusa execução direta sem o gate do launcher.
- Removido o launcher separado `VALIDAR-ANTES-DE-PUBLICAR.cmd`.
- O validador verifica 12 migrations, identidade ativa somente Clara, cabeçalhos de autoria, segredos e arquivos obrigatórios.
