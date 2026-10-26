# Validação — Clara v0.5.5

Execute somente `PUBLICAR-CLARA.cmd`.

O `RenderDeployHook` já configurado em `C:\ConfigSys\Clara\SegredosClara.local.xml` deve ser reutilizado automaticamente.

## Gate esperado

1. validação local confirma os quatro scripts de instalação/diagnóstico;
2. restore/build/test local concluem sem o warning `NomeCategoria`;
3. Git push e SHA remoto são confirmados;
4. Render inicia por Auto Deploy ou pelo Deploy Hook de fallback;
5. build Docker não apresenta `cp: cannot stat Ferramentas/Instalacao/...`;
6. `/api/health/live` responde `versao=0.5.5` e o Build ID desta execução;
7. `/api/health` confirma banco `clara`, status `ok` e 6 migrations;
8. `/api/versao` devolve a mesma versão/Build ID;
9. publicação termina como ONLINE E VALIDADO.

## Se o Render falhar

Enviar somente os arquivos da pasta `logs\` e o trecho final do Deploy Log do Render. A URL do Deploy Hook nunca deve ser compartilhada.
