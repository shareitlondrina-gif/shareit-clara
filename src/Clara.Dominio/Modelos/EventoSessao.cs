// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using Clara.Dominio.Enumeracoes;

namespace Clara.Dominio.Modelos;

/// <summary>
/// Objetivo: transportar uma amostra de estado da sessão interativa para o serviço do agente.
/// Origem: Clara.Sessao.
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
