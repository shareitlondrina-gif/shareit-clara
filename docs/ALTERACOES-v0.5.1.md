# Alterações — Share IT .Clara v0.5.1

## Objetivo

A v0.5.1 preserva integralmente a candidata v0.5.0 e corrige a principal falha observada na homologação: o publicador aguardava a nova versão, mas o Render continuava respondendo `0.3.2` e não havia um diagnóstico local simples de localizar.

## Publicador

- cria `artifacts/logs` antes de iniciar a automação;
- mantém transcript detalhado por execução;
- mantém `PUBLICACAO-ULTIMA.log` na raiz para localização imediata;
- `PUBLICAR-CLARA.cmd` também grava `artifacts/logs/Publicador-Launcher.log`;
- em erro, o CMD mostra os caminhos exatos do log e do diagnóstico;
- o publicador registra repositório e branch sem expor credenciais embutidas em URL;
- depois do `git push`, confirma que o SHA de `HEAD` é o mesmo de `origin/<branch>`;
- não usa `git push --force`.

## Build ID

Cada execução do publicador gera um identificador único no arquivo `BUILD.txt` antes do build. O arquivo é publicado junto da Web/API.

Os endpoints passam a devolver:

- `versao`;
- `buildId`;
- `commit` do Render quando `RENDER_GIT_COMMIT` estiver disponível.

Rotas:

- `/api/versao`;
- `/api/health/live`;
- `/api/health`.

O gate online só conclui com sucesso quando a versão e o `buildId` correspondem exatamente à execução atual.

## Diagnóstico automático

Em falha de publicação/validação, é gerado:

```text
artifacts\logs\DIAGNOSTICO-PUBLICACAO-v<versao>-<data>.txt
DIAGNOSTICO-PUBLICACAO-ULTIMO.txt
```

O diagnóstico inclui, sem segredos:

- versão e Build ID esperados;
- commit esperado;
- repositório/branch;
- resposta de `/api/versao`;
- resposta de `/api/health/live`;
- resposta de `/api/health`;
- `git status`;
- `git rev-parse HEAD`;
- último commit;
- SHA observado em `origin/<branch>`;
- indicação se Deploy Hook estava configurado/acionado.

## Deploy Hook de fallback

Se a variável local `CLARA_RENDER_DEPLOY_HOOK` estiver configurada e `UsarDeployHookComoFallback=true`, o publicador pode acionar o hook uma única vez após o número de segundos definido em `SegundosAntesDeployHookFallback`.

A URL do hook nunca é escrita no log ou no diagnóstico.

## Banco

Nenhuma migration nova.

As migrations `0001` a `0006` permanecem imutáveis em relação à v0.5.0.
