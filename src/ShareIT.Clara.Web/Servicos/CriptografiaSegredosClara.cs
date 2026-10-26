using System.Security.Cryptography;
using System.Text;

namespace ShareIT.Clara.Web.Servicos;

/// <summary>
/// Objetivo: cifrar credenciais de integrações/armazenamento sem persistir a chave-raiz no PostgreSQL.
/// Origem: CLARA_COFRE_MASTER_KEY_BASE64 (32 bytes em Base64) no backend.
/// Destino: segredos_integracao.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class CriptografiaSegredosClara
{
    private static byte[] Chave()
    {
        var bruto = Environment.GetEnvironmentVariable("CLARA_COFRE_MASTER_KEY_BASE64");
        if (string.IsNullOrWhiteSpace(bruto)) throw new InvalidOperationException("Configure CLARA_COFRE_MASTER_KEY_BASE64 no backend para usar armazenamento externo.");
        byte[] chave;
        try { chave = Convert.FromBase64String(bruto); }
        catch { throw new InvalidOperationException("CLARA_COFRE_MASTER_KEY_BASE64 não está em Base64 válido."); }
        if (chave.Length != 32) throw new InvalidOperationException("CLARA_COFRE_MASTER_KEY_BASE64 precisa possuir exatamente 32 bytes (AES-256).");
        return chave;
    }

    public bool Configurada { get { try { _ = Chave(); return true; } catch { return false; } } }

    public (string Cipher, string Nonce, string Tag) Cifrar(string segredo)
    {
        var aberto = Encoding.UTF8.GetBytes(segredo);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cifrado = new byte[aberto.Length];
        using var aes = new AesGcm(Chave(), 16);
        aes.Encrypt(nonce, aberto, cifrado, tag);
        CryptographicOperations.ZeroMemory(aberto);
        return (Convert.ToBase64String(cifrado), Convert.ToBase64String(nonce), Convert.ToBase64String(tag));
    }

    public string Decifrar(string cipher, string nonce, string tag)
    {
        var cifrado = Convert.FromBase64String(cipher);
        var aberto = new byte[cifrado.Length];
        using var aes = new AesGcm(Chave(), 16);
        aes.Decrypt(Convert.FromBase64String(nonce), cifrado, Convert.FromBase64String(tag), aberto);
        try { return Encoding.UTF8.GetString(aberto); }
        finally { CryptographicOperations.ZeroMemory(aberto); }
    }
}
