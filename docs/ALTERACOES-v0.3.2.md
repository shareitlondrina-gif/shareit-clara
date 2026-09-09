# Share IT .Clara v0.3.2 - Alterações

Correção intermediária de autenticação após o cenário em que a tela `/entrar` carregava, mas o envio da senha retornava HTTP 500.

## Ajustes

- `AutenticacaoController` consulta o estado de inicialização do banco antes de autenticar.
- Exceções de autenticação passam a ser registradas no log e exibidas como mensagem controlada, sem página HTTP 500 genérica.
- O usuário Master de bootstrap tem `senha_hash` e `senha_sal` sincronizados com `CLARA_ADMIN_SENHA` após uma inicialização de banco bem-sucedida.
- Criada rota `/erro` compatível com `UseExceptionHandler`.
- Mantidas intactas todas as migrations publicadas (`0001` a `0004`).
- O login continua protegido por antiforgery, cookie HttpOnly e rate limit.
