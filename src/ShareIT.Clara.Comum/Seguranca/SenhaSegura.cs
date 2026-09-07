using System.Security.Cryptography;

namespace ShareIT.Clara.Comum.Seguranca;

/// <summary>
/// Objetivo: gerar e validar hashes de senha administrativa com PBKDF2.
/// Origem: autenticação Web.
/// Destino: tabela usuarios.
/// Dependências: Rfc2898DeriveBytes.
/// Impacto: evita armazenamento de senha em texto puro.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public static class SenhaSegura
{
    private const int Iteracoes = 210_000;
    private const int TamanhoHash = 32;

    public static (string Hash, string Sal) Criar(string senha)
    {
        var sal = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(sal));
    }

    public static bool Validar(string senha, string hashBase64, string salBase64)
    {
        var sal = Convert.FromBase64String(salBase64);
        var esperado = Convert.FromBase64String(hashBase64);
        var atual = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
        return CryptographicOperations.FixedTimeEquals(esperado, atual);
    }
}
