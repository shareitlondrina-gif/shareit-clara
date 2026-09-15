-- Share IT .Clara v0.5.7
-- Jornada de referência, período de monitoramento, histórico/relatórios, alertas,
-- notificações, preferência de tema, e fundação de Governança e Privacidade.
-- IMPORTANTE: migrations 0001..0007 permanecem imutáveis.

-- Preferência visual individual.
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS tema_preferido varchar(20) NOT NULL DEFAULT 'Sistema';
ALTER TABLE usuarios DROP CONSTRAINT IF EXISTS ck_usuarios_tema_preferido;
ALTER TABLE usuarios ADD CONSTRAINT ck_usuarios_tema_preferido CHECK (tema_preferido IN ('Claro','Escuro','Sistema'));

-- Diferencia jornada de referência da janela efetiva de coleta.
ALTER TABLE configuracoes_monitoramento ADD COLUMN IF NOT EXISTS jornada_inicio time NOT NULL DEFAULT '08:00';
ALTER TABLE configuracoes_monitoramento ADD COLUMN IF NOT EXISTS jornada_fim time NOT NULL DEFAULT '18:00';
ALTER TABLE configuracoes_monitoramento ADD COLUMN IF NOT EXISTS carga_horaria_diaria_minutos integer NOT NULL DEFAULT 480;
ALTER TABLE configuracoes_monitoramento ADD COLUMN IF NOT EXISTS margem_antes_minutos integer NOT NULL DEFAULT 30;
ALTER TABLE configuracoes_monitoramento ADD COLUMN IF NOT EXISTS margem_depois_minutos integer NOT NULL DEFAULT 60;
ALTER TABLE configuracoes_monitoramento ADD COLUMN IF NOT EXISTS dias_jornada varchar(40) NOT NULL DEFAULT '1,2,3,4,5';
ALTER TABLE configuracoes_monitoramento ADD COLUMN IF NOT EXISTS fuso_horario varchar(100) NOT NULL DEFAULT 'America/Sao_Paulo';

ALTER TABLE configuracoes_monitoramento DROP CONSTRAINT IF EXISTS ck_config_carga_horaria;
ALTER TABLE configuracoes_monitoramento ADD CONSTRAINT ck_config_carga_horaria CHECK (carga_horaria_diaria_minutos BETWEEN 1 AND 1440);
ALTER TABLE configuracoes_monitoramento DROP CONSTRAINT IF EXISTS ck_config_margem_antes;
ALTER TABLE configuracoes_monitoramento ADD CONSTRAINT ck_config_margem_antes CHECK (margem_antes_minutos BETWEEN 0 AND 720);
ALTER TABLE configuracoes_monitoramento DROP CONSTRAINT IF EXISTS ck_config_margem_depois;
ALTER TABLE configuracoes_monitoramento ADD CONSTRAINT ck_config_margem_depois CHECK (margem_depois_minutos BETWEEN 0 AND 720);

-- Compatibilidade: horario_inicio/horario_fim continuam sendo a janela efetiva de monitoramento
-- enviada ao agente. Jornada é somente referência analítica e de alertas.
UPDATE configuracoes_monitoramento
SET jornada_inicio = horario_inicio,
    jornada_fim = horario_fim,
    dias_jornada = dias_monitorados
WHERE true;

-- Sobrescrita por estação. Quando ausente, herda a configuração do tenant.
CREATE TABLE IF NOT EXISTS configuracoes_jornada_ativo (
    ativo_id uuid PRIMARY KEY REFERENCES ativos(id) ON DELETE CASCADE,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    jornada_inicio time NOT NULL,
    jornada_fim time NOT NULL,
    carga_horaria_diaria_minutos integer NOT NULL,
    margem_antes_minutos integer NOT NULL DEFAULT 0,
    margem_depois_minutos integer NOT NULL DEFAULT 0,
    dias_jornada varchar(40) NOT NULL DEFAULT '1,2,3,4,5',
    monitoramento_inicio time NOT NULL,
    monitoramento_fim time NOT NULL,
    dias_monitorados varchar(40) NOT NULL DEFAULT '1,2,3,4,5',
    fuso_horario varchar(100) NOT NULL DEFAULT 'America/Sao_Paulo',
    atualizado_por_usuario_id uuid REFERENCES usuarios(id) ON DELETE SET NULL,
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_jornada_ativo_carga CHECK (carga_horaria_diaria_minutos BETWEEN 1 AND 1440),
    CONSTRAINT ck_jornada_ativo_margem_antes CHECK (margem_antes_minutos BETWEEN 0 AND 720),
    CONSTRAINT ck_jornada_ativo_margem_depois CHECK (margem_depois_minutos BETWEEN 0 AND 720)
);
CREATE INDEX IF NOT EXISTS ix_configuracoes_jornada_ativo_tenant ON configuracoes_jornada_ativo(tenant_id);

