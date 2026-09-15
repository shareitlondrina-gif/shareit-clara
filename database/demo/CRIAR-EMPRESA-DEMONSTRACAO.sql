-- ============================================================================
-- Share IT .Clara v0.5.8 - Empresa Demonstração
-- Requer migrations até 0009_fechamento_operacional_coexistencia_gestao.sql.
--
-- Login de demonstração:
--   E-mail: demonstracao@clara.local
--   Senha : demonstracao
--
-- Dados 100% fictícios. O script é idempotente para o tenant "demonstracao" e
-- renova a telemetria em relação a NOW() para apresentações dentro do sistema.
-- ============================================================================

DO $$
DECLARE
    v_tenant uuid := 'd0000000-0000-4000-8000-000000000001';
    v_empresa uuid := 'd0000000-0000-4000-8000-000000000002';
    v_unidade uuid := 'd0000000-0000-4000-8000-000000000003';
    v_dep_adm uuid := 'd0000000-0000-4000-8000-000000000011';
    v_dep_fin uuid := 'd0000000-0000-4000-8000-000000000012';
    v_dep_fiscal uuid := 'd0000000-0000-4000-8000-000000000013';
    v_dep_contabil uuid := 'd0000000-0000-4000-8000-000000000014';
    v_dep_com uuid := 'd0000000-0000-4000-8000-000000000015';
    v_perfil_admin uuid;
    v_ativo uuid;
    v_agente uuid;
    v_departamento uuid;
    v_dia date;
    v_inicio timestamptz;
    v_usuario_windows text;
    v_responsavel text;
    v_processo text;
    v_estado smallint;
    v_ip_publico inet;
    v_i integer;
    v_d integer;
    v_nomes text[] := ARRAY[
        'Ana Martins','Bruno Lima','Carla Souza','Diego Oliveira','Elisa Rocha',
        'Felipe Santos','Gabriela Alves','Henrique Costa','Isabela Melo','João Ribeiro',
        'Karen Freitas','Leandro Nunes','Mariana Lima','Paulo Silva','Renata Almeida'
    ];
