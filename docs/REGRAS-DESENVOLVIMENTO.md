# Regras-mestre — Clara

1. O nome do produto é **Clara**. Não introduzir outra marca em solução, namespace, assembly, executável, serviço, interface, log, documentação ou objeto novo de banco.
2. Migrations publicadas são imutáveis. Identificadores históricos existentes nelas são tratados por migrations posteriores, nunca por edição retroativa.
3. Código próprio deve usar português claro quando tecnicamente seguro; nomes impostos por frameworks/protocolos podem permanecer no padrão técnico.
4. Todo arquivo de código relevante deve registrar `Programador: Paulo Andrade`, objetivo, origem, destino e boas práticas.
5. Classes e métodos devem ter responsabilidade única, validação de entrada, tratamento explícito de erro e dependências injetadas quando aplicável.
6. Segredos, tokens, senhas, connection strings e chaves privadas nunca entram no código, Git, ZIP ou logs.
7. Banco PostgreSQL usa migrations sequenciais e isolamento por Administradora/Cliente. O nome físico vem de configuração; não hardcodar infraestrutura histórica.
8. O agente principal é Windows Service; telemetria dependente da sessão fica em processo interativo separado.
9. Não implementar keylogger, captura de senhas, conteúdo digitado, bloqueio de sites ou coleta de conteúdo pessoal.
10. Mídias/evidências ficam fora do PostgreSQL; o banco guarda metadados, hash, chave, retenção, permissões e auditoria.
11. Screenshots/gravação são recursos governados. Áudio/câmera permanecem desabilitados por padrão e exigem autorização formal para qualquer evolução futura.
12. Atualização do agente usa versão, SHA-256, backup, rollback, watchdog e registro de resultado.
13. Configurações persistentes locais ficam em `C:\ConfigSys\Clara`; instalação usa `C:\Program Files\Clara` e dados locais `C:\ProgramData\Clara`.
14. O único comando oficial de release é `PUBLICAR-CLARA.cmd`. Ele deve validar, restaurar, compilar, testar e só então publicar.
15. `Publicar-Clara.ps1` não pode ser usado para contornar o gate de validação.
16. Uma funcionalidade só é concluída quando compila, passa testes, funciona ponta a ponta e possui diagnóstico por logs/health checks.
