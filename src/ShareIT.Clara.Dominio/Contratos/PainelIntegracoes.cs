namespace ShareIT.Clara.Dominio.Contratos;

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
