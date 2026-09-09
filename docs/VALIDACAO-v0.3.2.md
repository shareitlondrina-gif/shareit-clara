# Validação estrutural - Share IT .Clara v0.3.2

1. Executar apenas `PUBLICAR-CLARA.cmd`.
2. Confirmar `/api/health/live` com versão `0.3.2` e aplicação `ok`.
3. Confirmar `/api/health` com `banco=ok`, `nomeBanco=shareit_clara` e `migracoes=4`.
4. Abrir `/entrar` e autenticar com `CLARA_ADMIN_EMAIL` + `CLARA_ADMIN_SENHA` configurados no Render.
5. O Master deve ser redirecionado para `/master/empresas`.
6. Se o banco estiver falho/inicializando, o POST de login deve permanecer na tela de acesso com mensagem controlada, sem HTTP 500 genérico.
7. Forçar uma rota com exceção em ambiente de teste deve cair em `/erro` e preservar o detalhe técnico apenas no log.
8. Confirmar por hash que migrations `0001` a `0004` não foram modificadas em relação à v0.3.1.