-- Regras parametrizadas de alerta. O sistema registra fatos; interpretação permanece com o cliente.
CREATE TABLE IF NOT EXISTS regras_alerta (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    nome varchar(180) NOT NULL,
    tipo varchar(60) NOT NULL,
    limite_minutos integer NOT NULL DEFAULT 20,
    escopo_tipo varchar(30) NOT NULL DEFAULT 'Todos',
    departamento_id uuid REFERENCES departamentos(id) ON DELETE CASCADE,
    ativo_id uuid REFERENCES ativos(id) ON DELETE CASCADE,
    severidade varchar(20) NOT NULL DEFAULT 'Atencao',
    canal_plataforma boolean NOT NULL DEFAULT true,
    canal_email boolean NOT NULL DEFAULT false,
    destinatarios_email text,
    destinatarios_perfis varchar(200) NOT NULL DEFAULT 'Administrador,Supervisor',
    gerar_evidencia boolean NOT NULL DEFAULT true,
    gerar_oportunidade boolean NOT NULL DEFAULT false,
    ativo boolean NOT NULL DEFAULT true,
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_regras_alerta_tipo CHECK (tipo IN ('UsoNaoProdutivo','Ociosidade','ProdutivoForaJornada','EstacaoOffline')),
    CONSTRAINT ck_regras_alerta_limite CHECK (limite_minutos BETWEEN 1 AND 1440),
    CONSTRAINT ck_regras_alerta_escopo CHECK (escopo_tipo IN ('Todos','Departamento','Estacao')),
    CONSTRAINT ck_regras_alerta_severidade CHECK (severidade IN ('Informativa','Atencao','Alta','Critica'))
);
CREATE INDEX IF NOT EXISTS ix_regras_alerta_tenant ON regras_alerta(tenant_id, ativo, tipo);

-- Central de notificações clicável.
CREATE TABLE IF NOT EXISTS notificacoes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    usuario_id uuid REFERENCES usuarios(id) ON DELETE CASCADE,
    tipo varchar(80) NOT NULL,
    titulo varchar(220) NOT NULL,
    resumo text,
    severidade varchar(20) NOT NULL DEFAULT 'Informativa',
    url_destino text,
    destinatarios_perfis varchar(200),
    visivel_plataforma boolean NOT NULL DEFAULT true,
    fonte_tipo varchar(80),
    fonte_id varchar(120),
    chave_deduplicacao varchar(250),
    lida boolean NOT NULL DEFAULT false,
    criada_em_utc timestamptz NOT NULL DEFAULT now(),
    lida_em_utc timestamptz,
    CONSTRAINT ck_notificacoes_severidade CHECK (severidade IN ('Informativa','Atencao','Alta','Critica'))
);
CREATE INDEX IF NOT EXISTS ix_notificacoes_tenant_data ON notificacoes(tenant_id, criada_em_utc DESC);
CREATE INDEX IF NOT EXISTS ix_notificacoes_usuario_lida ON notificacoes(usuario_id, lida, criada_em_utc DESC);
CREATE UNIQUE INDEX IF NOT EXISTS uq_notificacoes_deduplicacao
    ON notificacoes(tenant_id, COALESCE(usuario_id,'00000000-0000-0000-0000-000000000000'::uuid), chave_deduplicacao)
    WHERE chave_deduplicacao IS NOT NULL;

-- Leitura é individual: uma notificação compartilhada por perfil não some para os demais usuários.
CREATE TABLE IF NOT EXISTS notificacoes_leitura (
    notificacao_id uuid NOT NULL REFERENCES notificacoes(id) ON DELETE CASCADE,
    usuario_id uuid NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
    lida_em_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY(notificacao_id,usuario_id)
);
CREATE INDEX IF NOT EXISTS ix_notificacoes_leitura_usuario ON notificacoes_leitura(usuario_id,lida_em_utc DESC);

