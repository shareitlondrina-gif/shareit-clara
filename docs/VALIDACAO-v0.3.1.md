# Validação estrutural - Share IT .Clara v0.3.1

## Publicação

1. Executar somente `PUBLICAR-CLARA.cmd`.
2. Confirmar restore, build, testes e publish dos componentes.
3. Confirmar que o ZIP local `artifacts\Clara-v0.3.1.zip` é criado antes da validação online.
4. Confirmar push normal para `origin/main`, sem `--force`.

## Render

1. `/api/health/live` deve responder HTTP 200 na v0.3.1 mesmo enquanto o banco estiver inicializando.
2. O retorno de liveness deve indicar produto `Share IT .Clara`, versão `0.3.1` e aplicação `ok`.
3. `/api/health` pode responder 503 durante a inicialização do banco.
4. Após concluir a inicialização, `/api/health` deve responder:
   - versão `0.3.1`;
   - aplicação `ok`;
   - banco `ok`;
   - nomeBanco `shareit_clara`;
   - migrations `4`.
5. Se o banco falhar, a Web/API deve permanecer acessível em `/api/health/live` e o erro detalhado deve estar nos Logs do Render.

## Funcional

Depois do health completo ficar saudável, repetir a homologação funcional da v0.3.0:

- login/logout do Master;
- criação de empresa pelo Master;
- criação do primeiro Administrador do tenant;
- isolamento entre tenants;
- criação de usuário Padrão;
- matriz de módulos/perfis;
- empresa demonstração e dashboards fictícios.

A compilação real .NET no Windows continua sendo a validação autoritativa porque o ambiente de geração deste pacote não possui SDK .NET 10.
