using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

var argumentos = Argumentos.Parse(args);
var pastaDados = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ShareIT", "Clara");
var pastaAtualizacoes = Path.Combine(pastaDados, "atualizacoes");
Directory.CreateDirectory(pastaAtualizacoes);
var resultado = Path.Combine(pastaAtualizacoes, "resultado-atualizacao.json");
var momento = DateTimeOffset.UtcNow;
var backupVersao = Path.Combine(argumentos.Backup, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{argumentos.VersaoOrigem}");
var staging = Path.Combine(pastaAtualizacoes, "staging-" + Guid.NewGuid().ToString("N"));
using var travaGlobal = new Mutex(false, @"Global\ShareIT.ProductUpdate");
var travaAdquirida = false;

try
{
    // Coordenação entre produtos Share IT. Se outro atualizador adotar a mesma trava, as trocas de arquivos
    // deixam de ocorrer simultaneamente. A Clara também posterga antes quando detecta workdir recente do .VISÃO.
    try { travaAdquirida = travaGlobal.WaitOne(TimeSpan.FromMinutes(3)); }
    catch (AbandonedMutexException) { travaAdquirida = true; }
    if (!travaAdquirida) throw new InvalidOperationException("Outra atualização Share IT está em andamento. A atualização da .Clara foi adiada com segurança.");

    Directory.CreateDirectory(argumentos.Backup);
    PararServico(argumentos.Servico);
    EncerrarProcesso(argumentos.ProcessoSessao);

    if (Directory.Exists(argumentos.Destino))
        CopiarDiretorio(argumentos.Destino, backupVersao);

    Directory.CreateDirectory(staging);
    ZipFile.ExtractToDirectory(argumentos.Pacote, staging, overwriteFiles: true);
    ValidarPacote(staging);
    CopiarDiretorio(staging, argumentos.Destino, limparDestino: true);

    var marcadorSaude = Path.Combine(pastaAtualizacoes, "saude-agente.json");
    try { if (File.Exists(marcadorSaude)) File.Delete(marcadorSaude); } catch { }
    IniciarServico(argumentos.Servico);
    AguardarServicoExecutando(argumentos.Servico, TimeSpan.FromSeconds(45));
    Executar("schtasks.exe", $"/Run /TN \"{argumentos.TarefaSessao}\"", ignorarErro: true);
    AguardarMarcadorSaude(marcadorSaude, argumentos.VersaoDestino, TimeSpan.FromSeconds(60));
    Thread.Sleep(TimeSpan.FromSeconds(8));
    AguardarServicoExecutando(argumentos.Servico, TimeSpan.FromSeconds(15));

    LimparBackupsAntigos(argumentos.Backup, 3);
    EscreverResultado(resultado, argumentos, "SucessoValidado", $"Serviço e marcador de saúde da versão {argumentos.VersaoDestino} confirmados após atualização. Backup: {backupVersao}", momento);
    return 0;
}
catch (Exception ex)
{
    try
    {
        if (Directory.Exists(backupVersao))
        {
            PararServico(argumentos.Servico);
            CopiarDiretorio(backupVersao, argumentos.Destino, limparDestino: true);
            IniciarServico(argumentos.Servico);
            AguardarServicoExecutando(argumentos.Servico, TimeSpan.FromSeconds(45));
            Executar("schtasks.exe", $"/Run /TN \"{argumentos.TarefaSessao}\"", ignorarErro: true);
        }
    }
    catch (Exception rollbackEx)
    {
        ex = new AggregateException("Falha na atualização e no rollback.", ex, rollbackEx);
    }

    EscreverResultado(resultado, argumentos, "FalhaComRollback", ex.ToString(), momento);
    return 1;
}
finally
{
    try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
    if (travaAdquirida) try { travaGlobal.ReleaseMutex(); } catch { }
}

static void ValidarPacote(string raiz)
{
    var obrigatorios = new[]
    {
        Path.Combine(raiz, "Agente", "ShareIT.Clara.Agente.exe"),
        Path.Combine(raiz, "Sessao", "ShareIT.Clara.Sessao.exe"),
        Path.Combine(raiz, "Atualizador", "ShareIT.Clara.Atualizador.exe")
    };
    var faltantes = obrigatorios.Where(x => !File.Exists(x)).Select(Path.GetFileName).ToArray();
    if (faltantes.Length > 0)
        throw new InvalidDataException("Pacote de atualização incompleto: " + string.Join(", ", faltantes));
}

static void PararServico(string servico)
{
    Executar("sc.exe", $"stop \"{servico}\"", ignorarErro: true);
    for (var i = 0; i < 30; i++)
    {
        var saida = Executar("sc.exe", $"query \"{servico}\"", ignorarErro: true);
        if (saida.Contains("STOPPED", StringComparison.OrdinalIgnoreCase)) return;
        Thread.Sleep(1000);
    }
}

static void IniciarServico(string servico)
{
    Executar("sc.exe", $"start \"{servico}\"", ignorarErro: false);
}

static void AguardarServicoExecutando(string servico, TimeSpan timeout)
{
    var limite = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < limite)
    {
        var saida = Executar("sc.exe", $"query \"{servico}\"", ignorarErro: true);
        if (saida.Contains("RUNNING", StringComparison.OrdinalIgnoreCase)) return;
        Thread.Sleep(1500);
    }
    throw new InvalidOperationException($"O serviço {servico} não atingiu o estado RUNNING após a atualização.");
}

