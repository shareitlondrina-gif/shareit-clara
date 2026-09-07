using ShareIT.Clara.Dominio.Enumeracoes;

namespace ShareIT.Clara.Dominio.Modelos;

/// <summary>
/// Objetivo: transportar uma amostra de estado da sessão interativa para o serviço do agente.
/// Origem: ShareIT.Clara.Sessao.
/// Destino: Named Pipe local e agregador de atividade do agente.
/// Dependências: EstadoAtividade.
/// Impacto: alimenta a timeline sem capturar teclas, senhas ou conteúdo digitado.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record EventoSessao(
    DateTimeOffset MomentoUtc,
    string UsuarioWindows,
    EstadoAtividade Estado,
    string? ProcessoExecutavel);
