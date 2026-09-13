namespace ShareIT.Clara.Dominio.Modelos;

/// <summary>
/// Objetivo: representar um período agregado de permanência em um domínio autorizado.
/// Origem: amostras recebidas do conector Chrome/Edge.
/// Destino: fila offline do agente e tabela sites_utilizados.
/// Dependências: EventoNavegacao.
/// Impacto: reduz volume de eventos e preserva apenas domínio, navegador, usuário e intervalo temporal.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record PeriodoNavegacao(
    DateTimeOffset InicioUtc,
    DateTimeOffset FimUtc,
    string UsuarioWindows,
    string Dominio,
    string Navegador);
