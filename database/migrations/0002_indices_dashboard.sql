CREATE INDEX IF NOT EXISTS ix_periodos_atividade_tenant_estado_inicio
    ON periodos_atividade(tenant_id, estado, inicio_utc DESC);

CREATE INDEX IF NOT EXISTS ix_agentes_tenant_status
    ON agentes(tenant_id, status, ativo);
