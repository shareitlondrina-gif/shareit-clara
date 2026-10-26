// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Dominio.Enumeracoes;

/// <summary>
/// Objetivo: representar o estado consolidado de atividade observado na sessão Windows.
/// Origem: processa Clara.Sessao.
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
    Offline = 4,
    Reuniao = 5
}
