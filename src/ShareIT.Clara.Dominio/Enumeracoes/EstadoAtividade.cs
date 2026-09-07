namespace ShareIT.Clara.Dominio.Enumeracoes;

/// <summary>
/// Objetivo: representar o estado consolidado de atividade observado na sessão Windows.
/// Origem: processo ShareIT.Clara.Sessao.
/// Destino: agente, API, timeline e indicadores.
/// Dependências: nenhuma dependência externa.
/// Impacto: define a semântica dos períodos básicos de jornada digital observada.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public enum EstadoAtividade : short
{
    Ativo = 1,
    Ocioso = 2,
    Bloqueado = 3,
    Offline = 4
}
