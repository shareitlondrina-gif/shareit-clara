// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using Clara.Dominio.Enumeracoes;

namespace Clara.Dominio.Modelos;

/// <summary>
/// Objetivo: representar um período agregado de atividade de uma sessão.
/// Origem: agregador local do agente.
/// Destino: fila offline, API e PostgreSQL.
/// Dependências: EstadoAtividade.
/// Impacto: reduz volume de telemetria e preserva o contexto temporal necessário ao dashboard.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record PeriodoAtividade(
    DateTimeOffset InicioUtc,
    DateTimeOffset FimUtc,
    string UsuarioWindows,
    EstadoAtividade Estado,
    string? ProcessoExecutavel);
