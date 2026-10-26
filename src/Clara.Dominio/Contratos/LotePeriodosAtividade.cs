// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using Clara.Dominio.Modelos;

namespace Clara.Dominio.Contratos;

public sealed record LotePeriodosAtividade(IReadOnlyCollection<PeriodoAtividade> Periodos);
