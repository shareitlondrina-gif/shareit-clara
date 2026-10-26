-- Share IT .Clara v0.6.0-RC1
-- Fundação de dispositivos, códigos administrativos, armazenamento externo e transição multi-banco.
-- Migrations 0001..0009 permanecem imutáveis.

-- 1) Código de registro: continua armazenado somente como hash; agora identifica o tipo de dispositivo.
ALTER TABLE tokens_instalacao ADD COLUMN IF NOT EXISTS tipo_dispositivo varchar(20) NOT NULL DEFAULT 'Windows';
ALTER TABLE tokens_instalacao ADD COLUMN IF NOT EXISTS revogado_em_utc timestamptz;
ALTER TABLE tokens_instalacao ADD COLUMN IF NOT EXISTS descricao varchar(200);
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_tokens_instalacao_tipo_dispositivo') THEN
    ALTER TABLE tokens_instalacao ADD CONSTRAINT ck_tokens_instalacao_tipo_dispositivo CHECK (tipo_dispositivo IN ('Windows','Android'));
  END IF;
END $$;
CREATE INDEX IF NOT EXISTS ix_tokens_instalacao_validos ON tokens_instalacao(tenant_id,empresa_id,tipo_dispositivo,expira_em_utc) WHERE usado_em_utc IS NULL AND revogado_em_utc IS NULL;

-- 2) Entidade canônica de dispositivo. "ativos" permanece por compatibilidade durante a transição.
CREATE TABLE IF NOT EXISTS dispositivos (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    empresa_id uuid NOT NULL REFERENCES empresas(id) ON DELETE RESTRICT,
    ativo_legado_id uuid UNIQUE REFERENCES ativos(id) ON DELETE SET NULL,
    tipo varchar(20) NOT NULL,
    nome varchar(255) NOT NULL,
    plataforma varchar(80),
    identificador_externo varchar(255),
    responsavel varchar(200),
    patrimonio varchar(100),
    status varchar(30) NOT NULL DEFAULT 'Pendente',
    ultima_comunicacao_utc timestamptz,
    ativo boolean NOT NULL DEFAULT true,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_dispositivos_tipo CHECK (tipo IN ('Windows','Android')),
    CONSTRAINT uq_dispositivos_tenant_tipo_nome UNIQUE(tenant_id,tipo,nome)
);
CREATE INDEX IF NOT EXISTS ix_dispositivos_tenant_empresa ON dispositivos(tenant_id,empresa_id,tipo,ativo);

INSERT INTO dispositivos(tenant_id,empresa_id,ativo_legado_id,tipo,nome,plataforma,responsavel,patrimonio,status,ativo,criado_em_utc,atualizado_em_utc)
SELECT a.tenant_id,a.empresa_id,a.id,'Windows',a.hostname,'Windows',a.responsavel,a.patrimonio,
       CASE WHEN ag.ultima_comunicacao_utc >= now()-interval '5 minutes' THEN 'Online' ELSE 'Offline' END,
       a.ativo,a.criado_em_utc,now()
FROM ativos a
LEFT JOIN agentes ag ON ag.tenant_id=a.tenant_id AND ag.ativo_id=a.id AND ag.ativo=true
ON CONFLICT(ativo_legado_id) DO UPDATE SET
  empresa_id=excluded.empresa_id,nome=excluded.nome,responsavel=excluded.responsavel,patrimonio=excluded.patrimonio,
  status=excluded.status,ativo=excluded.ativo,atualizado_em_utc=now();

-- 3) Códigos oficiais de manutenção. Uso único, vinculados a dispositivo e operação.
CREATE TABLE IF NOT EXISTS codigos_manutencao (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    ativo_id uuid NOT NULL REFERENCES ativos(id) ON DELETE CASCADE,
    operacao varchar(20) NOT NULL,
    codigo_hash char(64) NOT NULL UNIQUE,
    motivo varchar(300),
    expira_em_utc timestamptz NOT NULL,
    usado_em_utc timestamptz,
    revogado_em_utc timestamptz,
    criado_por_usuario_id uuid REFERENCES usuarios(id) ON DELETE SET NULL,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_codigos_manutencao_operacao CHECK (operacao IN ('Parar','Desinstalar','Reparar'))
);
CREATE INDEX IF NOT EXISTS ix_codigos_manutencao_validos ON codigos_manutencao(tenant_id,ativo_id,operacao,expira_em_utc) WHERE usado_em_utc IS NULL AND revogado_em_utc IS NULL;