BEGIN
    INSERT INTO tenants(id,nome,identificador,ativo)
    VALUES(v_tenant,'Horizonte Contabilidade - Demonstração','demonstracao',true)
    ON CONFLICT(id) DO UPDATE SET nome=EXCLUDED.nome,identificador=EXCLUDED.identificador,ativo=true;

    INSERT INTO assinaturas_tenant(tenant_id,plano,status,limite_estacoes,retencao_dias,armazenamento_gb,vencimento,observacao,atualizado_em_utc)
    VALUES(v_tenant,'Demonstração','Trial',15,90,5,current_date+90,'Tenant fictício para apresentações comerciais do Share IT .Clara.',now())
    ON CONFLICT(tenant_id) DO UPDATE SET
        plano='Demonstração',status='Trial',limite_estacoes=15,retencao_dias=90,armazenamento_gb=5,
        vencimento=current_date+90,observacao='Tenant fictício para apresentações comerciais do Share IT .Clara.',atualizado_em_utc=now();

    INSERT INTO empresas(id,tenant_id,nome,documento,ativo)
    VALUES(v_empresa,v_tenant,'Horizonte Contabilidade','12.345.678/0001-99',true)
    ON CONFLICT(id) DO UPDATE SET nome=EXCLUDED.nome,documento=EXCLUDED.documento,ativo=true;

    INSERT INTO unidades(id,tenant_id,empresa_id,nome,ativo)
    VALUES(v_unidade,v_tenant,v_empresa,'Escritório Central',true)
    ON CONFLICT(id) DO UPDATE SET nome=EXCLUDED.nome,ativo=true;

    INSERT INTO departamentos(id,tenant_id,empresa_id,nome,ativo) VALUES
      (v_dep_adm,v_tenant,v_empresa,'Administrativo',true),
      (v_dep_fin,v_tenant,v_empresa,'Financeiro',true),
      (v_dep_fiscal,v_tenant,v_empresa,'Fiscal',true),
      (v_dep_contabil,v_tenant,v_empresa,'Contábil',true),
      (v_dep_com,v_tenant,v_empresa,'Comercial',true)
    ON CONFLICT(id) DO UPDATE SET nome=EXCLUDED.nome,ativo=true;

    INSERT INTO perfis_acesso(tenant_id,codigo,nome,padrao,sistema) VALUES
      (v_tenant,'ADMINISTRADOR','Administrador',false,true),
      (v_tenant,'SUPERVISOR','Supervisor',false,true),
      (v_tenant,'PADRAO','Padrão',true,true)
    ON CONFLICT(tenant_id,codigo) DO UPDATE SET nome=EXCLUDED.nome,ativo=true;

    SELECT id INTO v_perfil_admin FROM perfis_acesso WHERE tenant_id=v_tenant AND codigo='ADMINISTRADOR';

    INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
    SELECT v_perfil_admin,codigo,true FROM modulos WHERE ativo=true
    ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();

    INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
    SELECT p.id,m.codigo,true
    FROM perfis_acesso p JOIN modulos m ON m.codigo IN
      ('agora','estacoes','aplicativos','locais','integracoes','indice-geral','oportunidades','evidencias','relatorios')
    WHERE p.tenant_id=v_tenant AND p.codigo IN ('PADRAO','SUPERVISOR')
    ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();

    -- Hash PBKDF2-SHA256 compatível com SenhaSegura. Senha demo: demonstracao.
    INSERT INTO usuarios(id,tenant_id,empresa_id,nome,email,senha_hash,senha_sal,perfil,perfil_acesso_id,ativo)
    VALUES(
        'd0000000-0000-4000-8000-000000000020',v_tenant,v_empresa,
        'Ana Martins','demonstracao@clara.local',
        'GxTm+VDX0s3pGwwhchlabIk5H69bymL5Vt5+YhD/9uQ=','ABEiM0RVZneImaq7zN3u/w==',
        'Administrador',v_perfil_admin,true)
    ON CONFLICT DO NOTHING;

    UPDATE usuarios SET tenant_id=v_tenant,empresa_id=v_empresa,nome='Ana Martins',
        senha_hash='GxTm+VDX0s3pGwwhchlabIk5H69bymL5Vt5+YhD/9uQ=',
        senha_sal='ABEiM0RVZneImaq7zN3u/w==',perfil='Administrador',perfil_acesso_id=v_perfil_admin,ativo=true
    WHERE lower(email)='demonstracao@clara.local';

    INSERT INTO configuracoes_monitoramento(
        tenant_id,permitir_classificacao_web,monitorar_urls_dominios,usar_categorias_web,
        tempo_minimo_ociosidade_minutos,baixa_continuidade_minutos,limiar_alternancia_aplicativos,
        redes_confiaveis_cidr,considerar_vpn_escritorio,classificar_ip_publico,captura_screenshots,
        intervalo_captura_minutos,retencao_evidencias_dias,anonimizacao_parcial,horario_inicio,horario_fim,
        dias_monitorados,intervalo_heartbeat_segundos,tempo_offline_minutos,alerta_estacao_offline,
        notificar_por_email,destinatarios_alertas,atualizado_em_utc,
        jornada_inicio,jornada_fim,carga_horaria_diaria_minutos,margem_antes_minutos,margem_depois_minutos,dias_jornada,fuso_horario)
    VALUES(v_tenant,true,true,true,3,5,10,'10.10.0.0/16, 192.168.50.0/24',true,true,false,
           5,90,true,'07:00','20:00','1,2,3,4,5',300,15,true,true,
           'ti@empresa.com, gestor@empresa.com',now(),
           '08:00','18:00',480,30,60,'1,2,3,4,5','America/Sao_Paulo')
    ON CONFLICT(tenant_id) DO UPDATE SET
        permitir_classificacao_web=EXCLUDED.permitir_classificacao_web,
        monitorar_urls_dominios=EXCLUDED.monitorar_urls_dominios,
        usar_categorias_web=EXCLUDED.usar_categorias_web,
        tempo_minimo_ociosidade_minutos=EXCLUDED.tempo_minimo_ociosidade_minutos,
        baixa_continuidade_minutos=EXCLUDED.baixa_continuidade_minutos,
        limiar_alternancia_aplicativos=EXCLUDED.limiar_alternancia_aplicativos,
        redes_confiaveis_cidr=EXCLUDED.redes_confiaveis_cidr,
        considerar_vpn_escritorio=EXCLUDED.considerar_vpn_escritorio,
        classificar_ip_publico=EXCLUDED.classificar_ip_publico,
        captura_screenshots=false,
        intervalo_captura_minutos=EXCLUDED.intervalo_captura_minutos,
        retencao_evidencias_dias=EXCLUDED.retencao_evidencias_dias,
        anonimizacao_parcial=EXCLUDED.anonimizacao_parcial,
        horario_inicio=EXCLUDED.horario_inicio,horario_fim=EXCLUDED.horario_fim,
        dias_monitorados=EXCLUDED.dias_monitorados,
        intervalo_heartbeat_segundos=EXCLUDED.intervalo_heartbeat_segundos,
        tempo_offline_minutos=EXCLUDED.tempo_offline_minutos,
        alerta_estacao_offline=EXCLUDED.alerta_estacao_offline,
        notificar_por_email=EXCLUDED.notificar_por_email,
        destinatarios_alertas=EXCLUDED.destinatarios_alertas,
        jornada_inicio=EXCLUDED.jornada_inicio,jornada_fim=EXCLUDED.jornada_fim,
        carga_horaria_diaria_minutos=EXCLUDED.carga_horaria_diaria_minutos,
        margem_antes_minutos=EXCLUDED.margem_antes_minutos,margem_depois_minutos=EXCLUDED.margem_depois_minutos,
        dias_jornada=EXCLUDED.dias_jornada,fuso_horario=EXCLUDED.fuso_horario,atualizado_em_utc=now();

    INSERT INTO categorias_atividade(tenant_id,nome,classificacao)
    SELECT v_tenant,x.nome,x.classificacao FROM (VALUES
      ('Trabalho','Produtivo'),('Comunicacao','Neutro'),('Administrativo','Produtivo'),
      ('Navegacao','Neutro'),('Entretenimento','Atencao'),('Nao classificado','Neutro')
    ) x(nome,classificacao)
    ON CONFLICT DO NOTHING;

    INSERT INTO integracoes(tenant_id,provedor,nome_exibicao,status,ultima_sincronizacao_utc) VALUES
      (v_tenant,'Microsoft','Microsoft 365 / Outlook / Teams','Conectada',now()-interval '4 minutes'),
      (v_tenant,'Google','Google Workspace / Gmail / Meet','Conectada',now()-interval '7 minutes'),
      (v_tenant,'Zoom','Zoom','Conectada',now()-interval '11 minutes')
    ON CONFLICT(tenant_id,provedor) DO UPDATE SET status=EXCLUDED.status,
      ultima_sincronizacao_utc=EXCLUDED.ultima_sincronizacao_utc,ultimo_erro=NULL,ativo=true;

    DELETE FROM regras_local_trabalho WHERE tenant_id=v_tenant;
    INSERT INTO regras_local_trabalho(tenant_id,nome,faixa_ip,tipo,ativo) VALUES
      (v_tenant,'Escritório Central','10.10.0.0/16','Escritorio',true),
      (v_tenant,'Home office demonstrativo','172.20.0.0/16','HomeOffice',true),
      (v_tenant,'VPN / Externo','200.100.50.0/24','Externo',true),
      (v_tenant,'Rede convidados','192.168.50.0/24','Outro',true);

    DELETE FROM regras_aplicativos WHERE tenant_id=v_tenant;
    INSERT INTO regras_aplicativos(tenant_id,processo_executavel,classificacao) VALUES
      (v_tenant,'EXCEL.EXE','Produtivo'),(v_tenant,'WINWORD.EXE','Produtivo'),
      (v_tenant,'ERPDEMO.EXE','Produtivo'),(v_tenant,'OUTLOOK.EXE','Produtivo'),
      (v_tenant,'POWERPNT.EXE','Produtivo'),(v_tenant,'TEAMS.EXE','Neutro'),
      (v_tenant,'CHROME.EXE','Neutro'),(v_tenant,'SPOTIFY.EXE','Atencao');

    DELETE FROM regras_sites WHERE tenant_id=v_tenant;
    INSERT INTO regras_sites(tenant_id,dominio,categoria,classificacao) VALUES
      (v_tenant,'erp.horizonte-demo.local','Sistema de gestão','Produtivo'),
      (v_tenant,'office.com','Microsoft 365','Produtivo'),
      (v_tenant,'docs.google.com','Documentos','Produtivo'),
      (v_tenant,'linkedin.com','Rede profissional','Neutro'),
      (v_tenant,'instagram.com','Rede social','Atencao'),
      (v_tenant,'youtube.com','Vídeo','Atencao');

    -- Limpa apenas dados do tenant fictício antes de renovar a fotografia demo.
    DELETE FROM evidencias_visuais WHERE tenant_id=v_tenant;
    DELETE FROM oportunidades WHERE tenant_id=v_tenant;
    DELETE FROM evidencias_contextuais WHERE tenant_id=v_tenant;
    DELETE FROM participacoes_reuniao WHERE tenant_id=v_tenant;
    DELETE FROM eventos_agenda WHERE tenant_id=v_tenant;
    DELETE FROM metricas_email WHERE tenant_id=v_tenant;
    DELETE FROM sites_utilizados WHERE tenant_id=v_tenant;
    DELETE FROM atualizacoes_agente WHERE tenant_id=v_tenant;
    DELETE FROM periodos_atividade WHERE tenant_id=v_tenant;
    DELETE FROM sessoes_usuario WHERE tenant_id=v_tenant;
    DELETE FROM agentes WHERE tenant_id=v_tenant;
    DELETE FROM ativos WHERE tenant_id=v_tenant;

    -- 15 estações: 12 online; destas 7 ativas, 2 em reunião e 3 ociosas; 3 offline.
    FOR v_i IN 1..15 LOOP
        v_ativo := gen_random_uuid();
        v_agente := gen_random_uuid();
        v_responsavel := v_nomes[v_i];
        v_usuario_windows := lower(split_part(v_responsavel,' ',1) || '.' || split_part(v_responsavel,' ',2));
        v_departamento := CASE (v_i-1) % 5
            WHEN 0 THEN v_dep_adm WHEN 1 THEN v_dep_fin WHEN 2 THEN v_dep_fiscal
            WHEN 3 THEN v_dep_contabil ELSE v_dep_com END;
        v_processo := CASE v_i
            WHEN 1 THEN 'EXCEL.EXE' WHEN 2 THEN 'TEAMS.EXE' WHEN 3 THEN 'ERPDEMO.EXE'
            WHEN 4 THEN NULL WHEN 5 THEN 'OUTLOOK.EXE' WHEN 6 THEN 'TEAMS.EXE'
            WHEN 7 THEN 'CHROME.EXE' WHEN 8 THEN NULL WHEN 9 THEN 'WINWORD.EXE'
            WHEN 10 THEN 'TEAMS.EXE' WHEN 11 THEN NULL WHEN 12 THEN 'ERPDEMO.EXE'
            WHEN 13 THEN 'POWERPNT.EXE' ELSE NULL END;
        v_estado := CASE
            WHEN v_i IN (2,10) THEN 5
            WHEN v_i IN (4,8,14) THEN 2
            ELSE 1 END;
        v_ip_publico := CASE
            WHEN v_i<=8 THEN ('10.10.1.'||(20+v_i)::text)::inet
            WHEN v_i<=12 THEN ('172.20.5.'||(20+v_i)::text)::inet
            WHEN v_i<=14 THEN ('200.100.50.'||(20+v_i)::text)::inet
            ELSE '203.0.113.15'::inet END;

        INSERT INTO ativos(
            id,tenant_id,empresa_id,unidade_id,departamento_id,patrimonio,hostname,responsavel,tipo,
            numero_serie,uuid_equipamento,fabricante,modelo,sistema_operacional,arquitetura,
            memoria_total_bytes,disco_total_bytes,usuario_windows_detectado,endereco_ip_local,endereco_ip_publico,
            primeira_comunicacao_utc,cadastro_completo,atualizado_em_utc)
        VALUES(
            v_ativo,v_tenant,v_empresa,v_unidade,v_departamento,'HC-'||lpad(v_i::text,4,'0'),
            'DEMO-PC-'||lpad(v_i::text,2,'0'),v_responsavel,'Computador',
            'DEMO-SN-'||lpad(v_i::text,5,'0'),gen_random_uuid()::text,
            CASE WHEN v_i%3=0 THEN 'Lenovo' WHEN v_i%3=1 THEN 'Dell' ELSE 'HP' END,
            CASE WHEN v_i%3=0 THEN 'ThinkCentre M70q' WHEN v_i%3=1 THEN 'OptiPlex 7010' ELSE 'ProDesk 400 G9' END,
            'Windows 11 Pro','x64',(CASE WHEN v_i%4=0 THEN 32 ELSE 16 END)::bigint*1024*1024*1024,
            (CASE WHEN v_i%5=0 THEN 1024 ELSE 512 END)::bigint*1024*1024*1024,
            v_usuario_windows,'192.168.1.'||(20+v_i)::text,v_ip_publico,
            now()-interval '45 days',true,now());

        INSERT INTO agentes(
            id,tenant_id,ativo_id,identificador_agente,token_hash,versao,hostname,usuario_windows,
            sistema_operacional,fabricante,modelo,ultima_comunicacao_utc,status,ativo,registrado_em_utc,atualizado_em_utc,
            numero_serie,uuid_equipamento,arquitetura,memoria_total_bytes,disco_total_bytes,endereco_ip_local,endereco_ip_publico,processo_atual)
        SELECT v_agente,v_tenant,v_ativo,gen_random_uuid(),encode(digest(gen_random_uuid()::text,'sha256'),'hex'),'0.5.8',
               a.hostname,v_usuario_windows,a.sistema_operacional,a.fabricante,a.modelo,
               CASE WHEN v_i IN (6,12,15) THEN now()-interval '2 hours'-(v_i||' minutes')::interval ELSE now()-(v_i||' seconds')::interval END,
               CASE WHEN v_i IN (6,12,15) THEN 'Offline' ELSE 'Online' END,true,now()-interval '45 days',now(),
               a.numero_serie,a.uuid_equipamento,a.arquitetura,a.memoria_total_bytes,a.disco_total_bytes,a.endereco_ip_local,a.endereco_ip_publico,v_processo
        FROM ativos a WHERE a.id=v_ativo;

        INSERT INTO sessoes_usuario(tenant_id,ativo_id,agente_id,usuario_windows,inicio_utc)
        VALUES(v_tenant,v_ativo,v_agente,v_usuario_windows,LEAST(date_trunc('day',now())+interval '8 hours', now()-interval '18 minutes'));

        -- Histórico de 29 dias úteis. Escritório tem propositalmente maior proporção
        -- de apps classificados como produtivos e menos ociosidade para o comparativo demo.
        FOR v_d IN 1..29 LOOP
            v_dia := current_date-v_d;
            IF extract(isodow from v_dia)<=5 THEN
                v_inicio := v_dia::timestamptz+interval '8 hours';
                IF v_i<=8 THEN
                    INSERT INTO periodos_atividade(tenant_id,ativo_id,agente_id,usuario_windows,estado,inicio_utc,fim_utc,processo_executavel) VALUES
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio,v_inicio+interval '1 hour 35 minutes','OUTLOOK.EXE'),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '1 hour 35 minutes',v_inicio+interval '3 hours 50 minutes','ERPDEMO.EXE'),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '3 hours 50 minutes',v_inicio+interval '4 hours 5 minutes',NULL),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,5,v_inicio+interval '5 hours',v_inicio+interval '5 hours 40 minutes','TEAMS.EXE'),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '5 hours 40 minutes',v_inicio+interval '7 hours 35 minutes',CASE WHEN v_i%2=0 THEN 'EXCEL.EXE' ELSE 'WINWORD.EXE' END),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '7 hours 35 minutes',v_inicio+interval '7 hours 48 minutes',NULL),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '7 hours 48 minutes',v_inicio+interval '8 hours 10 minutes','ERPDEMO.EXE');
                ELSIF v_i<=12 THEN
                    INSERT INTO periodos_atividade(tenant_id,ativo_id,agente_id,usuario_windows,estado,inicio_utc,fim_utc,processo_executavel) VALUES
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio,v_inicio+interval '1 hour 5 minutes','OUTLOOK.EXE'),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '1 hour 5 minutes',v_inicio+interval '1 hour 45 minutes',NULL),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '1 hour 45 minutes',v_inicio+interval '3 hours 30 minutes','ERPDEMO.EXE'),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '3 hours 30 minutes',v_inicio+interval '4 hours 5 minutes',NULL),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,5,v_inicio+interval '5 hours',v_inicio+interval '5 hours 45 minutes','TEAMS.EXE'),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '5 hours 45 minutes',v_inicio+interval '7 hours','CHROME.EXE'),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '7 hours',v_inicio+interval '7 hours 35 minutes',NULL);
                ELSE
                    INSERT INTO periodos_atividade(tenant_id,ativo_id,agente_id,usuario_windows,estado,inicio_utc,fim_utc,processo_executavel) VALUES
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio,v_inicio+interval '1 hour 20 minutes','OUTLOOK.EXE'),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '1 hour 20 minutes',v_inicio+interval '1 hour 50 minutes',NULL),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '1 hour 50 minutes',v_inicio+interval '4 hours 30 minutes','ERPDEMO.EXE'),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,5,v_inicio+interval '5 hours',v_inicio+interval '5 hours 35 minutes','TEAMS.EXE'),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '5 hours 35 minutes',v_inicio+interval '7 hours 15 minutes','EXCEL.EXE'),
                      (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '7 hours 15 minutes',v_inicio+interval '7 hours 45 minutes',NULL);
                END IF;

                INSERT INTO sites_utilizados(tenant_id,ativo_id,agente_id,usuario_windows,dominio,navegador,inicio_utc,fim_utc) VALUES
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,'erp.horizonte-demo.local','Edge',v_inicio+interval '2 hours',v_inicio+interval '2 hours 45 minutes'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,'office.com','Edge',v_inicio+interval '6 hours',v_inicio+interval '6 hours 25 minutes'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,
                   CASE WHEN v_i IN (2,6,10,14) THEN 'youtube.com' WHEN v_i IN (7,12) THEN 'instagram.com' ELSE 'docs.google.com' END,
                   'Chrome',v_inicio+interval '7 hours',v_inicio+interval '7 hours 27 minutes');
            END IF;
        END LOOP;

        -- Fotografia do dia corrente. Os blocos são ancorados em NOW(), nunca no futuro,
        -- para que a empresa Demonstração continue convincente independentemente do horário
        -- em que o script for executado. Em horários muito cedo, o histórico diário é
        -- deliberadamente omitido e permanece apenas o estado atual.
        v_inicio := CASE WHEN v_i IN (6,12,15) THEN now()-interval '9 hours 50 minutes'
                         ELSE now()-interval '7 hours 50 minutes' END;
        IF v_inicio::date=current_date THEN
            IF v_i<=8 THEN
                INSERT INTO periodos_atividade(tenant_id,ativo_id,agente_id,usuario_windows,estado,inicio_utc,fim_utc,processo_executavel) VALUES
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio,v_inicio+interval '1 hour 35 minutes','OUTLOOK.EXE'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '1 hour 35 minutes',v_inicio+interval '3 hours 50 minutes','ERPDEMO.EXE'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '3 hours 50 minutes',v_inicio+interval '4 hours 5 minutes',NULL),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,CASE WHEN v_i IN (2,10) THEN 5 ELSE 1 END,v_inicio+interval '4 hours 45 minutes',v_inicio+interval '6 hours 13 minutes',CASE WHEN v_i IN (2,10) THEN 'TEAMS.EXE' ELSE 'ERPDEMO.EXE' END),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '6 hours 13 minutes',v_inicio+interval '7 hours 15 minutes',CASE WHEN v_i%2=0 THEN 'EXCEL.EXE' ELSE 'WINWORD.EXE' END),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '7 hours 15 minutes',v_inicio+interval '7 hours 32 minutes',NULL);
            ELSIF v_i<=12 THEN
                INSERT INTO periodos_atividade(tenant_id,ativo_id,agente_id,usuario_windows,estado,inicio_utc,fim_utc,processo_executavel) VALUES
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio,v_inicio+interval '1 hour 5 minutes','OUTLOOK.EXE'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '1 hour 5 minutes',v_inicio+interval '1 hour 45 minutes',NULL),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '1 hour 45 minutes',v_inicio+interval '3 hours 30 minutes','ERPDEMO.EXE'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '3 hours 30 minutes',v_inicio+interval '4 hours 5 minutes',NULL),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,CASE WHEN v_i IN (2,10) THEN 5 ELSE 1 END,v_inicio+interval '4 hours 45 minutes',v_inicio+interval '6 hours 13 minutes',CASE WHEN v_i IN (2,10) THEN 'TEAMS.EXE' ELSE 'ERPDEMO.EXE' END),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '6 hours 13 minutes',v_inicio+interval '7 hours 15 minutes','CHROME.EXE'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '7 hours 15 minutes',v_inicio+interval '7 hours 32 minutes',NULL);
            ELSE
                INSERT INTO periodos_atividade(tenant_id,ativo_id,agente_id,usuario_windows,estado,inicio_utc,fim_utc,processo_executavel) VALUES
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio,v_inicio+interval '1 hour 20 minutes','OUTLOOK.EXE'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '1 hour 20 minutes',v_inicio+interval '1 hour 50 minutes',NULL),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '1 hour 50 minutes',v_inicio+interval '4 hours 30 minutes','ERPDEMO.EXE'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,5,v_inicio+interval '5 hours',v_inicio+interval '5 hours 35 minutes','TEAMS.EXE'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '5 hours 35 minutes',v_inicio+interval '7 hours 15 minutes','EXCEL.EXE'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '7 hours 15 minutes',v_inicio+interval '7 hours 32 minutes',NULL);
            END IF;

            INSERT INTO sites_utilizados(tenant_id,ativo_id,agente_id,usuario_windows,dominio,navegador,inicio_utc,fim_utc) VALUES
              (v_tenant,v_ativo,v_agente,v_usuario_windows,'erp.horizonte-demo.local','Edge',v_inicio+interval '2 hours',v_inicio+interval '2 hours 45 minutes'),
              (v_tenant,v_ativo,v_agente,v_usuario_windows,'office.com','Edge',v_inicio+interval '6 hours',v_inicio+interval '6 hours 25 minutes'),
              (v_tenant,v_ativo,v_agente,v_usuario_windows,
               CASE WHEN v_i IN (2,6,10,14) THEN 'youtube.com' WHEN v_i IN (7,12) THEN 'instagram.com' ELSE 'docs.google.com' END,
               'Chrome',v_inicio+interval '7 hours',v_inicio+interval '7 hours 27 minutes');
        END IF;

        -- Período final é inserido por último para representar o estado atual no dashboard.
        INSERT INTO periodos_atividade(tenant_id,ativo_id,agente_id,usuario_windows,estado,inicio_utc,fim_utc,processo_executavel)
        VALUES(v_tenant,v_ativo,v_agente,v_usuario_windows,v_estado,
               CASE WHEN v_i IN (6,12,15) THEN now()-interval '2 hours 18 minutes' ELSE now()-interval '18 minutes' END,
               CASE WHEN v_i IN (6,12,15) THEN now()-interval '2 hours' ELSE now() END,v_processo);
    END LOOP;

    -- Agenda/reuniões e métricas agregadas fictícias.
    FOR v_d IN 0..20 LOOP
        v_dia := current_date-v_d;
        IF extract(isodow from v_dia)<=5 THEN
            INSERT INTO eventos_agenda(tenant_id,provedor,identificador_externo,usuario_externo,inicio_utc,fim_utc,tipo,possui_reuniao_online,provedor_reuniao)
            VALUES(v_tenant,'Microsoft','demo-reuniao-'||v_d::text,'demonstracao@clara.local',
                   v_dia::timestamptz+interval '13 hours',v_dia::timestamptz+interval '13 hours 45 minutes',
                   'Reunião de equipe',true,'Teams')
            ON CONFLICT DO NOTHING;

            INSERT INTO participacoes_reuniao(tenant_id,provedor,identificador_reuniao_externo,usuario_externo,entrada_utc,saida_utc,duracao_segundos)
            VALUES(v_tenant,'Microsoft','demo-reuniao-'||v_d::text,'demonstracao@clara.local',
                   v_dia::timestamptz+interval '13 hours 2 minutes',v_dia::timestamptz+interval '13 hours 43 minutes',2460);

            INSERT INTO metricas_email(tenant_id,provedor,usuario_externo,data_referencia,recebidos,enviados,tempo_preparacao_segundos,tempo_uso_segundos,tempo_resposta_medio_segundos,fora_expediente)
            VALUES(v_tenant,'Microsoft','demonstracao@clara.local',v_dia,38+(v_d%9),14+(v_d%5),3900+(v_d*30),6200+(v_d*45),3100+(v_d*20),CASE WHEN v_d%5=0 THEN 2 ELSE 0 END)
            ON CONFLICT(tenant_id,provedor,usuario_externo,data_referencia) DO UPDATE SET
              recebidos=EXCLUDED.recebidos,enviados=EXCLUDED.enviados,
              tempo_preparacao_segundos=EXCLUDED.tempo_preparacao_segundos,
              tempo_uso_segundos=EXCLUDED.tempo_uso_segundos,
              tempo_resposta_medio_segundos=EXCLUDED.tempo_resposta_medio_segundos,
              fora_expediente=EXCLUDED.fora_expediente,atualizado_em_utc=now();
        END IF;
    END LOOP;

    -- Evidências: fatos observados. Oportunidades: interpretações/recomendações separadas.
    -- A carga abaixo é propositalmente rica para que a demonstração nunca apresente
    -- os blocos gerenciais vazios quando a telemetria fictícia estiver ativa.
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    SELECT v_tenant,a.id,'Ana Martins','Contabilidade','Atividade atual','Positiva','Ana está trabalhando no ERP','ERPDEMO.EXE em uso contínuo há 18 minutos.','DEMO-PC-01',now()-interval '3 minutes'
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-01';
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    SELECT v_tenant,a.id,'Ana Martins','Contabilidade','Aplicativo','Positiva','Ana concentrou 68% do tempo no ERP','Trabalhou 5h12min no sistema durante o período observado.','DEMO-PC-01',now()-interval '12 minutes'
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-01';
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    SELECT v_tenant,a.id,'Bruno Lima','Financeiro','Reunião','Informativa','Bruno está em reunião no Teams','Reunião de equipe correlacionada com agenda Microsoft 365.','DEMO-PC-02',now()-interval '18 minutes'
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-02';
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    SELECT v_tenant,a.id,'João Ribeiro','Financeiro','Ociosidade','Atencao','João ficou 1h42 ocioso em horário comercial','Sem atividade no computador durante parte relevante da jornada.','DEMO-PC-10',now()-interval '48 minutes'
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-10';
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    SELECT v_tenant,a.id,'Bruno Lima','Financeiro','Acesso não produtivo','Alta','Bruno acessou YouTube por 27 min','Acesso classificado como Atenção fora dos aplicativos de negócio.','DEMO-PC-02',now()-interval '1 hour 29 minutes'
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-02';
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    SELECT v_tenant,a.id,'Gabriela Alves','Financeiro','Acesso não produtivo','Alta','Gabriela usou redes sociais por 43 min','Instagram foi classificado como Atenção na política demonstrativa.','DEMO-PC-07',now()-interval '2 hours 5 minutes'
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-07';
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    VALUES(v_tenant,NULL,NULL,'Financeiro','Desempenho','Positiva','Financeiro teve desempenho maior no escritório','As estações do escritório apresentaram maior uso de aplicativos produtivos e menor ociosidade no período.','5 estações',now()-interval '2 hours 40 minutes');
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    SELECT v_tenant,a.id,'Carla Souza','Fiscal','Comportamento','Atencao','Carla teve 18 trocas de app em 1 hora','Alta alternância entre aplicativos pode indicar baixa continuidade.','DEMO-PC-03',now()-interval '3 hours 10 minutes'
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-03';
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    SELECT v_tenant,a.id,'Diego Oliveira','Contábil','Aplicativo','Positiva','Diego ficou 2h12 em Excel com foco alto','Atividade contínua sem trocas relevantes de aplicativo.','DEMO-PC-04',now()-interval '3 hours 42 minutes'
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-04';
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    SELECT v_tenant,a.id,'Elisa Rocha','Comercial','Uso do sistema principal','Atencao','Elisa usou o ERP por apenas 36 min','Uso abaixo do esperado para a rotina definida no cenário demonstrativo.','DEMO-PC-05',now()-interval '4 hours 5 minutes'
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-05';
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    SELECT v_tenant,a.id,'Karen Freitas','Administrativo','Ociosidade','Atencao','Karen acumulou 1h18 de ociosidade','Períodos de inatividade distribuídos durante a manhã.','DEMO-PC-11',now()-interval '4 hours 35 minutes'
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-11';
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    SELECT v_tenant,a.id,'Felipe Santos','Administrativo','Treinamento','Atencao','Felipe repetiu tarefas manuais fora do ERP','O padrão sugere oportunidade de capacitação e revisão de processo.','DEMO-PC-06',now()-interval '5 hours 10 minutes'
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-06';
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    SELECT v_tenant,a.id,'Mariana Lima','Fiscal','Uso do sistema principal','Atencao','Mariana ficou abaixo da média de uso do sistema fiscal','Uso do aplicativo principal ficou abaixo do padrão do setor.','DEMO-PC-13',now()-interval '5 hours 40 minutes'
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-13';
    INSERT INTO evidencias_contextuais(tenant_id,ativo_id,pessoa,equipe,categoria,gravidade,resumo,detalhe,fonte,observada_em_utc)
    VALUES(v_tenant,NULL,NULL,'Administrativo','Home x Escritório','Atencao','Home office apresentou mais ociosidade no período','Comparativo demonstrativo entre os ambientes indica oportunidade de acompanhamento.','Comparativo por local',now()-interval '6 hours');

    -- Evidências visuais 100% fictícias. Servem apenas para apresentação da UX de prints.
    -- A captura real continua dependente da política do tenant, RBAC, auditoria, retenção e storage.
    INSERT INTO evidencias_visuais(tenant_id,ativo_id,pessoa,tipo,titulo,descricao,arquivo_relativo,capturada_em_utc,demonstrativa)
    SELECT v_tenant,a.id,'Ana Martins','Screenshot','ERP em uso — Ana Martins','Captura fictícia para demonstrar a evidência visual do aplicativo principal.','/demo/evidencias/demo-erp.png',now()-interval '8 minutes',true
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-01';
    INSERT INTO evidencias_visuais(tenant_id,ativo_id,pessoa,tipo,titulo,descricao,arquivo_relativo,capturada_em_utc,demonstrativa)
    SELECT v_tenant,a.id,'Diego Oliveira','Screenshot','Planilha de fechamento — Diego Oliveira','Captura fictícia de uma sessão produtiva em planilha.','/demo/evidencias/demo-excel.png',now()-interval '42 minutes',true
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-04';
    INSERT INTO evidencias_visuais(tenant_id,ativo_id,pessoa,tipo,titulo,descricao,arquivo_relativo,capturada_em_utc,demonstrativa)
    SELECT v_tenant,a.id,'Elisa Rocha','Screenshot','E-mail operacional — Elisa Rocha','Captura fictícia de atividade no Outlook.','/demo/evidencias/demo-outlook.png',now()-interval '1 hour 12 minutes',true
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-05';
    INSERT INTO evidencias_visuais(tenant_id,ativo_id,pessoa,tipo,titulo,descricao,arquivo_relativo,capturada_em_utc,demonstrativa)
    SELECT v_tenant,a.id,'Bruno Lima','Screenshot','Reunião no Teams — Bruno Lima','Captura fictícia para contextualizar período de reunião.','/demo/evidencias/demo-teams.png',now()-interval '1 hour 38 minutes',true
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-02';
    INSERT INTO evidencias_visuais(tenant_id,ativo_id,pessoa,tipo,titulo,descricao,arquivo_relativo,capturada_em_utc,demonstrativa)
    SELECT v_tenant,a.id,'Isabela Melo','Screenshot','Documento operacional — Isabela Melo','Captura fictícia de preparação de documento.','/demo/evidencias/demo-word.png',now()-interval '2 hours 15 minutes',true
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-09';
    INSERT INTO evidencias_visuais(tenant_id,ativo_id,pessoa,tipo,titulo,descricao,arquivo_relativo,capturada_em_utc,demonstrativa)
    SELECT v_tenant,a.id,'Gabriela Alves','Screenshot','Navegação corporativa — Gabriela Alves','Captura fictícia de navegação em portal de documentos.','/demo/evidencias/demo-browser.png',now()-interval '2 hours 52 minutes',true
      FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-07';

    -- Oportunidades: volume suficiente para alimentar os cinco indicadores da tela e os pontos de atenção do Agora.
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'João Ribeiro','Financeiro','OciosidadeAlta','1h42 de tempo ocioso em horário comercial','Alto','Conversar sobre ajuste de rotina e redistribuição de atividades.','Pendente','Regra demonstrativa',now()-interval '50 minutes' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-10';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Karen Freitas','Administrativo','OciosidadeAlta','1h18 ociosa no período da manhã','Medio','Validar agenda e distribuição de demandas.','Pendente','Regra demonstrativa',now()-interval '1 hour 20 minutes' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-11';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Henrique Costa','Contábil','OciosidadeAlta','58 min de ociosidade acumulada no expediente','Medio','Revisar sequência das atividades e dependências do processo.','Pendente','Regra demonstrativa',now()-interval '1 hour 50 minutes' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-08';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Paulo Silva','Comercial','OciosidadeAlta','Períodos ociosos superiores ao padrão do setor','Medio','Acompanhar distribuição de carteira e agenda comercial.','EmAndamento','Regra demonstrativa',now()-interval '2 hours 15 minutes' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-14';

    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Bruno Lima','Financeiro','SitesNaoProdutivos','27 min em YouTube','Medio','Reforçar política de uso e orientar sobre foco nas atividades.','EmAndamento','Regra demonstrativa',now()-interval '2 hours' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-02';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Gabriela Alves','Financeiro','SitesNaoProdutivos','43 min em Instagram e redes sociais','Medio','Reforçar política de uso e conscientização sobre produtividade.','Pendente','Regra demonstrativa',now()-interval '2 hours 30 minutes' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-07';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'João Ribeiro','Financeiro','SitesNaoProdutivos','Uso recorrente de vídeo em horário comercial','Medio','Validar contexto e reforçar diretrizes de navegação.','Pendente','Regra demonstrativa',now()-interval '3 hours' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-10';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Mariana Lima','Fiscal','SitesNaoProdutivos','Acessos classificados como Atenção acima do padrão','Baixo','Revisar classificação e orientar uso quando necessário.','Pendente','Regra demonstrativa',now()-interval '3 hours 20 minutes' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-13';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Felipe Santos','Administrativo','SitesNaoProdutivos','Tempo relevante em navegação não relacionada ao ERP','Baixo','Orientar priorização e revisar atalhos/processo.','Pendente','Regra demonstrativa',now()-interval '3 hours 45 minutes' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-06';

    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Carla Souza','Fiscal','BaixaContinuidade','Alternou 18 vezes de aplicativo em 1h','Medio','Apoiar na organização da rotina e priorização de tarefas.','Pendente','Regra demonstrativa',now()-interval '3 hours' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-03';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Henrique Costa','Contábil','BaixaContinuidade','Muitas trocas entre navegador, planilha e e-mail','Medio','Organizar blocos de foco e reduzir interrupções.','Pendente','Regra demonstrativa',now()-interval '4 hours' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-08';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Isabela Melo','Administrativo','BaixaContinuidade','Sequência fragmentada entre Word, navegador e e-mail','Baixo','Organizar janelas de concentração e reduzir alternâncias.','Pendente','Regra demonstrativa',now()-interval '4 hours 20 minutes' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-09';

    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Renata Almeida','Financeiro','PossivelNecessidadeTreinamento','Dificuldade recorrente no Excel (erros em fórmulas)','Baixo','Treinamento em Excel intermediário e materiais de apoio.','Concluida','Regra demonstrativa',now()-interval '5 hours' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-15';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Felipe Santos','Administrativo','PossivelNecessidadeTreinamento','Baixo uso do ERP e repetição de tarefas manuais','Baixo','Avaliar treinamento e melhoria do processo.','Pendente','Regra demonstrativa',now()-interval '5 hours 30 minutes' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-06';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Elisa Rocha','Comercial','PossivelNecessidadeTreinamento','Uso reduzido de recursos esperados do ERP','Medio','Treinamento prático no ERP e acompanhamento inicial.','Pendente','Regra demonstrativa',now()-interval '6 hours' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-05';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Mariana Lima','Fiscal','PossivelNecessidadeTreinamento','Padrão de uso sugere dúvidas no sistema fiscal','Baixo','Validar necessidade de capacitação com o responsável.','Pendente','Regra demonstrativa',now()-interval '6 hours 20 minutes' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-13';

    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Elisa Rocha','Comercial','BaixoUsoSistemaPrincipal','Apenas 36 min de uso do ERP em um dia de 8h','Alto','Treinamento prático no ERP e acompanhamento inicial.','Pendente','Regra demonstrativa',now()-interval '4 hours' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-05';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Mariana Lima','Fiscal','BaixoUsoSistemaPrincipal','Uso abaixo da média do sistema fiscal principal','Baixo','Revisar fluxo e necessidade de capacitação.','Pendente','Regra demonstrativa',now()-interval '4 days' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-13';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Felipe Santos','Administrativo','BaixoUsoSistemaPrincipal','Baixo uso do sistema principal e alto volume manual','Medio','Revisar aderência ao processo e treinar recursos do sistema.','Pendente','Regra demonstrativa',now()-interval '6 hours 40 minutes' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-06';

    -- Oportunidades específicas do contexto de local para alimentar o Insight Principal e recomendações.
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    SELECT v_tenant,a.id,'Isabela Melo','Administrativo','HomeOffice','Home office com ociosidade acima do escritório','Medio','Revisar contexto, alinhar entregas e rotina no home office.','EmAndamento','Regra demonstrativa',now()-interval '5 hours' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-09';
    INSERT INTO oportunidades(tenant_id,ativo_id,pessoa,equipe,categoria,evidencia_resumida,impacto,recomendacao,status,origem,detectada_em_utc)
    VALUES(v_tenant,NULL,NULL,'Administrativo','HomeOffice','Média de ociosidade maior em dias de home office','Medio','Revisar diretrizes, definir metas claras e rotina de acompanhamento.','Pendente','Regra demonstrativa',now()-interval '1 day 2 hours');


    -- v0.5.8-RC: contexto executivo e integração opcional .VISÃO apenas para a empresa de demonstração.
    INSERT INTO integracoes_produtos_shareit(tenant_id,produto,contratado,integracao_ativa,url_base,ultimo_sucesso_utc,atualizado_em_utc)
    VALUES(v_tenant,'Visao',true,true,'https://visao.demo.local',now()-interval '9 minutes',now())
    ON CONFLICT(tenant_id,produto) DO UPDATE SET contratado=true,integracao_ativa=true,url_base=EXCLUDED.url_base,ultimo_sucesso_utc=EXCLUDED.ultimo_sucesso_utc,atualizado_em_utc=now();

    DELETE FROM contexto_tecnico_visao WHERE tenant_id=v_tenant;
    INSERT INTO contexto_tecnico_visao(tenant_id,ativo_id,hostname,categoria,severidade,resumo,detalhe,referencia_externa,observado_em_utc)
    SELECT v_tenant,a.id,a.hostname,'Performance','Atencao','Uso de memória acima da referência','Memória observada acima de 90% durante parte do expediente.','VIS-DEMO-001',now()-interval '48 minutes' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-03';
    INSERT INTO contexto_tecnico_visao(tenant_id,ativo_id,hostname,categoria,severidade,resumo,detalhe,referencia_externa,observado_em_utc)
    SELECT v_tenant,a.id,a.hostname,'Rede','Alta','Oscilação de conectividade observada','Perda de pacotes e latência acima da referência no mesmo período de queda operacional.','VIS-DEMO-002',now()-interval '2 hours' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-07';
    INSERT INTO contexto_tecnico_visao(tenant_id,ativo_id,hostname,categoria,severidade,resumo,detalhe,referencia_externa,observado_em_utc)
    SELECT v_tenant,a.id,a.hostname,'Sistema','Informativa','Estação tecnicamente saudável','Sem sinais técnicos relevantes no período analisado.','VIS-DEMO-003',now()-interval '3 hours' FROM ativos a WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-01';

    INSERT INTO custos_software(tenant_id,processo_ou_produto,quantidade_licencas,custo_mensal_unitario,observacao,ativo)
    VALUES(v_tenant,'ERPDEMO.EXE',15,129.90,'Licenças fictícias para demonstração de utilização/custo.',true)
    ON CONFLICT(tenant_id,(lower(processo_ou_produto))) DO UPDATE SET quantidade_licencas=15,custo_mensal_unitario=129.90,observacao=EXCLUDED.observacao,ativo=true,atualizado_em_utc=now();
    INSERT INTO custos_software(tenant_id,processo_ou_produto,quantidade_licencas,custo_mensal_unitario,observacao,ativo)
    VALUES(v_tenant,'POWERPNT.EXE',10,45.00,'Exemplo de software com possível subutilização.',true)
    ON CONFLICT(tenant_id,(lower(processo_ou_produto))) DO UPDATE SET quantidade_licencas=10,custo_mensal_unitario=45.00,observacao=EXCLUDED.observacao,ativo=true,atualizado_em_utc=now();

    DELETE FROM referencias_operacionais WHERE tenant_id=v_tenant;
    INSERT INTO referencias_operacionais(tenant_id,departamento_id,nome,metrica,valor_referencia,unidade,ativo) VALUES
      (v_tenant,NULL,'Foco operacional da empresa','FocoPercentual',75,'%',true),
      (v_tenant,v_dep_fin,'Ociosidade de referência - Financeiro','OciosidadePercentual',15,'%',true),
      (v_tenant,v_dep_fiscal,'Uso esperado do sistema principal - Fiscal','UsoSistemaPrincipalMinutos',240,'min/dia',true);

    -- Adoção de IA é medida apenas por ferramenta/domínio e duração; nenhum prompt/conteúdo é lido.
    INSERT INTO sites_utilizados(tenant_id,ativo_id,agente_id,usuario_windows,dominio,navegador,inicio_utc,fim_utc)
    SELECT v_tenant,a.id,g.id,'carla.souza','chatgpt.com','Edge',now()-interval '4 hours',now()-interval '3 hours 42 minutes'
    FROM ativos a JOIN agentes g ON g.ativo_id=a.id AND g.tenant_id=a.tenant_id
    WHERE a.tenant_id=v_tenant AND a.hostname='DEMO-PC-03'
    LIMIT 1;

    RAISE NOTICE 'Empresa demonstração atualizada. Login: demonstracao@clara.local / senha: demonstracao';
END $$;
