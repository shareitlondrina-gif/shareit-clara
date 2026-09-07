# Validação estrutural — Share IT .Clara v0.1.2

Validações realizadas no pacote fonte:

- estrutura da solution e projetos preservada;
- versão alterada para 0.1.2;
- migrations `0001` e `0002` preservadas sem alteração;
- health check mapeado diretamente em `/api/health`;
- endpoints `/api/health/live` e `/api/versao` presentes;
- `render.yaml` aponta health para `/api/health`, Virginia e auto deploy;
- `PUBLICAR-CLARA.cmd` mantém a janela aberta no sucesso e no erro;
- publicador grava log em `artifacts\logs` e compara a versão remota com `VERSAO.txt`;
- regras de proteção do banco `shareit_clara` preservadas.

A compilação .NET deve ser validada no Windows pelo próprio `PUBLICAR-CLARA.cmd`, pois o ambiente de geração deste pacote não possui o SDK .NET instalado.
