-- Share IT .Clara v0.5.6
-- Evidências visuais e suporte à demonstração segura.
-- IMPORTANTE: migrations 0001..0006 permanecem imutáveis.

CREATE TABLE IF NOT EXISTS evidencias_visuais (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    ativo_id uuid REFERENCES ativos(id) ON DELETE SET NULL,
    evidencia_id uuid REFERENCES evidencias_contextuais(id) ON DELETE SET NULL,
    pessoa varchar(200),
    tipo varchar(40) NOT NULL DEFAULT 'Screenshot',
    titulo varchar(250) NOT NULL,
    descricao text,
    arquivo_relativo text NOT NULL,
    capturada_em_utc timestamptz NOT NULL DEFAULT now(),
    demonstrativa boolean NOT NULL DEFAULT false,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_evidencias_visuais_tipo CHECK (tipo IN ('Screenshot','Imagem'))
);

CREATE INDEX IF NOT EXISTS ix_evidencias_visuais_tenant_data
    ON evidencias_visuais(tenant_id, capturada_em_utc DESC);
CREATE INDEX IF NOT EXISTS ix_evidencias_visuais_ativo_data
    ON evidencias_visuais(ativo_id, capturada_em_utc DESC);

COMMENT ON TABLE evidencias_visuais IS
    'Metadados de evidências visuais autorizadas. O binário/arquivo é mantido fora do banco; captura real deve respeitar política do tenant, RBAC, auditoria e retenção.';
