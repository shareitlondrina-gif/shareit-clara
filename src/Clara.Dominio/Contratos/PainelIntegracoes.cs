// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Dominio.Contratos;

/// <summary>
/// Objetivo: expor o estado das integrações corporativas previstas para contexto de agenda, reunião e e-mail.
/// Origem: configuração do tenant e resultados de sincronização.
/// Destino: tela Integrações.
/// Dependências: Microsoft 365, Google Workspace e Zoom.
/// Impacto: deixa explícito o que está conectado, pendente ou com erro sem misturar credenciais com o banco.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record PainelIntegracoes(IReadOnlyCollection<IntegracaoResumo> Integracoes);

public sealed record IntegracaoResumo(
    string Provedor,
    string NomeExibicao,
    string Status,
    DateTimeOffset? UltimaSincronizacaoUtc,
    string? UltimoErro);
