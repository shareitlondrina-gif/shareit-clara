// Clara
// Programador: Paulo Andrade
// Objetivo: fornecer infraestrutura compartilhada da Clara.
// Origem: configuração e componentes internos.
// Destino: demais projetos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Comum.Versoes;

/// <summary>
/// Objetivo: comparar versões semânticas simples do agente.
/// Origem: versão instalada e manifesto publicado.
/// Destino: serviço de atualização automática.
/// Dependências: System.Version.
/// Impacto: decide de forma determinística quando o agente deve atualizar.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public static class ComparadorVersao
{
    public static bool HaVersaoMaisNova(string atual, string publicada)
    {
        var a = Parse(atual);
        var p = Parse(publicada);
        return p > a;
    }

    private static Version Parse(string valor)
    {
        valor = valor.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(valor, out var versao))
            throw new FormatException($"Versão inválida: {valor}");
        return versao;
    }
}
