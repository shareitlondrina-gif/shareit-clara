# Regras-mestre — Share IT .Clara

1. O Share IT .Clara é um produto novo. Não reutilizar código, banco, migrations, entidades ou módulos do Monitor legado.
2. O produto é exclusivo para inteligência de performance de pessoas. Não incluir Service Desk, projetos, CMDB, RMM, bloqueio de sites ou suporte remoto.
3. Código próprio em português claro quando tecnicamente seguro. APIs e nomes obrigatórios de frameworks não devem ser traduzidos.
4. Classes próprias relevantes devem documentar objetivo, origem, destino, dependências, impacto e `Desenvolvedor: Paulo Andrade`.
5. Banco PostgreSQL novo, com tabelas e colunas intuitivas em português, migrations SQL sequenciais e imutáveis depois de publicadas.
6. Toda estrutura aplicável deve respeitar isolamento por tenant.
7. O agente principal é Windows Service; telemetria dependente da sessão deve ficar em processo interativo separado.
8. Não implementar keylogger, captura de senhas, conteúdo digitado, controle remoto, suporte remoto ou bloqueio de sites.
9. Mídias e evidências ficam fora do PostgreSQL; o banco guarda metadados, hash, chave/caminho, tenant, retenção, permissões e auditoria.
10. Screenshots e gravação são recursos configuráveis. Áudio/câmera vêm desabilitados e exigirão evidência jurídica para habilitação.
11. Atualização do agente é automática, com manifesto de versão, SHA-256, backup, rollback e relatório de resultado.
12. Segredos nunca entram no Git nem no ZIP. Configurações persistentes locais ficam em `C:\ConfigSys\Clara`.
13. A operação normal de publicação deve partir de um único `PUBLICAR-CLARA.cmd`.
14. Uma funcionalidade só é concluída quando compila, valida, funciona ponta a ponta e é diagnosticável por logs/health checks.