-- Caixa de saída. O transporte SMTP/API é configurado via segredo no ambiente do Render.
CREATE TABLE IF NOT EXISTS fila_emails (
    id bigserial PRIMARY KEY,
    tenant_id uuid REFERENCES tenants(id) ON DELETE CASCADE,
    destinatario varchar(320) NOT NULL,
    assunto varchar(300) NOT NULL,
    corpo_texto text NOT NULL,
    corpo_html text,
    status varchar(30) NOT NULL DEFAULT 'Pendente',
    tentativas integer NOT NULL DEFAULT 0,
    ultimo_erro text,
    proxima_tentativa_utc timestamptz NOT NULL DEFAULT now(),
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    enviado_em_utc timestamptz,
    CONSTRAINT ck_fila_emails_status CHECK (status IN ('Pendente','Enviando','Enviado','Erro'))
);
CREATE INDEX IF NOT EXISTS ix_fila_emails_pendente ON fila_emails(status, proxima_tentativa_utc);

CREATE TABLE IF NOT EXISTS configuracoes_email_tenant (
    tenant_id uuid PRIMARY KEY REFERENCES tenants(id) ON DELETE CASCADE,
    remetente_nome varchar(180) NOT NULL DEFAULT 'Share IT .Clara',
    remetente_email varchar(320) NOT NULL DEFAULT 'monitoramento.clara@shareitbrasil.com.br',
    reply_to varchar(320),
    destinatarios_adicionais text,
    atualizado_em_utc timestamptz NOT NULL DEFAULT now()
);
INSERT INTO configuracoes_email_tenant(tenant_id)
SELECT id FROM tenants
ON CONFLICT(tenant_id) DO NOTHING;

-- Documentos e trilha de Governança e Privacidade.
CREATE TABLE IF NOT EXISTS documentos_governanca (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    tipo varchar(80) NOT NULL,
    titulo varchar(250) NOT NULL,
    versao varchar(40),
    nome_arquivo varchar(255) NOT NULL,
    mime_type varchar(120) NOT NULL DEFAULT 'application/pdf',
    tamanho_bytes bigint NOT NULL,
    sha256 char(64) NOT NULL,
    conteudo bytea NOT NULL,
    validade_ate date,
    status_validacao varchar(40) NOT NULL DEFAULT 'Recebido',
    criado_por_usuario_id uuid REFERENCES usuarios(id) ON DELETE SET NULL,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_documentos_governanca_tipo CHECK (tipo IN ('PoliticaMonitoramento','AutorizacaoJuridica','RIPD','ComprovanteCiencia','Outro')),
    CONSTRAINT ck_documentos_governanca_status CHECK (status_validacao IN ('Recebido','ValidadoExternamente','Rejeitado','Expirado'))
);
CREATE INDEX IF NOT EXISTS ix_documentos_governanca_tenant ON documentos_governanca(tenant_id, criado_em_utc DESC);

CREATE TABLE IF NOT EXISTS solicitacoes_aprovacao_juridica (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    documento_id uuid REFERENCES documentos_governanca(id) ON DELETE SET NULL,
    recurso varchar(80) NOT NULL,
    escopo_descricao text,
    email_juridico varchar(320) NOT NULL,
    token_hash char(64) NOT NULL UNIQUE,
    validade_solicitada_ate date NOT NULL,
    status varchar(30) NOT NULL DEFAULT 'Pendente',
    aprovador_nome varchar(200),
    aprovador_cargo varchar(160),
    decisao_em_utc timestamptz,
    criado_por_usuario_id uuid REFERENCES usuarios(id) ON DELETE SET NULL,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    expira_em_utc timestamptz NOT NULL,
    CONSTRAINT ck_solicitacoes_recurso CHECK (recurso IN ('Screenshot','VisualizacaoAoVivo','GravacaoTela','Audio','Webcam')),
    CONSTRAINT ck_solicitacoes_status CHECK (status IN ('Pendente','Aprovada','Rejeitada','Expirada'))
);
CREATE INDEX IF NOT EXISTS ix_solicitacoes_aprovacao_tenant ON solicitacoes_aprovacao_juridica(tenant_id, status, criado_em_utc DESC);

