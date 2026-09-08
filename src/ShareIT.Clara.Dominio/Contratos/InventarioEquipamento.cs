namespace ShareIT.Clara.Dominio.Contratos;

/// <summary>
/// Objetivo: transportar o inventário técnico mínimo coletado automaticamente pelo agente Windows.
/// Origem: ShareIT.Clara.Agente na estação monitorada.
/// Destino: API e PostgreSQL do Share IT .Clara.
/// Dependências: APIs do Windows e informações seguras do equipamento.
/// Impacto: elimina cadastro manual de hostname, usuário, serial, fabricante e modelo durante a instalação.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record InventarioEquipamento(
    string Hostname,
    string? UsuarioWindows,
    string SistemaOperacional,
    string? NumeroSerie,
    string? UuidEquipamento,
    string? Fabricante,
    string? Modelo,
    string? Arquitetura,
    long? MemoriaTotalBytes,
    long? DiscoTotalBytes,
    string? EnderecoIpLocal);
