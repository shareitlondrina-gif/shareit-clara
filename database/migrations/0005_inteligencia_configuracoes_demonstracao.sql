-- Share IT .Clara v0.4.0
-- Inteligência gerencial, configurações por tenant, políticas do agente e persistência de Data Protection.
-- IMPORTANTE: migrations 0001..0004 permanecem imutáveis.

CREATE TABLE IF NOT EXISTS configuracoes_monitoramento (
    tenant_id uuid PRIMARY KEY REFERENCES tenants(id) ON DELETE RESTRICT,
    permitir_classificacao_web boolean NOT NULL DEFAULT true,
    monitorar_urls_dominios boolean NOT NULL DEFAULT false,
    usar_categorias_web boolean NOT NULL DEFAULT true,
    tempo_minimo_ociosidade_minutos integer NOT NULL DEFAULT 5,
    baixa_continuidade_minutos integer NOT NULL DEFAULT 5,
    limiar_alternancia_aplicativos integer NOT NULL DEFAULT 10,
    redes_confiaveis_cidr text,
    considerar_vpn_escritorio boolean NOT NULL DEFAULT true,
    classificar_ip_publico boolean NOT NULL DEFAULT true,
    captura_screenshots boolean NOT NULL DEFAULT false,
    intervalo_captura_minutos integer NOT NULL DEFAULT 5,
    retencao_evidencias_dias integer NOT NULL DEFAULT 90,
    anonimizacao_parcial boolean NOT NULL DEFAULT true,
    horario_inicio time NOT NULL DEFAULT '08:00',
    horario_fim time NOT NULL DEFAULT '18:00',
    dias_monitorados varchar(40) NOT NULL DEFAULT '1,2,3,4,5',
    intervalo_heartbeat_segundos integer NOT NULL DEFAULT 300,
    tempo_offline_minutos integer NOT NULL DEFAULT 15,
    alerta_estacao_offline boolean NOT NULL DEFAULT true,
    notificar_por_email boolean NOT NULL DEFAULT false,
    destinatarios_alertas text,
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_config_ociosidade CHECK (tempo_minimo_ociosidade_minutos BETWEEN 1 AND 240),
    CONSTRAINT ck_config_continuidade CHECK (baixa_continuidade_minutos BETWEEN 1 AND 240),
    CONSTRAINT ck_config_alternancia CHECK (limiar_alternancia_aplicativos BETWEEN 1 AND 500),
    CONSTRAINT ck_config_intervalo_captura CHECK (intervalo_captura_minutos BETWEEN 1 AND 1440),
    CONSTRAINT ck_config_retencao CHECK (retencao_evidencias_dias BETWEEN 1 AND 3650),
    CONSTRAINT ck_config_heartbeat CHECK (intervalo_heartbeat_segundos BETWEEN 15 AND 3600),
    CONSTRAINT ck_config_offline CHECK (tempo_offline_minutos BETWEEN 1 AND 1440)
);

INSERT INTO configuracoes_monitoramento(tenant_id)
SELECT t.id FROM tenants t
ON CONFLICT(tenant_id) DO NOTHING;

CREATE TABLE IF NOT EXISTS oportunidades (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    ativo_id uuid REFERENCES ativos(id) ON DELETE SET NULL,
    pessoa varchar(200),
    equipe varchar(150),
    categoria varchar(100) NOT NULL,
    evidencia_resumida text NOT NULL,
    impacto varchar(20) NOT NULL DEFAULT 'Medio',
    recomendacao text NOT NULL,
    status varchar(30) NOT NULL DEFAULT 'Pendente',
    origem varchar(120) NOT NULL DEFAULT 'Regra',
    detectada_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizada_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_oportunidades_impacto CHECK (impacto IN ('Baixo','Medio','Alto')),
    CONSTRAINT ck_oportunidades_status CHECK (status IN ('Pendente','EmAndamento','Concluida'))
);
CREATE INDEX IF NOT EXISTS ix_oportunidades_tenant_data ON oportunidades(tenant_id, detectada_em_utc DESC);
CREATE INDEX IF NOT EXISTS ix_oportunidades_tenant_categoria ON oportunidades(tenant_id, categoria, status);
CREATE INDEX IF NOT EXISTS ix_oportunidades_ativo ON oportunidades(ativo_id, detectada_em_utc DESC);

CREATE TABLE IF NOT EXISTS evidencias_contextuais (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    ativo_id uuid REFERENCES ativos(id) ON DELETE SET NULL,
    pessoa varchar(200),
    equipe varchar(150),
    categoria varchar(100) NOT NULL,
    gravidade varchar(20) NOT NULL DEFAULT 'Informativa',
    resumo text NOT NULL,
    detalhe text,
    fonte varchar(150),
    observada_em_utc timestamptz NOT NULL DEFAULT now(),
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_evidencias_gravidade CHECK (gravidade IN ('Positiva','Informativa','Atencao','Alta'))
);
CREATE INDEX IF NOT EXISTS ix_evidencias_tenant_data ON evidencias_contextuais(tenant_id, observada_em_utc DESC);
CREATE INDEX IF NOT EXISTS ix_evidencias_tenant_categoria ON evidencias_contextuais(tenant_id, categoria, gravidade);
CREATE INDEX IF NOT EXISTS ix_evidencias_ativo ON evidencias_contextuais(ativo_id, observada_em_utc DESC);

-- Repositório persistente do key ring do ASP.NET Core Data Protection.
-- Não expor o XML em logs/telas; a proteção de acesso, backup e criptografia em repouso é responsabilidade da infraestrutura do PostgreSQL/Neon.
CREATE TABLE IF NOT EXISTS chaves_protecao_dados (
    id bigserial PRIMARY KEY,
    nome varchar(250),
    xml text NOT NULL,
    criado_em_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_chaves_protecao_dados_criado ON chaves_protecao_dados(criado_em_utc);

INSERT INTO modulos(codigo,nome,grupo,rota,ordem) VALUES
    ('oportunidades','Oportunidades','Inteligência','/oportunidades',65),
    ('evidencias','Evidências','Inteligência','/evidencias',70),
    ('configuracoes','Configurações','Administração','/administracao/configuracoes',100)
ON CONFLICT(codigo) DO UPDATE SET
    nome=EXCLUDED.nome,
    grupo=EXCLUDED.grupo,
    rota=EXCLUDED.rota,
    ordem=EXCLUDED.ordem,
    ativo=true;

-- Administradores recebem todos os módulos; perfil padrão recebe os módulos de leitura/inteligência.
INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
SELECT p.id,m.codigo,true
FROM perfis_acesso p
JOIN modulos m ON m.codigo IN ('oportunidades','evidencias','configuracoes')
WHERE p.codigo='ADMINISTRADOR' AND p.ativo=true
ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();

INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
SELECT p.id,m.codigo,true
FROM perfis_acesso p
JOIN modulos m ON m.codigo IN ('oportunidades','evidencias')
WHERE p.codigo='PADRAO' AND p.ativo=true
ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();
