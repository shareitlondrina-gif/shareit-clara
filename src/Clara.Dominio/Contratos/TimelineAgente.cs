// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Dominio.Contratos;

public sealed record TimelineAgente(
    Guid AgenteId,
    string Hostname,
    string? Responsavel,
    IReadOnlyCollection<LinhaTimeline> Periodos);

public sealed record LinhaTimeline(
    DateTimeOffset InicioUtc,
    DateTimeOffset FimUtc,
    string Estado,
    string? ProcessoExecutavel,
    string UsuarioWindows);
