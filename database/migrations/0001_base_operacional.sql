CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS migracoes_aplicadas (
    id varchar(100) PRIMARY KEY,
    nome varchar(250) NOT NULL,
    aplicada_em_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS tenants (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    nome varchar(200) NOT NULL,
    identificador varchar(100) NOT NULL UNIQUE,
    ativo boolean NOT NULL DEFAULT true,
    criado_em_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS empresas (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    nome varchar(200) NOT NULL,
    documento varchar(30),
    ativo boolean NOT NULL DEFAULT true,
    criado_em_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_empresas_tenant ON empresas(tenant_id);

CREATE TABLE IF NOT EXISTS unidades (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    empresa_id uuid NOT NULL REFERENCES empresas(id) ON DELETE RESTRICT,
    nome varchar(200) NOT NULL,
    ativo boolean NOT NULL DEFAULT true,
    criado_em_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_unidades_tenant_empresa ON unidades(tenant_id, empresa_id);

CREATE TABLE IF NOT EXISTS departamentos (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    empresa_id uuid NOT NULL REFERENCES empresas(id) ON DELETE RESTRICT,
    nome varchar(200) NOT NULL,
    ativo boolean NOT NULL DEFAULT true,
    criado_em_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_departamentos_tenant_empresa ON departamentos(tenant_id, empresa_id);

CREATE TABLE IF NOT EXISTS usuarios (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    nome varchar(200) NOT NULL,
    email varchar(320) NOT NULL,
    senha_hash text NOT NULL,
    senha_sal text NOT NULL,
    perfil varchar(50) NOT NULL DEFAULT 'Administrador',
    ativo boolean NOT NULL DEFAULT true,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_usuarios_tenant_email UNIQUE(tenant_id, email)
);
CREATE INDEX IF NOT EXISTS ix_usuarios_email ON usuarios(lower(email));

CREATE TABLE IF NOT EXISTS ativos (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    empresa_id uuid NOT NULL REFERENCES empresas(id) ON DELETE RESTRICT,
    unidade_id uuid REFERENCES unidades(id) ON DELETE SET NULL,
    departamento_id uuid REFERENCES departamentos(id) ON DELETE SET NULL,
    patrimonio varchar(100),
    hostname varchar(255) NOT NULL,
    responsavel varchar(200),
    tipo varchar(50) NOT NULL DEFAULT 'Computador',
    ativo boolean NOT NULL DEFAULT true,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_ativos_tenant_hostname UNIQUE(tenant_id, hostname)
);
CREATE INDEX IF NOT EXISTS ix_ativos_tenant_empresa ON ativos(tenant_id, empresa_id);

CREATE TABLE IF NOT EXISTS tokens_instalacao (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    ativo_id uuid NOT NULL REFERENCES ativos(id) ON DELETE CASCADE,
    token_hash char(64) NOT NULL UNIQUE,
    expira_em_utc timestamptz NOT NULL,
    usado_em_utc timestamptz,
    criado_por_usuario_id uuid REFERENCES usuarios(id) ON DELETE SET NULL,
    criado_em_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_tokens_instalacao_tenant_expira ON tokens_instalacao(tenant_id, expira_em_utc);

CREATE TABLE IF NOT EXISTS agentes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    ativo_id uuid NOT NULL REFERENCES ativos(id) ON DELETE CASCADE,
    identificador_agente uuid NOT NULL UNIQUE,
    token_hash char(64) NOT NULL,
    versao varchar(30) NOT NULL,
    hostname varchar(255) NOT NULL,
    usuario_windows varchar(255),
    sistema_operacional varchar(255),
    fabricante varchar(200),
    modelo varchar(200),
    ultima_comunicacao_utc timestamptz,
    status varchar(30) NOT NULL DEFAULT 'Registrado',
    ativo boolean NOT NULL DEFAULT true,
    registrado_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_agentes_tenant_ativo UNIQUE(tenant_id, ativo_id)
);
CREATE INDEX IF NOT EXISTS ix_agentes_tenant_comunicacao ON agentes(tenant_id, ultima_comunicacao_utc DESC);

CREATE TABLE IF NOT EXISTS sessoes_usuario (
    id bigserial PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    ativo_id uuid NOT NULL REFERENCES ativos(id) ON DELETE RESTRICT,
    agente_id uuid NOT NULL REFERENCES agentes(id) ON DELETE RESTRICT,
    usuario_windows varchar(255) NOT NULL,
    inicio_utc timestamptz NOT NULL,
    fim_utc timestamptz,
    criado_em_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_sessoes_usuario_tenant_inicio ON sessoes_usuario(tenant_id, inicio_utc DESC);
CREATE INDEX IF NOT EXISTS ix_sessoes_usuario_agente ON sessoes_usuario(agente_id, inicio_utc DESC);

CREATE TABLE IF NOT EXISTS periodos_atividade (
    id bigserial PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    ativo_id uuid NOT NULL REFERENCES ativos(id) ON DELETE RESTRICT,
    agente_id uuid NOT NULL REFERENCES agentes(id) ON DELETE RESTRICT,
    usuario_windows varchar(255) NOT NULL,
    estado smallint NOT NULL,
    inicio_utc timestamptz NOT NULL,
    fim_utc timestamptz NOT NULL,
    processo_executavel varchar(500),
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_periodos_atividade_intervalo CHECK (fim_utc >= inicio_utc),
    CONSTRAINT ck_periodos_atividade_estado CHECK (estado BETWEEN 1 AND 4)
);
CREATE INDEX IF NOT EXISTS ix_periodos_atividade_tenant_inicio ON periodos_atividade(tenant_id, inicio_utc DESC);
CREATE INDEX IF NOT EXISTS ix_periodos_atividade_agente_inicio ON periodos_atividade(agente_id, inicio_utc DESC);

CREATE TABLE IF NOT EXISTS atualizacoes_agente (
    id bigserial PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    agente_id uuid NOT NULL REFERENCES agentes(id) ON DELETE RESTRICT,
    versao_origem varchar(30) NOT NULL,
    versao_destino varchar(30) NOT NULL,
    status varchar(50) NOT NULL,
    detalhe text,
    momento_utc timestamptz NOT NULL,
    criado_em_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_atualizacoes_agente_agente_momento ON atualizacoes_agente(agente_id, momento_utc DESC);

CREATE TABLE IF NOT EXISTS auditoria (
    id bigserial PRIMARY KEY,
    tenant_id uuid REFERENCES tenants(id) ON DELETE RESTRICT,
    usuario_id uuid REFERENCES usuarios(id) ON DELETE SET NULL,
    agente_id uuid REFERENCES agentes(id) ON DELETE SET NULL,
    acao varchar(120) NOT NULL,
    entidade varchar(120),
    entidade_id varchar(120),
    detalhe jsonb,
    endereco_ip inet,
    criado_em_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_auditoria_tenant_criado ON auditoria(tenant_id, criado_em_utc DESC);
