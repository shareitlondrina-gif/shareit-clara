-- Share IT .Clara v0.5.0
-- Distribuição do agente, coleta autorizada de domínio via navegador e controles comerciais SaaS.
-- IMPORTANTE: migrations 0001..0005 permanecem imutáveis.

CREATE TABLE IF NOT EXISTS assinaturas_tenant (
    tenant_id uuid PRIMARY KEY REFERENCES tenants(id) ON DELETE RESTRICT,
    plano varchar(80) NOT NULL DEFAULT 'Gestao',
    status varchar(30) NOT NULL DEFAULT 'Ativa',
    limite_estacoes integer NOT NULL DEFAULT 15,
    retencao_dias integer NOT NULL DEFAULT 90,
    armazenamento_gb integer NOT NULL DEFAULT 5,
    vencimento date,
    observacao text,
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_assinaturas_status CHECK (status IN ('Ativa','Trial','Suspensa','Cancelada')),
    CONSTRAINT ck_assinaturas_limite CHECK (limite_estacoes BETWEEN 1 AND 100000),
    CONSTRAINT ck_assinaturas_retencao CHECK (retencao_dias BETWEEN 1 AND 3650),
    CONSTRAINT ck_assinaturas_armazenamento CHECK (armazenamento_gb BETWEEN 1 AND 100000)
);

INSERT INTO assinaturas_tenant(tenant_id,plano,status,limite_estacoes,retencao_dias,armazenamento_gb)
SELECT t.id,
       CASE WHEN t.identificador='shareit' THEN 'Interno'
            WHEN t.identificador='demonstracao' THEN 'Demonstração'
            ELSE 'Gestão' END,
       CASE WHEN t.identificador='demonstracao' THEN 'Trial' ELSE 'Ativa' END,
       CASE WHEN t.identificador='shareit' THEN 100000
            WHEN t.identificador='demonstracao' THEN 15
            ELSE 15 END,
       90,
       CASE WHEN t.identificador='shareit' THEN 1000 ELSE 5 END
FROM tenants t
ON CONFLICT(tenant_id) DO NOTHING;

-- Melhora consultas de contexto web real sem alterar a estrutura publicada em 0003.
CREATE INDEX IF NOT EXISTS ix_sites_utilizados_tenant_dominio_inicio
    ON sites_utilizados(tenant_id, lower(dominio), inicio_utc DESC);
CREATE INDEX IF NOT EXISTS ix_sites_utilizados_ativo_inicio
    ON sites_utilizados(ativo_id, inicio_utc DESC);

INSERT INTO modulos(codigo,nome,grupo,rota,ordem) VALUES
    ('instalacao-agente','Instalação do agente','Administração','/administracao/instalacao',95)
ON CONFLICT(codigo) DO UPDATE SET
    nome=EXCLUDED.nome,
    grupo=EXCLUDED.grupo,
    rota=EXCLUDED.rota,
    ordem=EXCLUDED.ordem,
    ativo=true;

INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
SELECT p.id,'instalacao-agente',true
FROM perfis_acesso p
WHERE p.codigo='ADMINISTRADOR' AND p.ativo=true
ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();