-- 4) Armazenamento: o PostgreSQL guarda metadados; o binário fica no provedor configurado.
CREATE TABLE IF NOT EXISTS configuracoes_armazenamento (
    tenant_id uuid PRIMARY KEY REFERENCES tenants(id) ON DELETE CASCADE,
    provedor varchar(30) NOT NULL DEFAULT 'R2S3',
    endpoint text,
    container_bucket varchar(255),
    pasta_base varchar(500) NOT NULL DEFAULT 'clara/evidencias',
    regiao varchar(80) NOT NULL DEFAULT 'auto',
    segredo_id varchar(180),
    status varchar(30) NOT NULL DEFAULT 'NaoConfigurado',
    ultimo_teste_utc timestamptz,
    ultimo_erro text,
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_config_armazenamento_provedor CHECK (provedor IN ('R2S3','Local','SharePoint','GoogleDrive','HostGatorSftp'))
);

CREATE TABLE IF NOT EXISTS segredos_integracao (
    segredo_id varchar(180) PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    finalidade varchar(60) NOT NULL,
    cipher_base64 text NOT NULL,
    nonce_base64 varchar(80) NOT NULL,
    tag_base64 varchar(80) NOT NULL,
    key_version integer NOT NULL DEFAULT 1,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now()
);
COMMENT ON TABLE segredos_integracao IS 'Segredos cifrados AES-256-GCM. A chave-raiz CLARA_COFRE_MASTER_KEY_BASE64 permanece somente no backend.';

ALTER TABLE evidencias_visuais ADD COLUMN IF NOT EXISTS provedor_armazenamento varchar(30);
ALTER TABLE evidencias_visuais ADD COLUMN IF NOT EXISTS chave_objeto text;
ALTER TABLE evidencias_visuais ADD COLUMN IF NOT EXISTS mime_type varchar(120);
ALTER TABLE evidencias_visuais ADD COLUMN IF NOT EXISTS tamanho_bytes bigint;
ALTER TABLE evidencias_visuais ADD COLUMN IF NOT EXISTS sha256 char(64);
ALTER TABLE evidencias_visuais ADD COLUMN IF NOT EXISTS excluida_armazenamento_em_utc timestamptz;

-- 5) Catálogo de transição para o padrão Management -> Administradora -> Cliente.
CREATE TABLE IF NOT EXISTS clara_infra_bancos (
    banco_logico_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tipo varchar(20) NOT NULL CHECK (tipo IN ('Management','Administradora','Cliente')),
    codigo varchar(80) NOT NULL UNIQUE,
    nome_exibicao varchar(240) NOT NULL,
    slug varchar(120) NOT NULL,
    pai_banco_logico_id uuid REFERENCES clara_infra_bancos(banco_logico_id),
    provedor varchar(40) NOT NULL DEFAULT 'Neon',
    projeto_provedor_id varchar(120),
    projeto_provedor_nome varchar(256),
    database_name varchar(120),
    segredo_id varchar(180),
    versao_schema varchar(30),
    status varchar(30) NOT NULL DEFAULT 'Planejado',
    ultimo_health_utc timestamptz,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_clara_infra_bancos_pai ON clara_infra_bancos(pai_banco_logico_id,tipo,status);

CREATE TABLE IF NOT EXISTS clara_provisionamentos (
    provisionamento_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tipo varchar(20) NOT NULL CHECK (tipo IN ('Administradora','Cliente')),
    referencia varchar(180) NOT NULL,
    etapa varchar(80) NOT NULL,
    percentual integer NOT NULL DEFAULT 0 CHECK (percentual BETWEEN 0 AND 100),
    mensagem varchar(500),
    status varchar(30) NOT NULL DEFAULT 'EmAndamento',
    erro text,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    concluido_em_utc timestamptz
);
