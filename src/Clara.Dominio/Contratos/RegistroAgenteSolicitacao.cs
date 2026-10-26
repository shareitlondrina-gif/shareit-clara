// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Dominio.Contratos;

public sealed record RegistroAgenteSolicitacao(
    string TokenInstalacao,
    string VersaoAgente,
    InventarioEquipamento Inventario);

public sealed record RegistroAgenteResposta(
    Guid IdentificadorAgente,
    string TokenDispositivo,
    Guid AtivoId,
    Guid TenantId);

public sealed record AutorizacaoManutencaoSolicitacao(string Codigo, string Operacao);