static void AguardarMarcadorSaude(string arquivo, string versaoEsperada, TimeSpan timeout)
{
    var limite = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < limite)
    {
        try
        {
            if (File.Exists(arquivo))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(arquivo));
                var versao = doc.RootElement.TryGetProperty("versao", out var v) ? v.GetString() : null;
                var iniciado = doc.RootElement.TryGetProperty("iniciadoEmUtc", out var i) && i.TryGetDateTimeOffset(out var data) ? data : DateTimeOffset.MinValue;
                if (string.Equals(versao, versaoEsperada, StringComparison.OrdinalIgnoreCase)
                    && iniciado >= DateTimeOffset.UtcNow.AddMinutes(-2))
                    return;
            }
        }
        catch { }
        Thread.Sleep(1500);
    }
    throw new InvalidOperationException($"O agente não confirmou o marcador local de saúde para a versão {versaoEsperada}.");
}

static void EncerrarProcesso(string processo)
{
    foreach (var p in Process.GetProcessesByName(processo))
    {
        try { p.Kill(entireProcessTree: true); p.WaitForExit(5000); } catch { }
    }
}

static string Executar(string arquivo, string argumentos, bool ignorarErro)
{
    using var processo = Process.Start(new ProcessStartInfo(arquivo, argumentos)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    }) ?? throw new InvalidOperationException($"Não foi possível iniciar {arquivo}.");
    var saida = processo.StandardOutput.ReadToEnd();
    var erro = processo.StandardError.ReadToEnd();
    processo.WaitForExit(30000);
    if (!ignorarErro && processo.ExitCode != 0) throw new InvalidOperationException($"{arquivo} falhou ({processo.ExitCode}): {erro}");
    return saida + Environment.NewLine + erro;
}

static void CopiarDiretorio(string origem, string destino, bool limparDestino = false)
{
    if (limparDestino && Directory.Exists(destino))
    {
        foreach (var pasta in Directory.GetDirectories(destino)) Directory.Delete(pasta, true);
        foreach (var arquivo in Directory.GetFiles(destino)) File.Delete(arquivo);
    }
    Directory.CreateDirectory(destino);
    foreach (var arquivo in Directory.GetFiles(origem)) File.Copy(arquivo, Path.Combine(destino, Path.GetFileName(arquivo)), true);
    foreach (var pasta in Directory.GetDirectories(origem)) CopiarDiretorio(pasta, Path.Combine(destino, Path.GetFileName(pasta)));
}

static void LimparBackupsAntigos(string raiz, int manter)
{
    try
    {
        if (!Directory.Exists(raiz)) return;
        foreach (var pasta in Directory.GetDirectories(raiz).OrderByDescending(Directory.GetLastWriteTimeUtc).Skip(Math.Max(1, manter)))
            try { Directory.Delete(pasta, true); } catch { }
    }
    catch { }
}

static void EscreverResultado(string arquivo, Argumentos a, string status, string detalhe, DateTimeOffset momento)
{
    var objeto = new
    {
        VersaoOrigem = a.VersaoOrigem,
        VersaoDestino = a.VersaoDestino,
        Status = status,
        Detalhe = detalhe,
        MomentoUtc = momento
    };
    File.WriteAllText(arquivo, JsonSerializer.Serialize(objeto, new JsonSerializerOptions { WriteIndented = true }));
}

internal sealed record Argumentos(
    string Pacote,
    string Destino,
    string Backup,
    string Servico,
    string ProcessoSessao,
    string TarefaSessao,
    string VersaoOrigem,
    string VersaoDestino)
{
    public static Argumentos Parse(string[] args)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length - 1; i += 2) d[args[i]] = args[i + 1];
        string V(string nome) => d.TryGetValue(nome, out var valor) && !string.IsNullOrWhiteSpace(valor) ? valor : throw new ArgumentException($"Argumento obrigatório ausente: {nome}");
        return new(V("--pacote"), V("--destino"), V("--backup"), V("--servico"), V("--processo-sessao"), V("--tarefa-sessao"), V("--versao-origem"), V("--versao-destino"));
    }
}
