-- ============================================================================
-- Share IT .Clara - Empresa Demonstração
-- Compatível com a estrutura até a migration 0004_multiempresa_perfis_acesso.sql
--
-- Login de demonstração:
--   E-mail: demonstracao@clara.local
--   Senha : demonstracao
--
-- IMPORTANTE: dados totalmente fictícios. Não usar esta conta em produção real.
-- O script é idempotente para o tenant identificador "demonstracao" e pode ser
-- executado novamente para renovar os dados e reposicioná-los em relação a NOW().
-- ============================================================================

DO $$
DECLARE
    v_tenant uuid := 'd0000000-0000-4000-8000-000000000001';
    v_empresa uuid := 'd0000000-0000-4000-8000-000000000002';
    v_unidade uuid := 'd0000000-0000-4000-8000-000000000003';
    v_dep_adm uuid := 'd0000000-0000-4000-8000-000000000011';
    v_dep_fin uuid := 'd0000000-0000-4000-8000-000000000012';
    v_dep_com uuid := 'd0000000-0000-4000-8000-000000000013';
    v_dep_ops uuid := 'd0000000-0000-4000-8000-000000000014';
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
    v_i integer;
    v_d integer;
    v_nomes text[] := ARRAY[
        'Ana Martins','Bruno Lima','Carla Souza','Diego Oliveira','Elisa Rocha',
        'Felipe Santos','Gabriela Alves','Henrique Costa','Isabela Melo','João Ribeiro',
        'Karen Freitas','Lucas Barros','Mariana Nunes','Nicolas Prado','Olivia Teixeira'
    ];
