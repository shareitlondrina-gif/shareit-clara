# Validação - Share IT .Clara v0.5.6

## Gate esperado no Windows / Render

1. `PUBLICAR-CLARA.cmd` executa restore, build e testes;
2. Dockerfile continua usando caminhos absolutos `/src/Ferramentas/...`;
3. GitHub confirma o SHA remoto;
4. Render publica a versão `0.5.6` e o Build ID da execução;
5. `/api/health` confirma banco `shareit_clara`;
6. `/api/health` confirma **7 migrations**;
7. login `demonstracao@clara.local` continua válido;
8. primeira navegação do tenant Demonstração repara automaticamente a carga caso esteja incompleta/desatualizada.

## Validação funcional da Demonstração

Após entrar na empresa Horizonte Contabilidade - Demonstração:

- **Agora** deve exibir 15 estações, com 12 online e 3 offline;
- cards Ativos, Reunião e Ociosos devem estar populados;
- **Pontos de atenção identificados** não deve ficar zerado;
- **Insights do ambiente** deve listar evidências recentes;
- **Oportunidades** deve exibir itens nos cinco indicadores principais;
- **Evidências** deve exibir fatos textuais e galeria visual;
- **Home x Escritório** deve mostrar diferenças entre escritório e home office e gerar Insight principal;
- **Detalhe da estação** deve mostrar timeline, pontos de atenção, “No que trabalhou hoje” e, quando disponível, capturas fictícias.

## Segurança

As capturas incluídas no tenant Demonstração são arquivos estáticos e fictícios. Nenhuma captura real é coletada nesta versão.
