-- Share IT .Clara v0.5.8-RC
-- Fechamento técnico/comercial: timezone no agente, retenção automática, relatórios agendados,
-- filtros salvos, alertas de aderência à jornada, exclusões de privacidade, integração opcional com .VISÃO
-- e fundação do Pulso da Operação.
-- IMPORTANTE: migrations 0001..0008 permanecem imutáveis.

ALTER TABLE configuracoes_monitoramento ADD COLUMN IF NOT EXISTS retencao_telemetria_dias integer NOT NULL DEFAULT 180;
ALTER TABLE configuracoes_monitoramento ADD COLUMN IF NOT EXISTS retencao_notificacoes_dias integer NOT NULL DEFAULT 180;
ALTER TABLE configuracoes_monitoramento ADD COLUMN IF NOT EXISTS retencao_emails_dias integer NOT NULL DEFAULT 90;
ALTER TABLE configuracoes_monitoramento ADD COLUMN IF NOT EXISTS retencao_auditoria_dias integer NOT NULL DEFAULT 365;
ALTER TABLE configuracoes_monitoramento DROP CONSTRAINT IF EXISTS ck_config_retencao_telemetria;
ALTER TABLE configuracoes_monitoramento ADD CONSTRAINT ck_config_retencao_telemetria CHECK (retencao_telemetria_dias BETWEEN 7 AND 3650);
ALTER TABLE configuracoes_monitoramento DROP CONSTRAINT IF EXISTS ck_config_retencao_notificacoes;
ALTER TABLE configuracoes_monitoramento ADD CONSTRAINT ck_config_retencao_notificacoes CHECK (retencao_notificacoes_dias BETWEEN 7 AND 3650);
ALTER TABLE configuracoes_monitoramento DROP CONSTRAINT IF EXISTS ck_config_retencao_emails;
ALTER TABLE configuracoes_monitoramento ADD CONSTRAINT ck_config_retencao_emails CHECK (retencao_emails_dias BETWEEN 7 AND 3650);
ALTER TABLE configuracoes_monitoramento DROP CONSTRAINT IF EXISTS ck_config_retencao_auditoria;
ALTER TABLE configuracoes_monitoramento ADD CONSTRAINT ck_config_retencao_auditoria CHECK (retencao_auditoria_dias BETWEEN 30 AND 3650);

-- Mais fatos observáveis da jornada. Permanecem parâmetros gerenciais; não são ponto eletrônico.
ALTER TABLE regras_alerta DROP CONSTRAINT IF EXISTS ck_regras_alerta_tipo;
ALTER TABLE regras_alerta ADD CONSTRAINT ck_regras_alerta_tipo CHECK (tipo IN (
    'UsoNaoProdutivo','Ociosidade','ProdutivoForaJornada','EstacaoOffline',
    'InicioTardio','SaidaAntecipada','IntervaloLongo','AusenciaAtividade','CargaAbaixo','CargaAcima'
));

