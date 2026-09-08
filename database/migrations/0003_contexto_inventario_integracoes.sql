-- Share IT .Clara v0.2.0
-- Evolui onboarding automático, inventário, contexto, classificação e fundação das integrações.

ALTER TABLE tokens_instalacao ADD COLUMN IF NOT EXISTS empresa_id uuid REFERENCES empresas(id) ON DELETE RESTRICT;
UPDATE tokens_instalacao t
SET empresa_id = a.empresa_id
FROM ativos a
WHERE t.ativo_id = a.id AND t.empresa_id IS NULL;
ALTER TABLE tokens_instalacao ALTER COLUMN empresa_id SET NOT NULL;
ALTER TABLE tokens_instalacao ALTER COLUMN ativo_id DROP NOT NULL;
CREATE INDEX IF NOT EXISTS ix_tokens_instalacao_tenant_empresa ON tokens_instalacao(tenant_id, empresa_id, expira_em_utc DESC);

ALTER TABLE ativos ADD COLUMN IF NOT EXISTS numero_serie varchar(200);
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS uuid_equipamento varchar(200);
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS fabricante varchar(200);
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS modelo varchar(200);
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS sistema_operacional varchar(255);
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS arquitetura varchar(80);
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS memoria_total_bytes bigint;
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS disco_total_bytes bigint;
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS usuario_windows_detectado varchar(255);
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS endereco_ip_local varchar(80);
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS endereco_ip_publico inet;
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS primeira_comunicacao_utc timestamptz;
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS atualizado_em_utc timestamptz NOT NULL DEFAULT now();
ALTER TABLE ativos ADD COLUMN IF NOT EXISTS cadastro_completo boolean NOT NULL DEFAULT false;
CREATE UNIQUE INDEX IF NOT EXISTS uq_ativos_tenant_numero_serie
    ON ativos(tenant_id, lower(numero_serie))
    WHERE numero_serie IS NOT NULL AND btrim(numero_serie) <> '';
CREATE INDEX IF NOT EXISTS ix_ativos_tenant_cadastro ON ativos(tenant_id, cadastro_completo, ativo);

ALTER TABLE agentes ADD COLUMN IF NOT EXISTS numero_serie varchar(200);
ALTER TABLE agentes ADD COLUMN IF NOT EXISTS uuid_equipamento varchar(200);
ALTER TABLE agentes ADD COLUMN IF NOT EXISTS arquitetura varchar(80);
ALTER TABLE agentes ADD COLUMN IF NOT EXISTS memoria_total_bytes bigint;
ALTER TABLE agentes ADD COLUMN IF NOT EXISTS disco_total_bytes bigint;
ALTER TABLE agentes ADD COLUMN IF NOT EXISTS endereco_ip_local varchar(80);
ALTER TABLE agentes ADD COLUMN IF NOT EXISTS endereco_ip_publico inet;
ALTER TABLE agentes ADD COLUMN IF NOT EXISTS processo_atual varchar(500);

ALTER TABLE periodos_atividade DROP CONSTRAINT IF EXISTS ck_periodos_atividade_estado;
ALTER TABLE periodos_atividade ADD CONSTRAINT ck_periodos_atividade_estado CHECK (estado BETWEEN 1 AND 5);

CREATE TABLE IF NOT EXISTS categorias_atividade (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    nome varchar(120) NOT NULL,
    classificacao varchar(30) NOT NULL DEFAULT 'Neutro',
    ativo boolean NOT NULL DEFAULT true,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_categorias_atividade_tenant_nome UNIQUE(tenant_id, nome),
    CONSTRAINT ck_categorias_atividade_classificacao CHECK (classificacao IN ('Produtivo','Neutro','Atencao'))
);

