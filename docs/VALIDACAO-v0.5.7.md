# Validação — Share IT .Clara v0.5.7

## Escopo

A validação realizada neste ambiente é **estática**. O SDK .NET 10 e PowerShell não estão disponíveis aqui, portanto a compilação, testes automatizados e publicação real precisam ser executados no Windows pelo `PUBLICAR-CLARA.cmd`.

## Verificações estáticas

- versão 0.5.7 consistente em `VERSAO.txt`, `Directory.Build.props`, Web, agente, extensão, layout e demo;
- 8 migrations presentes;
- migrations 0001–0007 idênticas às da v0.5.6;
- migration 0008 adicionada;
- Dockerfile mantém caminhos absolutos `/src/Ferramentas/...` corrigidos na v0.5.4;
- logs do publicador continuam centralizados em `logs/`;
- nenhum Deploy Hook real, senha PostgreSQL real ou senha SMTP encontrado no pacote;
- documentação de Governança/Jurídico presente;
- código de sessão e navegação possui verificação da janela efetiva de monitoramento;
- Supervisor disponível para criação/alteração de usuários;
- `render.yaml` preparado para SMTP sem embutir segredos.

## Gate esperado após publicação

- `/api/versao`: versão `0.5.7` e Build ID da execução;
- `/api/health/live`: aplicação online;
- `/api/health`: `banco=ok`, `nomeBanco=shareit_clara`, `migracoes=8`;
- Central de Notificações abre ao clicar no sino;
- Meu perfil alterna Claro/Escuro/Sistema;
- Administração > Alertas cria regra e mostra histórico;
- Administração > Governança registra documento e calcula SHA-256;
- Relatórios apresenta histórico no período selecionado.

## Observação

SMTP somente enviará mensagens depois que `monitoramento.clara@shareitbrasil.com.br` (ou outra identidade aprovada) estiver provisionado no provedor e as variáveis SMTP forem configuradas no Render.
