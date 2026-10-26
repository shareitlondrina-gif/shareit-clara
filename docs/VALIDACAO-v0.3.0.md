# Validação estrutural - Clara v0.3.0

Pontos esperados para homologação no Windows/Render:

1. `PUBLICAR-CLARA.cmd` compila e testa a solução;
2. health online retorna versão `0.3.0`, banco `clara` e 4 migrations;
3. logout/login do usuário inicial passa a exibir perfil Master;
4. `/master/empresas` permite criar um novo tenant e administrador;
5. login do administrador criado acessa apenas seu tenant;
6. administrador cria usuário Padrão em `/administracao/usuarios`;
7. `/administracao/perfis` altera módulos do perfil Padrão;
8. usuário Padrão recebe a matriz no próximo login e não acessa rotas sem permissão;
9. botão Master de demonstração cria/atualiza tenant `demonstracao`;
10. login `demonstracao@clara.local` / `demonstracao` apresenta dados fictícios.

A compilação real .NET continua sendo a validação autoritativa porque o ambiente de geração do pacote não possui SDK .NET.
