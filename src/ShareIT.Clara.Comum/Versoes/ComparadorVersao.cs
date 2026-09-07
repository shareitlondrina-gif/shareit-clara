namespace ShareIT.Clara.Comum.Versoes;

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
