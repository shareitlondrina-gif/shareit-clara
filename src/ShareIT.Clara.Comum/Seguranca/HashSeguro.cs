using System.Security.Cryptography;
using System.Text;

namespace ShareIT.Clara.Comum.Seguranca;

/// <summary>
/// Objetivo: concentrar geração e validação de hashes usados em tokens e arquivos.
/// Origem: tokens de instalação/dispositivo e pacotes de atualização.
/// Destino: banco, agente e publicador.
/// Dependências: SHA-256.
/// Impacto: evita persistência de tokens em texto puro e garante integridade de pacotes.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public static class HashSeguro
{
    public static string Sha256Texto(string valor) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor))).ToLowerInvariant();

    public static async Task<string> Sha256ArquivoAsync(string caminho, CancellationToken cancellationToken = default)
    {
        await using var fluxo = File.OpenRead(caminho);
        var hash = await SHA256.HashDataAsync(fluxo, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