CREATE TABLE IF NOT EXISTS regras_aplicativos (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    departamento_id uuid REFERENCES departamentos(id) ON DELETE CASCADE,
    processo_executavel varchar(500) NOT NULL,
    categoria_id uuid REFERENCES categorias_atividade(id) ON DELETE SET NULL,
    classificacao varchar(30) NOT NULL DEFAULT 'Neutro',
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_regras_aplicativos_classificacao CHECK (classificacao IN ('Produtivo','Neutro','Atencao'))
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_regras_aplicativos_tenant_departamento_processo
    ON regras_aplicativos(tenant_id, COALESCE(departamento_id, '00000000-0000-0000-0000-000000000000'::uuid), lower(processo_executavel));

CREATE TABLE IF NOT EXISTS sites_utilizados (
    id bigserial PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    ativo_id uuid NOT NULL REFERENCES ativos(id) ON DELETE RESTRICT,
    agente_id uuid NOT NULL REFERENCES agentes(id) ON DELETE RESTRICT,
    usuario_windows varchar(255) NOT NULL,
    dominio varchar(500) NOT NULL,
    navegador varchar(120),
    inicio_utc timestamptz NOT NULL,
    fim_utc timestamptz NOT NULL,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_sites_utilizados_intervalo CHECK (fim_utc >= inicio_utc)
);
CREATE INDEX IF NOT EXISTS ix_sites_utilizados_tenant_inicio ON sites_utilizados(tenant_id, inicio_utc DESC);
CREATE INDEX IF NOT EXISTS ix_sites_utilizados_agente_inicio ON sites_utilizados(agente_id, inicio_utc DESC);

CREATE TABLE IF NOT EXISTS regras_sites (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    departamento_id uuid REFERENCES departamentos(id) ON DELETE CASCADE,
    dominio varchar(500) NOT NULL,
    categoria varchar(120),
    classificacao varchar(30) NOT NULL DEFAULT 'Neutro',
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_regras_sites_classificacao CHECK (classificacao IN ('Produtivo','Neutro','Atencao'))
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_regras_sites_tenant_departamento_dominio
    ON regras_sites(tenant_id, COALESCE(departamento_id, '00000000-0000-0000-0000-000000000000'::uuid), lower(dominio));

CREATE TABLE IF NOT EXISTS regras_local_trabalho (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    nome varchar(150) NOT NULL,
    faixa_ip cidr NOT NULL,
    tipo varchar(30) NOT NULL DEFAULT 'Escritorio',
    ativo boolean NOT NULL DEFAULT true,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_regras_local_tipo CHECK (tipo IN ('Escritorio','HomeOffice','Externo','Outro')),
    CONSTRAINT uq_regras_local_tenant_faixa UNIQUE(tenant_id, faixa_ip)
);

CREATE TABLE IF NOT EXISTS integracoes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    provedor varchar(40) NOT NULL,
    nome_exibicao varchar(120) NOT NULL,
    status varchar(40) NOT NULL DEFAULT 'NaoConfigurada',
    ultima_sincronizacao_utc timestamptz,
    ultimo_erro text,
    configuracao_publica jsonb,
    ativo boolean NOT NULL DEFAULT true,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_integracoes_provedor CHECK (provedor IN ('Microsoft','Google','Zoom')),
    CONSTRAINT uq_integracoes_tenant_provedor UNIQUE(tenant_id, provedor)
);

CREATE TABLE IF NOT EXISTS eventos_agenda (
    id bigserial PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    provedor varchar(40) NOT NULL,
    identificador_externo varchar(500) NOT NULL,
    usuario_externo varchar(320),
    inicio_utc timestamptz NOT NULL,
    fim_utc timestamptz NOT NULL,
    tipo varchar(80),
    possui_reuniao_online boolean NOT NULL DEFAULT false,
    provedor_reuniao varchar(40),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_eventos_agenda_tenant_provedor_externo UNIQUE(tenant_id, provedor, identificador_externo)
);
CREATE INDEX IF NOT EXISTS ix_eventos_agenda_tenant_inicio ON eventos_agenda(tenant_id, inicio_utc DESC);

CREATE TABLE IF NOT EXISTS participacoes_reuniao (
    id bigserial PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    provedor varchar(40) NOT NULL,
    identificador_reuniao_externo varchar(500) NOT NULL,
    usuario_externo varchar(320),
    entrada_utc timestamptz,
    saida_utc timestamptz,
    duracao_segundos integer,
    atualizado_em_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_participacoes_reuniao_tenant_usuario ON participacoes_reuniao(tenant_id, usuario_externo, entrada_utc DESC);

CREATE TABLE IF NOT EXISTS metricas_email (
    id bigserial PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    provedor varchar(40) NOT NULL,
    usuario_externo varchar(320) NOT NULL,
    data_referencia date NOT NULL,
    recebidos integer NOT NULL DEFAULT 0,
    enviados integer NOT NULL DEFAULT 0,
    tempo_preparacao_segundos bigint NOT NULL DEFAULT 0,
    tempo_uso_segundos bigint NOT NULL DEFAULT 0,
    tempo_resposta_medio_segundos bigint,
    fora_expediente integer NOT NULL DEFAULT 0,
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_metricas_email_tenant_provedor_usuario_data UNIQUE(tenant_id, provedor, usuario_externo, data_referencia)
);

INSERT INTO categorias_atividade(tenant_id,nome,classificacao)
SELECT t.id, x.nome, x.classificacao
FROM tenants t
CROSS JOIN (VALUES
    ('Trabalho','Produtivo'),
    ('Comunicacao','Neutro'),
    ('Desenvolvimento','Produtivo'),
    ('Administrativo','Produtivo'),
    ('Navegacao','Neutro'),
    ('Entretenimento','Atencao'),
    ('Nao classificado','Neutro')
) AS x(nome,classificacao)
ON CONFLICT DO NOTHING;

INSERT INTO integracoes(tenant_id,provedor,nome_exibicao,status)
SELECT t.id, x.provedor, x.nome, 'NaoConfigurada'
FROM tenants t
CROSS JOIN (VALUES
    ('Microsoft','Microsoft 365 / Outlook / Teams'),
    ('Google','Google Workspace / Gmail / Meet'),
    ('Zoom','Zoom')
) AS x(provedor,nome)
ON CONFLICT DO NOTHING;
