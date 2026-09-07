using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShareIT.Clara.Agente.Configuracao;

namespace ShareIT.Clara.Agente.Identidade;

/// <summary>
/// Objetivo: persistir a identidade do dispositivo de forma protegida no Windows.
/// Origem: resposta de registro da API.
/// Destino: autenticação de todas as chamadas posteriores do agente.
/// Dependências: Windows DPAPI.
/// Impacto: evita armazenar o token de dispositivo em texto puro no disco.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class GerenciadorIdentidadeAgente
{
    private readonly string _caminho = Path.Combine(ConfiguracaoAgenteLocal.DiretorioSegredos, "identidade.dat");
    private readonly object _sincronizacao = new();
    private IdentidadeAgente? _atual;

    public IdentidadeAgente? Atual
    {
        get { lock (_sincronizacao) return _atual ??= CarregarInterno(); }
    }

    public void Salvar(IdentidadeAgente identidade)
    {
        var json = JsonSerializer.Serialize(identidade);
        var protegido = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.LocalMachine);
        Directory.CreateDirectory(Path.GetDirectoryName(_caminho)!);
        File.WriteAllBytes(_caminho, protegido);
        lock (_sincronizacao) _atual = identidade;
    }

    private IdentidadeAgente? CarregarInterno()
    {
        if (!File.Exists(_caminho)) return null;
        var protegido = File.ReadAllBytes(_caminho);
        var dados = ProtectedData.Unprotect(protegido, null, DataProtectionScope.LocalMachine);
        return JsonSerializer.Deserialize<IdentidadeAgente>(Encoding.UTF8.GetString(dados));
    }
}