BEGIN
    -- Tenant e empresa principal fictícios.
    INSERT INTO tenants(id,nome,identificador,ativo)
    VALUES(v_tenant,'Horizonte Contabilidade - Demonstração','demonstracao',true)
    ON CONFLICT(id) DO UPDATE SET nome=EXCLUDED.nome,identificador=EXCLUDED.identificador,ativo=true;

    INSERT INTO empresas(id,tenant_id,nome,documento,ativo)
    VALUES(v_empresa,v_tenant,'Horizonte Contabilidade - Demonstração','00.000.000/0000-00',true)
    ON CONFLICT(id) DO UPDATE SET nome=EXCLUDED.nome,documento=EXCLUDED.documento,ativo=true;

    INSERT INTO unidades(id,tenant_id,empresa_id,nome,ativo)
    VALUES(v_unidade,v_tenant,v_empresa,'Escritório Central',true)
    ON CONFLICT(id) DO UPDATE SET nome=EXCLUDED.nome,ativo=true;

    INSERT INTO departamentos(id,tenant_id,empresa_id,nome,ativo) VALUES
      (v_dep_adm,v_tenant,v_empresa,'Administrativo',true),
      (v_dep_fin,v_tenant,v_empresa,'Financeiro',true),
      (v_dep_com,v_tenant,v_empresa,'Comercial',true),
      (v_dep_ops,v_tenant,v_empresa,'Operações',true)
    ON CONFLICT(id) DO UPDATE SET nome=EXCLUDED.nome,ativo=true;

    -- Perfis e módulos do tenant demo.
    INSERT INTO perfis_acesso(tenant_id,codigo,nome,padrao,sistema) VALUES
      (v_tenant,'ADMINISTRADOR','Administrador',false,true),
      (v_tenant,'PADRAO','Padrão',true,true)
    ON CONFLICT(tenant_id,codigo) DO UPDATE SET nome=EXCLUDED.nome,ativo=true;

    SELECT id INTO v_perfil_admin FROM perfis_acesso WHERE tenant_id=v_tenant AND codigo='ADMINISTRADOR';

    INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
    SELECT v_perfil_admin,codigo,true FROM modulos WHERE ativo=true
    ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();

    INSERT INTO perfil_modulos(perfil_id,modulo_codigo,permitido)
    SELECT p.id,m.codigo,true
    FROM perfis_acesso p JOIN modulos m ON m.codigo IN ('agora','estacoes','aplicativos','locais','integracoes','indice-geral')
    WHERE p.tenant_id=v_tenant AND p.codigo='PADRAO'
    ON CONFLICT(perfil_id,modulo_codigo) DO UPDATE SET permitido=true,atualizado_em_utc=now();

    -- Hash PBKDF2-SHA256 gerado com 210.000 iterações, salt 16 bytes,
    -- compatível com ShareIT.Clara.Comum.Seguranca.SenhaSegura.
    -- Senha em texto apenas para a demonstração: demonstracao
    INSERT INTO usuarios(
        id,tenant_id,empresa_id,nome,email,senha_hash,senha_sal,perfil,perfil_acesso_id,ativo)
    VALUES(
        'd0000000-0000-4000-8000-000000000020',v_tenant,v_empresa,
        'Usuário Demonstração','demonstracao@clara.local',
        'GxTm+VDX0s3pGwwhchlabIk5H69bymL5Vt5+YhD/9uQ=','ABEiM0RVZneImaq7zN3u/w==',
        'Administrador',v_perfil_admin,true)
    ON CONFLICT DO NOTHING;

    UPDATE usuarios SET
        tenant_id=v_tenant,empresa_id=v_empresa,nome='Usuário Demonstração',
        senha_hash='GxTm+VDX0s3pGwwhchlabIk5H69bymL5Vt5+YhD/9uQ=',
        senha_sal='ABEiM0RVZneImaq7zN3u/w==',perfil='Administrador',perfil_acesso_id=v_perfil_admin,ativo=true
    WHERE lower(email)='demonstracao@clara.local';

    -- Contexto padrão e regras de classificação.
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
    ON CONFLICT(tenant_id,provedor) DO UPDATE SET status=EXCLUDED.status,ultima_sincronizacao_utc=EXCLUDED.ultima_sincronizacao_utc,ultimo_erro=NULL,ativo=true;

    INSERT INTO regras_local_trabalho(tenant_id,nome,faixa_ip,tipo,ativo) VALUES
      (v_tenant,'Escritório Central','10.10.0.0/16','Escritorio',true),
      (v_tenant,'Home office demonstrativo','172.20.0.0/16','HomeOffice',true)
    ON CONFLICT(tenant_id,faixa_ip) DO UPDATE SET nome=EXCLUDED.nome,tipo=EXCLUDED.tipo,ativo=true;

    DELETE FROM regras_aplicativos WHERE tenant_id=v_tenant;
    INSERT INTO regras_aplicativos(tenant_id,processo_executavel,classificacao) VALUES
      (v_tenant,'EXCEL.EXE','Produtivo'),(v_tenant,'WINWORD.EXE','Produtivo'),
      (v_tenant,'ERPDEMO.EXE','Produtivo'),(v_tenant,'OUTLOOK.EXE','Produtivo'),
      (v_tenant,'TEAMS.EXE','Neutro'),(v_tenant,'CHROME.EXE','Neutro'),
      (v_tenant,'SPOTIFY.EXE','Atencao');

    DELETE FROM regras_sites WHERE tenant_id=v_tenant;
    INSERT INTO regras_sites(tenant_id,dominio,categoria,classificacao) VALUES
      (v_tenant,'erp.horizonte-demo.local','Sistema de gestão','Produtivo'),
      (v_tenant,'office.com','Microsoft 365','Produtivo'),
      (v_tenant,'docs.google.com','Documentos','Produtivo'),
      (v_tenant,'linkedin.com','Rede profissional','Neutro'),
      (v_tenant,'youtube.com','Vídeo','Atencao');

    -- Limpa somente a telemetria demo para gerar uma fotografia nova relativa à data atual.
    DELETE FROM participacoes_reuniao WHERE tenant_id=v_tenant;
    DELETE FROM eventos_agenda WHERE tenant_id=v_tenant;
    DELETE FROM metricas_email WHERE tenant_id=v_tenant;
    DELETE FROM sites_utilizados WHERE tenant_id=v_tenant;
    DELETE FROM atualizacoes_agente WHERE tenant_id=v_tenant;
    DELETE FROM periodos_atividade WHERE tenant_id=v_tenant;
    DELETE FROM sessoes_usuario WHERE tenant_id=v_tenant;
    DELETE FROM agentes WHERE tenant_id=v_tenant;
    DELETE FROM ativos WHERE tenant_id=v_tenant;

    -- 15 estações fictícias, com distribuição escritório/home office e alguns estados diferentes.
    FOR v_i IN 1..15 LOOP
        v_ativo := gen_random_uuid();
        v_agente := gen_random_uuid();
        v_responsavel := v_nomes[v_i];
        v_usuario_windows := lower(replace(split_part(v_responsavel,' ',1) || '.' || split_part(v_responsavel,' ',2),' ',''));
        v_departamento := CASE (v_i-1) % 4 WHEN 0 THEN v_dep_adm WHEN 1 THEN v_dep_fin WHEN 2 THEN v_dep_com ELSE v_dep_ops END;
        v_processo := CASE v_i % 6 WHEN 0 THEN 'TEAMS.EXE' WHEN 1 THEN 'EXCEL.EXE' WHEN 2 THEN 'ERPDEMO.EXE' WHEN 3 THEN 'OUTLOOK.EXE' WHEN 4 THEN 'CHROME.EXE' ELSE 'WINWORD.EXE' END;
        v_estado := CASE WHEN v_i IN (4,11) THEN 2 WHEN v_i=8 THEN 3 WHEN v_i IN (6,13) THEN 5 ELSE 1 END;

        INSERT INTO ativos(
            id,tenant_id,empresa_id,unidade_id,departamento_id,patrimonio,hostname,responsavel,tipo,
            numero_serie,uuid_equipamento,fabricante,modelo,sistema_operacional,arquitetura,
            memoria_total_bytes,disco_total_bytes,usuario_windows_detectado,endereco_ip_local,endereco_ip_publico,
            primeira_comunicacao_utc,cadastro_completo,atualizado_em_utc)
        VALUES(
            v_ativo,v_tenant,v_empresa,v_unidade,v_departamento,'PAT-'||lpad(v_i::text,4,'0'),
            'DEMO-PC-'||lpad(v_i::text,2,'0'),v_responsavel,'Computador',
            'DEMO-SN-'||lpad(v_i::text,5,'0'),gen_random_uuid()::text,
            CASE WHEN v_i%3=0 THEN 'Lenovo' WHEN v_i%3=1 THEN 'Dell' ELSE 'HP' END,
            CASE WHEN v_i%3=0 THEN 'ThinkCentre M70q' WHEN v_i%3=1 THEN 'OptiPlex 7010' ELSE 'ProDesk 400 G9' END,
            'Windows 11 Pro','x64',
            (CASE WHEN v_i%4=0 THEN 32 ELSE 16 END)::bigint*1024*1024*1024,
            (CASE WHEN v_i%5=0 THEN 1024 ELSE 512 END)::bigint*1024*1024*1024,
            v_usuario_windows,'192.168.1.'||(20+v_i)::text,
            (CASE WHEN v_i<=9 THEN '10.10.1.'||(30+v_i)::text ELSE '172.20.1.'||(30+v_i)::text END)::inet,
            now()-interval '45 days',true,now());

        INSERT INTO agentes(
            id,tenant_id,ativo_id,identificador_agente,token_hash,versao,hostname,usuario_windows,
            sistema_operacional,fabricante,modelo,ultima_comunicacao_utc,status,ativo,registrado_em_utc,atualizado_em_utc,
            numero_serie,uuid_equipamento,arquitetura,memoria_total_bytes,disco_total_bytes,endereco_ip_local,endereco_ip_publico,processo_atual)
        SELECT v_agente,v_tenant,v_ativo,gen_random_uuid(),encode(digest(gen_random_uuid()::text,'sha256'),'hex'),'0.3.2',
               a.hostname,v_usuario_windows,a.sistema_operacional,a.fabricante,a.modelo,
               CASE WHEN v_i<=12 THEN now()-(v_i||' seconds')::interval ELSE now()-interval '2 hours' END,
               CASE WHEN v_i<=12 THEN 'Online' ELSE 'Offline' END,true,now()-interval '45 days',now(),
               a.numero_serie,a.uuid_equipamento,a.arquitetura,a.memoria_total_bytes,a.disco_total_bytes,a.endereco_ip_local,a.endereco_ip_publico,v_processo
        FROM ativos a WHERE a.id=v_ativo;

        INSERT INTO sessoes_usuario(tenant_id,ativo_id,agente_id,usuario_windows,inicio_utc)
        VALUES(v_tenant,v_ativo,v_agente,v_usuario_windows,date_trunc('day',now())+interval '8 hours');

        -- Estado atual para a visão "Agora".
        INSERT INTO periodos_atividade(tenant_id,ativo_id,agente_id,usuario_windows,estado,inicio_utc,fim_utc,processo_executavel)
        VALUES(v_tenant,v_ativo,v_agente,v_usuario_windows,v_estado,now()-interval '18 minutes',now(),v_processo);

        -- Histórico de 21 dias úteis para gráficos, índice e tendências.
        FOR v_d IN 0..28 LOOP
            v_dia := current_date - v_d;
            IF extract(isodow from v_dia) <= 5 THEN
                v_inicio := v_dia::timestamptz + interval '8 hours';
                INSERT INTO periodos_atividade(tenant_id,ativo_id,agente_id,usuario_windows,estado,inicio_utc,fim_utc,processo_executavel) VALUES
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio,v_inicio+interval '1 hour 40 minutes','OUTLOOK.EXE'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '1 hour 40 minutes',v_inicio+interval '1 hour 52 minutes',NULL),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '1 hour 52 minutes',v_inicio+interval '4 hours','ERPDEMO.EXE'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,5,v_inicio+interval '5 hours',v_inicio+interval '5 hours 40 minutes','TEAMS.EXE'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '5 hours 40 minutes',v_inicio+interval '7 hours 20 minutes',CASE WHEN v_i%2=0 THEN 'EXCEL.EXE' ELSE 'WINWORD.EXE' END),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,2,v_inicio+interval '7 hours 20 minutes',v_inicio+interval '7 hours 30 minutes',NULL),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,1,v_inicio+interval '7 hours 30 minutes',v_inicio+interval '8 hours 15 minutes','CHROME.EXE');

                INSERT INTO sites_utilizados(tenant_id,ativo_id,agente_id,usuario_windows,dominio,navegador,inicio_utc,fim_utc) VALUES
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,'erp.horizonte-demo.local','Edge',v_inicio+interval '2 hours',v_inicio+interval '2 hours 45 minutes'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,'office.com','Edge',v_inicio+interval '6 hours',v_inicio+interval '6 hours 30 minutes'),
                  (v_tenant,v_ativo,v_agente,v_usuario_windows,CASE WHEN v_i%5=0 THEN 'youtube.com' ELSE 'docs.google.com' END,'Chrome',v_inicio+interval '7 hours 35 minutes',v_inicio+interval '7 hours 55 minutes');
            END IF;
        END LOOP;
    END LOOP;

    -- Agenda/reuniões fictícias e métricas agregadas de e-mail dos últimos 20 dias.
    FOR v_d IN 0..20 LOOP
        v_dia := current_date-v_d;
        IF extract(isodow from v_dia) <= 5 THEN
            INSERT INTO eventos_agenda(tenant_id,provedor,identificador_externo,usuario_externo,inicio_utc,fim_utc,tipo,possui_reuniao_online,provedor_reuniao)
            VALUES(v_tenant,'Microsoft','demo-reuniao-'||v_d::text,'demonstracao@clara.local',v_dia::timestamptz+interval '13 hours',v_dia::timestamptz+interval '13 hours 45 minutes','Reunião de equipe',true,'Teams')
            ON CONFLICT DO NOTHING;

            INSERT INTO participacoes_reuniao(tenant_id,provedor,identificador_reuniao_externo,usuario_externo,entrada_utc,saida_utc,duracao_segundos)
            VALUES(v_tenant,'Microsoft','demo-reuniao-'||v_d::text,'demonstracao@clara.local',v_dia::timestamptz+interval '13 hours 2 minutes',v_dia::timestamptz+interval '13 hours 43 minutes',2460);

            INSERT INTO metricas_email(tenant_id,provedor,usuario_externo,data_referencia,recebidos,enviados,tempo_preparacao_segundos,tempo_uso_segundos,tempo_resposta_medio_segundos,fora_expediente)
            VALUES(v_tenant,'Microsoft','demonstracao@clara.local',v_dia,38+(v_d%9),14+(v_d%5),3900+(v_d*30),6200+(v_d*45),3100+(v_d*20),CASE WHEN v_d%5=0 THEN 2 ELSE 0 END)
            ON CONFLICT(tenant_id,provedor,usuario_externo,data_referencia) DO UPDATE SET
              recebidos=EXCLUDED.recebidos,enviados=EXCLUDED.enviados,tempo_preparacao_segundos=EXCLUDED.tempo_preparacao_segundos,
              tempo_uso_segundos=EXCLUDED.tempo_uso_segundos,tempo_resposta_medio_segundos=EXCLUDED.tempo_resposta_medio_segundos,
              fora_expediente=EXCLUDED.fora_expediente,atualizado_em_utc=now();
        END IF;
    END LOOP;

    RAISE NOTICE 'Empresa demonstração atualizada. Login: demonstracao@clara.local / senha: demonstracao';
END $$;
