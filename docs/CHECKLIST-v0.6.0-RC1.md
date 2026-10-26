# Checklist — v0.6.0-RC1

- [ ] 10 migrations detectadas; 0001–0009 preservadas.
- [ ] Migration 0010 aplicada em cópia/homologação antes de produção.
- [ ] Administradora criada pelo Master.
- [ ] Empresa criada pela Administradora.
- [ ] Código Windows gerado, consumido uma vez e rejeitado no segundo uso.
- [ ] Código expirado/revogado rejeitado.
- [ ] Código Android gerado sem permitir registro no endpoint Windows.
- [ ] Código de Parar/Reparar/Desinstalar validado por máquina e operação.
- [ ] `CLARA_COFRE_MASTER_KEY_BASE64` configurada fora do Git.
- [ ] R2/S3: teste grava/exclui objeto privado.
- [ ] Screenshot: banco guarda metadados/hash; arquivo não fica no PostgreSQL.
- [ ] `/evidencias/arquivo/{id}` exige autenticação e isolamento por tenant.
- [ ] Build/test Windows executados antes de publicar.
