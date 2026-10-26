# Checklist — Clara v0.6.0-RC1-R4

## Gate único
- [ ] Executar somente `PUBLICAR-CLARA.cmd`.
- [ ] Gate local confirma identidade ativa somente Clara.
- [ ] 12 migrations detectadas; 0001–0011 preservadas byte a byte.
- [ ] Migration 0012 aplicada.
- [ ] Restore concluído.
- [ ] Build Release concluído.
- [ ] Testes concluídos.
- [ ] Publicação somente inicia depois do gate local aprovado.
- [ ] Gate online confirma versão, Build ID, banco lógico Clara e 12 migrations.

## Identidade
- [ ] Solução `Clara.sln`.
- [ ] Projetos/assemblies/namespaces `Clara.*`.
- [ ] Serviço Windows `Clara.Agente`.
- [ ] Instalação em `C:\Program Files\Clara`.
- [ ] Dados em `C:\ProgramData\Clara`.
- [ ] UI, logs e documentação exibem somente `Clara`.
- [ ] Ícone + nome Clara aparecem corretamente no login e menu lateral.

## Código
- [ ] Todos os arquivos C# possuem `Programador: Paulo Andrade`.
- [ ] Cabeçalhos registram objetivo, origem, destino e boas práticas.
- [ ] Nenhum segredo presente no ZIP/Git.

## Interface já consolidada
- [ ] Tema claro validado.
- [ ] Tema escuro validado.
- [ ] Ativa verde / Trial azul / Suspensa âmbar / Cancelada vermelha.
- [ ] Clique no usuário abre Perfil e Sair.
- [ ] Perfil salva foto, nome, cargo e telefone.
- [ ] Foto JPG, PNG e WebP até 2 MB.
- [ ] Logout pelo menu superior encerra a sessão.
