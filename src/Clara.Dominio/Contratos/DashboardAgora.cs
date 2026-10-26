// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Dominio.Contratos;

public sealed record DashboardAgora(
    int TotalAgentes,
    int Online,
    int Offline,
    int Ativos,
    int Ociosos,
    int Bloqueados,
    int Reuniao,
    int CadastroIncompleto,
    long SegundosAtivosHoje,
    long SegundosOciososHoje,
    string? AplicativoMaisUsadoHoje,
    IReadOnlyCollection<LinhaAgenteAgora> Agentes,
    IReadOnlyCollection<ResumoAplicativo> AplicativosHoje);

public sealed record LinhaAgenteAgora(
    Guid AgenteId,
    Guid AtivoId,
    string Hostname,
    string? UsuarioWindows,
    string? Responsavel,
    string? NumeroSerie,
    string? Fabricante,
    string? Modelo,
    string Status,
    string? EstadoAtual,
    string? ProcessoAtual,
    string LocalTrabalho,
    bool CadastroCompleto,
    string Versao,
    DateTimeOffset? UltimaComunicacaoUtc);

public sealed record ResumoAplicativo(
    string ProcessoExecutavel,
    long Segundos,
    string Classificacao,
    string? Categoria);
