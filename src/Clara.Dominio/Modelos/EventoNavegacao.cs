// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Dominio.Modelos;

/// <summary>
/// Objetivo: transportar uma amostra autorizada de contexto do navegador sem ler conteúdo da página.
/// Origem: extensão Chrome/Edge via Native Messaging.
/// Destino: Named Pipe local do agente e agregador de navegação.
/// Dependências: domínio já reduzido pela extensão, navegador e usuário Windows.
/// Impacto: permite medir duração por domínio sem coletar senha, formulário, texto digitado, caminho completo ou conteúdo da página.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record EventoNavegacao(
    DateTimeOffset MomentoUtc,
    string UsuarioWindows,
    string Dominio,
    string Navegador);
