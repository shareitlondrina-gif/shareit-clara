# Coexistência Clara + Clara VISÃO — v0.5.8-RC

## Princípio
Os produtos são independentes. O cliente pode contratar somente a Clara. A existência do VISÃO nunca é requisito para coleta, histórico, alertas, relatórios ou gestão da Clara.

## Isolamento esperado
- Serviços Windows com nomes distintos;
- Pastas de instalação e ProgramData distintas;
- Identidade/tokens distintos;
- Bancos lógicos distintos (`clara` e `visao`);
- Atualizadores distintos.

## Integração opcional
O contexto técnico só é exibido quando:
1. o Master marca que o produto/serviço VISÃO foi contratado pelo tenant; e
2. a integração técnica é ativada.

Sem as duas condições, o menu `Contexto TI (VISÃO)` e o bloco correspondente no Pulso da Operação não aparecem.

## Proteção de atualização
A Clara v0.5.8-RC:
- usa `Global\Clara.ProductUpdate` durante a troca de arquivos;
- verifica sinais recentes de atualização do VISÃO antes de iniciar sua própria atualização;
- posterga em vez de disputar a máquina durante manutenção detectada.

A coordenação bilateral completa dependerá de o atualizador do VISÃO adotar a mesma trava global em versão compatível. Até lá, a proteção do lada Clara continua ativa.

## Homologação recomendada
Executar ambos por um dia útil e testar:
- login/logoff;
- uso normal do ERP/navegador/Office;
- ociosidade e reunião;
- suspensão/retorno;
- perda/retorno de rede;
- reinicialização;
- Windows Update;
- atualização automática da Clara;
- atualização do VISÃO;
- atualizações próximas entre si;
- consumo de CPU/RAM/disco/rede;
- antivírus/EDR.

Registrar qualquer impacto antes de promover a candidata para v1.0.
