# Anexo Técnico — Share IT .Clara

## 1. Objetivo

Descrever tecnicamente os recursos da Share IT .Clara que podem compor a Política de Monitoramento, Produtividade e Privacidade da empresa contratante.

## 2. Matriz de recursos

| Recurso | Situação-base | Governança |
|---|---|---|
| Heartbeat / disponibilidade | Habilitável | Configuração normal |
| Aplicativo/processo e duração | Habilitável | Configuração normal |
| Atividade / ociosidade / reunião | Habilitável | Configuração normal |
| Domínio/categoria web | Opcional | Conector + política do cliente |
| Conteúdo digitado / senhas | Não coletado pelo escopo padrão | Fora do escopo |
| Conteúdo de e-mail/mensagem | Não coletado pelo escopo padrão | Fora do escopo |
| Timeline e histórico | Habilitável | Configuração normal |
| Jornada de referência | Habilitável | Configuração normal |
| Período efetivo de monitoramento | Habilitável | Controla coleta da sessão |
| Screenshots | Desabilitado por padrão | Autorização jurídica + política + ciência + retenção |
| Visualização ao vivo | Estrutura de governança; captura técnica não liberada nesta versão | Autorização jurídica reforçada |
| Gravação de tela | Estrutura de governança; captura técnica não liberada nesta versão | Autorização jurídica reforçada |
| Áudio | Recurso excepcional/bloqueado | Não liberado automaticamente |
| Webcam | Recurso excepcional/bloqueado | Não liberado automaticamente |

## 3. Jornada x monitoramento

A .Clara distingue:

- **Jornada de referência**: usada para relatórios/alertas;
- **Período de monitoramento**: janela efetiva de coleta da telemetria de atividade no agente de sessão.

O cliente pode configurar valores padrão por empresa e sobrescrever por estação.

## 4. Classificação

A classificação produtivo/neutro/não produtivo é configurada pela empresa contratante. A .Clara não classifica intenção pessoal/profissional por inferência automática.

## 5. Alertas

A plataforma admite regras por empresa, departamento/equipe ou estação, com limite, severidade e canais. Os destinatários podem ser perfis com acesso à plataforma e/ou endereços externos de e-mail.

## 6. E-mail

Remetente padrão previsto:

`Share IT .Clara <monitoramento.clara@shareitbrasil.com.br>`

O cliente pode configurar Reply-To e destinatários adicionais. Credenciais SMTP/API permanecem em segredo de ambiente e não são armazenadas no código-fonte.

## 7. Governança

A plataforma mantém estrutura para documento, SHA-256, validade, solicitação de aprovação jurídica por link/token, ciência do colaborador e bloqueio por expiração. Nesta versão, a aprovação por link prova a posse do link enviado ao endereço informado; ela não equivale a uma autenticação corporativa federada do Jurídico.
