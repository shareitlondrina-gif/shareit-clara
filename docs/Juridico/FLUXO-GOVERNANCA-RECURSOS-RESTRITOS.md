# Fluxo de Governança — recursos restritos da Clara

## Nível 1 — telemetria comportamental

Aplicativos, duração, atividade/ociosidade, heartbeat, domínio categorizado, jornada e histórico.

**Liberação:** configuração administrativa + política da empresa.

## Nível 2 — evidência visual

Screenshots.

**Liberação proposta:** documento/política vigente + ciência aplicável + autorização jurídica vigente + retenção + perfis de acesso.

## Nível 3 — visualização/gravação

Live view somente leitura e gravação de tela.

**Liberação proposta:** todos os requisitos do Nível 2 + escopo/finalidade explícitos + auditoria de acesso + validade determinada.

## Nível 4 — áudio/webcam

Tratados como excepcionalíssimos.

**Regra:** autorização documental registrada não libera automaticamente o recurso. A capacidade técnica deve permanecer bloqueada até homologação específica e análise jurídica/proporcionalidade do caso de uso.

## Fluxo sistêmico

1. Administrador registra a política/documento;
2. Clara calcula e guarda SHA-256;
3. Administrador solicita autorização para recurso e escopo;
4. Clara envia link por e-mail ao Jurídico/Encarregado;
5. Jurídico aprova ou rejeita e informa nome/cargo;
6. Clara registra decisão, data, validade e e-mail destinatário;
7. autorização vencida deixa o recurso em estado bloqueado;
8. ciência dos trabalhadores pode ser registrada vinculada à política vigente;
9. futuras operações de acesso ao recurso deverão gerar auditoria.

## Observação da v0.5.7

A v0.5.7 implementa a **fundação de governança**. Screenshots reais, live view, gravação, áudio e webcam não são tecnicamente habilitados por esse fluxo nesta release.
