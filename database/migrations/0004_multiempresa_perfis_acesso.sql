-- Share IT .Clara v0.3.0
-- Gestão multiempresa, usuário Master, perfis de acesso e módulos.

CREATE TABLE IF NOT EXISTS modulos (
    codigo varchar(80) PRIMARY KEY,
    nome varchar(150) NOT NULL,
    grupo varchar(80) NOT NULL,
    rota varchar(250),
    ordem integer NOT NULL DEFAULT 0,
    ativo boolean NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS perfis_acesso (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    codigo varchar(80) NOT NULL,
    nome varchar(150) NOT NULL,
    padrao boolean NOT NULL DEFAULT false,
    sistema boolean NOT NULL DEFAULT false,
    ativo boolean NOT NULL DEFAULT true,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_perfis_acesso_tenant_codigo UNIQUE(tenant_id, codigo)
);
CREATE INDEX IF NOT EXISTS ix_perfis_acesso_tenant ON perfis_acesso(tenant_id, ativo);

CREATE TABLE IF NOT EXISTS perfil_modulos (
    perfil_id uuid NOT NULL REFERENCES perfis_acesso(id) ON DELETE CASCADE,
    modulo_codigo varchar(80) NOT NULL REFERENCES modulos(codigo) ON DELETE CASCADE,
    permitido boolean NOT NULL DEFAULT true,
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY(perfil_id, modulo_codigo)
);

ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS empresa_id uuid REFERENCES empresas(id) ON DELETE SET NULL;
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS perfil_acesso_id uuid REFERENCES perfis_acesso(id) ON DELETE SET NULL;

-- O login atual é feito somente pelo e-mail; portanto o e-mail precisa ser único na plataforma.
CREATE UNIQUE INDEX IF NOT EXISTS uq_usuarios_email_global ON usuarios(lower(email));
CREATE INDEX IF NOT EXISTS ix_usuarios_tenant_empresa ON usuarios(tenant_id, empresa_id, ativo);

INSERT INTO modulos(codigo,nome,grupo,rota,ordem) VALUES
    ('agora','Agora','Visão','/',10),
    ('estacoes','Estações','Visão','/estacoes',20),
    ('aplicativos','Aplicativos','Visão','/aplicativos',30),
    ('locais','Home x Escritório','Contexto','/locais',40),
    ('integracoes','Integrações','Contexto','/integracoes',50),
    ('indice-geral','Índice Geral','Inteligência','/indice-geral',60),
    ('usuarios','Usuários','Administração','/administracao/usuarios',80),
    ('perfis','Perfis de acesso','Administração','/administracao/perfis',90)
ON CONFLICT(codigo) DO UPDATE SET
    nome=EXCLUDED.nome,
    grupo=EXCLUDED.grupo,
    rota=EXCLUDED.rota,
    ordem=EXCLUDED.ordem,
    ativo=true;

-- Garante perfis básicos para todos os tenants existentes.
INSERT INTO perfis_acesso(tenant_id,codigo,nome,padrao,sistema)
SELECT t.id,'ADMINISTRADOR','Administrador',false,true
FROM tenants t
ON CONFLICT(tenant_id,codigo) DO NOTHING;

INSERT INTO perfis_acesso(tenant_id,codigo,nome,padrao,sistema)
SELECT t.id,'PADRAO','Padrão',true,true
FROM tenants t
ON CONFLICT(tenant_id,codigo) DO NOTHING;

-- Administrador da empresa possui acesso a todos os módulos do tenant.
INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
SELECT p.id,m.codigo,true
FROM perfis_acesso p
CROSS JOIN modulos m
WHERE p.codigo='ADMINISTRADOR' AND p.ativo=true AND m.ativo=true
ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true, atualizado_em_utc=now();

-- Perfil padrão começa somente com módulos operacionais/analíticos, sem administração de usuários/perfis.
INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
SELECT p.id,m.codigo,true
FROM perfis_acesso p
JOIN modulos m ON m.codigo IN ('agora','estacoes','aplicativos','locais','integracoes','indice-geral')
WHERE p.codigo='PADRAO' AND p.ativo=true
ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true, atualizado_em_utc=now();

-- Vincula usuários legados aos perfis correspondentes.
UPDATE usuarios u
SET perfil_acesso_id=p.id
FROM perfis_acesso p
WHERE p.tenant_id=u.tenant_id
  AND p.codigo=CASE WHEN u.perfil='Administrador' THEN 'ADMINISTRADOR' ELSE 'PADRAO' END
  AND u.perfil_acesso_id IS NULL;
