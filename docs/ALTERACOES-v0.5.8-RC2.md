# Alterações — Share IT .Clara v0.5.8-RC2

## Correção de compilação

A RC original referenciava na view `Views/Painel/Estacao.cshtml` a propriedade `SobrescritaEstacao`, mas o contrato `ConfiguracaoJornadaEstacao` expõe a propriedade `PossuiSobrescrita`.

Isso gerava três erros CS1061 durante a compilação Razor, porque a mesma propriedade inexistente era acessada mais de uma vez na view.

### Correção aplicada

- `jornada.SobrescritaEstacao` → `jornada.PossuiSobrescrita`;
- nenhum contrato, migration ou estrutura de banco foi alterado;
- versão da aplicação permanece `0.5.8`;
- rótulo do pacote passa a `v0.5.8-RC2`;
- migrations esperadas permanecem em **9**.

## Validação adicional

Foi executada conferência estática entre as propriedades usadas via `jornada.*` na view de estação e os campos declarados no record `ConfiguracaoJornadaEstacao`. Após a correção, não há propriedade de jornada desconhecida na view.
