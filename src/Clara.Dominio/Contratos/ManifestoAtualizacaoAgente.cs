// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Dominio.Contratos;

/// <summary>
/// Objetivo: descrever uma versão publicada e autorizada do agente.
/// Origem: publicador automático da Clara.
/// Destino: API de atualização e agentes Windows.
/// Dependências: pacote ZIP publicado e hash SHA-256.
/// Impacto: permite atualização automática verificável e rollback seguro.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record ManifestoAtualizacaoAgente(
    string Versao,
    string UrlPacote,
    string Sha256,
    bool Obrigatoria,
    DateTimeOffset PublicadaEmUtc);