CREATE TABLE IF NOT EXISTS autorizacoes_governanca (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    solicitacao_id uuid NOT NULL REFERENCES solicitacoes_aprovacao_juridica(id) ON DELETE RESTRICT,
    recurso varchar(80) NOT NULL,
    escopo_descricao text,
    aprovada_em_utc timestamptz NOT NULL,
    validade_ate date NOT NULL,
    aprovador_email varchar(320) NOT NULL,
    aprovador_nome varchar(200),
    aprovador_cargo varchar(160),
    revogada_em_utc timestamptz,
    motivo_revogacao text,
    CONSTRAINT uq_autorizacao_solicitacao UNIQUE(solicitacao_id)
);
CREATE INDEX IF NOT EXISTS ix_autorizacoes_governanca_tenant ON autorizacoes_governanca(tenant_id, recurso, validade_ate DESC);

CREATE TABLE IF NOT EXISTS ciencias_politica (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    documento_id uuid NOT NULL REFERENCES documentos_governanca(id) ON DELETE RESTRICT,
    colaborador_nome varchar(200) NOT NULL,
    colaborador_email varchar(320),
    identificador_interno varchar(120),
    forma_ciencia varchar(60) NOT NULL DEFAULT 'RegistroAdministrativo',
    ciencia_em_utc timestamptz NOT NULL DEFAULT now(),
    registrado_por_usuario_id uuid REFERENCES usuarios(id) ON DELETE SET NULL,
    observacao text
);
CREATE INDEX IF NOT EXISTS ix_ciencias_politica_tenant ON ciencias_politica(tenant_id, ciencia_em_utc DESC);

-- Perfil Supervisor padrão para leitura operacional/gerencial e recebimento de alertas.
-- O cliente continua podendo criar perfis adicionais e ajustar permissões.
INSERT INTO perfis_acesso(tenant_id,codigo,nome,padrao,sistema)
SELECT t.id,'SUPERVISOR','Supervisor',false,true
FROM tenants t
ON CONFLICT(tenant_id,codigo) DO NOTHING;

-- Novos módulos.
INSERT INTO modulos(codigo,nome,grupo,rota,ordem) VALUES
    ('relatorios','Relatórios','Inteligência','/relatorios',75),
    ('alertas','Alertas e regras','Administração','/administracao/alertas',105),
    ('governanca','Governança e Privacidade','Administração','/administracao/governanca',110)
ON CONFLICT(codigo) DO UPDATE SET nome=EXCLUDED.nome,grupo=EXCLUDED.grupo,rota=EXCLUDED.rota,ordem=EXCLUDED.ordem,ativo=true;

INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
SELECT p.id,m.codigo,true
FROM perfis_acesso p
JOIN modulos m ON m.codigo IN ('relatorios','alertas','governanca')
WHERE p.codigo='ADMINISTRADOR' AND p.ativo=true
ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();

-- Supervisor recebe leitura operacional e inteligência, sem Administração.
INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
SELECT p.id,m.codigo,true
FROM perfis_acesso p
JOIN modulos m ON m.codigo IN ('agora','estacoes','aplicativos','locais','integracoes','indice-geral','oportunidades','evidencias','relatorios')
WHERE p.codigo='SUPERVISOR' AND p.ativo=true
ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();

INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
SELECT p.id,'relatorios',true
FROM perfis_acesso p
WHERE p.codigo='PADRAO' AND p.ativo=true
ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();

-- Regras iniciais: podem ser editadas pelo Administrador.
INSERT INTO regras_alerta(tenant_id,nome,tipo,limite_minutos,severidade,canal_plataforma,canal_email,gerar_evidencia,gerar_oportunidade)
SELECT t.id,x.nome,x.tipo,x.limite,x.severidade,true,false,true,x.oportunidade
FROM tenants t
CROSS JOIN (VALUES
    ('Uso não produtivo acima do limite','UsoNaoProdutivo',20,'Atencao',false),
    ('Ociosidade acima do limite','Ociosidade',20,'Atencao',true),
    ('Atividade produtiva fora da jornada','ProdutivoForaJornada',30,'Informativa',false),
    ('Estação sem comunicação','EstacaoOffline',15,'Atencao',false)
) AS x(nome,tipo,limite,severidade,oportunidade)
WHERE NOT EXISTS (
    SELECT 1 FROM regras_alerta r WHERE r.tenant_id=t.id AND r.tipo=x.tipo AND r.escopo_tipo='Todos'
);