-- Exclusões explícitas para privacidade/minimização. A regra mais específica do departamento prevalece.
CREATE TABLE IF NOT EXISTS exclusoes_monitoramento (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    departamento_id uuid REFERENCES departamentos(id) ON DELETE CASCADE,
    tipo varchar(30) NOT NULL,
    valor varchar(500) NOT NULL,
    motivo varchar(300),
    ativo boolean NOT NULL DEFAULT true,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_exclusoes_monitoramento_tipo CHECK (tipo IN ('Aplicativo','Dominio'))
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_exclusoes_monitoramento
    ON exclusoes_monitoramento(tenant_id,COALESCE(departamento_id,'00000000-0000-0000-0000-000000000000'::uuid),tipo,lower(valor));

-- Relatórios salvos/agendados. O envio usa a mesma fila de e-mail da Clara.
CREATE TABLE IF NOT EXISTS filtros_salvos_relatorio (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    usuario_id uuid REFERENCES usuarios(id) ON DELETE CASCADE,
    nome varchar(180) NOT NULL,
    tipo_relatorio varchar(80) NOT NULL DEFAULT 'JornadaAtividade',
    parametros jsonb NOT NULL DEFAULT '{}'::jsonb,
    compartilhado boolean NOT NULL DEFAULT false,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_filtros_salvos_relatorio_tenant ON filtros_salvos_relatorio(tenant_id,tipo_relatorio);

CREATE TABLE IF NOT EXISTS relatorios_agendados (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    criado_por_usuario_id uuid REFERENCES usuarios(id) ON DELETE SET NULL,
    nome varchar(180) NOT NULL,
    tipo_relatorio varchar(80) NOT NULL DEFAULT 'ResumoExecutivo',
    periodicidade varchar(30) NOT NULL DEFAULT 'Semanal',
    hora_local time NOT NULL DEFAULT '08:00',
    dia_semana integer,
    dia_mes integer,
    fuso_horario varchar(100) NOT NULL DEFAULT 'America/Sao_Paulo',
    destinatarios text NOT NULL,
    parametros jsonb NOT NULL DEFAULT '{}'::jsonb,
    ativo boolean NOT NULL DEFAULT true,
    ultima_execucao_utc timestamptz,
    proxima_execucao_utc timestamptz,
    ultimo_status varchar(40),
    ultimo_erro text,
    criado_em_utc timestamptz NOT NULL DEFAULT now(),
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_relatorios_agendados_periodicidade CHECK (periodicidade IN ('Diario','Semanal','Mensal')),
    CONSTRAINT ck_relatorios_agendados_dia_semana CHECK (dia_semana IS NULL OR dia_semana BETWEEN 1 AND 7),
    CONSTRAINT ck_relatorios_agendados_dia_mes CHECK (dia_mes IS NULL OR dia_mes BETWEEN 1 AND 28)
);
CREATE INDEX IF NOT EXISTS ix_relatorios_agendados_execucao ON relatorios_agendados(ativo,proxima_execucao_utc);

-- Integração opcional entre produtos Share IT. A Clara funciona sem .VISÃO.
CREATE TABLE IF NOT EXISTS integracoes_produtos_shareit (
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    produto varchar(40) NOT NULL,
    contratado boolean NOT NULL DEFAULT false,
    integracao_ativa boolean NOT NULL DEFAULT false,
    url_base varchar(500),
    token_hash char(64),
    ultimo_sucesso_utc timestamptz,
    ultima_falha text,
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY(tenant_id,produto),
    CONSTRAINT ck_integracoes_produtos_shareit_produto CHECK (produto IN ('Visao'))
);

CREATE TABLE IF NOT EXISTS contexto_tecnico_visao (
    id bigserial PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    ativo_id uuid REFERENCES ativos(id) ON DELETE SET NULL,
    hostname varchar(255),
    categoria varchar(100) NOT NULL,
    severidade varchar(20) NOT NULL DEFAULT 'Informativa',
    resumo text NOT NULL,
    detalhe text,
    referencia_externa varchar(180),
    observado_em_utc timestamptz NOT NULL,
    recebido_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_contexto_tecnico_visao_severidade CHECK (severidade IN ('Informativa','Atencao','Alta','Critica'))
);
CREATE INDEX IF NOT EXISTS ix_contexto_tecnico_visao_tenant_data ON contexto_tecnico_visao(tenant_id,observado_em_utc DESC);
CREATE INDEX IF NOT EXISTS ix_contexto_tecnico_visao_ativo_data ON contexto_tecnico_visao(ativo_id,observado_em_utc DESC);

-- Gestão executiva: referências e custos informados pelo cliente. A Clara não presume valor financeiro.
CREATE TABLE IF NOT EXISTS referencias_operacionais (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    departamento_id uuid REFERENCES departamentos(id) ON DELETE CASCADE,
    nome varchar(180) NOT NULL,
    metrica varchar(80) NOT NULL,
    valor_referencia numeric(12,2) NOT NULL,
    unidade varchar(40) NOT NULL,
    ativo boolean NOT NULL DEFAULT true,
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_referencias_operacionais_metrica CHECK (metrica IN ('FocoPercentual','OciosidadePercentual','ProdutivoMinutos','UsoSistemaPrincipalMinutos','CargaUtilizacaoPercentual'))
);
CREATE INDEX IF NOT EXISTS ix_referencias_operacionais_tenant ON referencias_operacionais(tenant_id,ativo);

CREATE TABLE IF NOT EXISTS custos_software (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    processo_ou_produto varchar(300) NOT NULL,
    quantidade_licencas integer NOT NULL DEFAULT 1,
    custo_mensal_unitario numeric(14,2) NOT NULL DEFAULT 0,
    observacao text,
    ativo boolean NOT NULL DEFAULT true,
    atualizado_em_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_custos_software_quantidade CHECK (quantidade_licencas BETWEEN 1 AND 1000000),
    CONSTRAINT ck_custos_software_valor CHECK (custo_mensal_unitario >= 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_custos_software_tenant_produto ON custos_software(tenant_id,lower(processo_ou_produto));

-- Auditoria da rotina de retenção.
CREATE TABLE IF NOT EXISTS execucoes_retencao (
    id bigserial PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    inicio_utc timestamptz NOT NULL DEFAULT now(),
    fim_utc timestamptz,
    registros_excluidos bigint NOT NULL DEFAULT 0,
    detalhe jsonb NOT NULL DEFAULT '{}'::jsonb,
    status varchar(30) NOT NULL DEFAULT 'Executando',
    CONSTRAINT ck_execucoes_retencao_status CHECK (status IN ('Executando','Concluida','Falha'))
);

-- Novos módulos gerenciais. Contexto TI só será exibido na interface quando .VISÃO estiver contratado/ativo.
INSERT INTO modulos(codigo,nome,grupo,rota,ordem) VALUES
    ('pulso-operacao','Pulso da Operação','Inteligência','/pulso-operacao',55),
    ('contexto-ti','Contexto TI (.VISÃO)','Contexto','/contexto-ti',58)
ON CONFLICT(codigo) DO UPDATE SET nome=EXCLUDED.nome,grupo=EXCLUDED.grupo,rota=EXCLUDED.rota,ordem=EXCLUDED.ordem,ativo=true;

INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
SELECT p.id,m.codigo,true FROM perfis_acesso p JOIN modulos m ON m.codigo IN ('pulso-operacao','contexto-ti')
WHERE p.codigo='ADMINISTRADOR' AND p.ativo=true
ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();

INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
SELECT p.id,m.codigo,true FROM perfis_acesso p JOIN modulos m ON m.codigo IN ('pulso-operacao','contexto-ti')
WHERE p.codigo='SUPERVISOR' AND p.ativo=true
ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();

-- Regras de aderência vêm pausadas/ativas de forma conservadora e podem ser ajustadas pelo cliente.
INSERT INTO regras_alerta(tenant_id,nome,tipo,limite_minutos,severidade,canal_plataforma,canal_email,gerar_evidencia,gerar_oportunidade,ativo)
SELECT t.id,x.nome,x.tipo,x.limite,x.severidade,true,false,true,x.oportunidade,x.ativo
FROM tenants t
CROSS JOIN (VALUES
    ('Início de atividade após a margem prevista','InicioTardio',30,'Informativa',false,true),
    ('Encerramento de atividade antes da margem prevista','SaidaAntecipada',30,'Informativa',false,true),
    ('Intervalo sem atividade acima da referência','IntervaloLongo',60,'Atencao',false,true),
    ('Sem atividade observada no dia monitorado','AusenciaAtividade',60,'Atencao',false,true),
    ('Carga observada abaixo da referência diária','CargaAbaixo',60,'Informativa',false,false),
    ('Carga observada acima da referência diária','CargaAcima',60,'Atencao',true,true)
) AS x(nome,tipo,limite,severidade,oportunidade,ativo)
WHERE NOT EXISTS (SELECT 1 FROM regras_alerta r WHERE r.tenant_id=t.id AND r.tipo=x.tipo AND r.escopo_tipo='Todos');
