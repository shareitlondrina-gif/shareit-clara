-- Clara v0.6.0-RC1-R4
-- Programador: Paulo Andrade
-- Objetivo: concluir a transição dos objetos ativos do schema para a identidade Clara.
-- Origem: schema atualizado pelas migrations imutáveis 0001-0011.
-- Destino: nomes ativos utilizados pela aplicação Clara.

DO $$
DECLARE
    tabela_origem text;
BEGIN
    IF to_regclass('public.integracoes_produtos_clara') IS NULL THEN
        SELECT table_name INTO tabela_origem
          FROM information_schema.tables
         WHERE table_schema='public'
           AND table_name LIKE 'integracoes_produtos_%'
           AND table_name <> 'integracoes_produtos_clara'
         ORDER BY table_name
         LIMIT 1;

        IF tabela_origem IS NOT NULL THEN
            EXECUTE format('ALTER TABLE %I RENAME TO integracoes_produtos_clara', tabela_origem);
        END IF;
    END IF;
END $$;

DO $$
DECLARE
    nome_atual text;
BEGIN
    SELECT conname INTO nome_atual FROM pg_constraint WHERE conrelid='integracoes_produtos_clara'::regclass AND contype='p' LIMIT 1;
    IF nome_atual IS NOT NULL AND nome_atual <> 'integracoes_produtos_clara_pkey' THEN
        EXECUTE format('ALTER TABLE integracoes_produtos_clara RENAME CONSTRAINT %I TO integracoes_produtos_clara_pkey', nome_atual);
    END IF;

    SELECT conname INTO nome_atual FROM pg_constraint WHERE conrelid='integracoes_produtos_clara'::regclass AND contype='f' LIMIT 1;
    IF nome_atual IS NOT NULL AND nome_atual <> 'integracoes_produtos_clara_tenant_id_fkey' THEN
        EXECUTE format('ALTER TABLE integracoes_produtos_clara RENAME CONSTRAINT %I TO integracoes_produtos_clara_tenant_id_fkey', nome_atual);
    END IF;

    SELECT conname INTO nome_atual FROM pg_constraint WHERE conrelid='integracoes_produtos_clara'::regclass AND contype='c' LIMIT 1;
    IF nome_atual IS NOT NULL AND nome_atual <> 'ck_integracoes_produtos_clara_produto' THEN
        EXECUTE format('ALTER TABLE integracoes_produtos_clara RENAME CONSTRAINT %I TO ck_integracoes_produtos_clara_produto', nome_atual);
    END IF;
END $$;

UPDATE configuracoes_email_tenant
   SET remetente_nome='Clara',
       remetente_email=CASE
           WHEN remetente_email IS NULL OR remetente_email='' OR split_part(remetente_email,'@',1)='monitoramento.clara'
           THEN 'no-reply@clara.local'
           ELSE remetente_email
       END
 WHERE remetente_nome <> 'Clara' OR remetente_nome IS NULL;

-- Normaliza o tenant interno da plataforma sem depender do identificador histórico.
UPDATE tenants t
   SET nome='Clara', identificador='clara'
  FROM assinaturas_tenant a
 WHERE a.tenant_id=t.id
   AND a.plano='Interno'
   AND (t.identificador='clara' OR NOT EXISTS (SELECT 1 FROM tenants x WHERE x.identificador='clara' AND x.id<>t.id));

UPDATE empresas e
   SET nome='Clara'
  FROM assinaturas_tenant a
 WHERE a.tenant_id=e.tenant_id
   AND a.plano='Interno';
