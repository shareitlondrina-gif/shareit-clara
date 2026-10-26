// Clara
// Programador: Paulo Andrade
// Objetivo: executar coleta e comunicação do agente Windows.
// Origem: Windows, configuração local e API Clara.
// Destino: API Clara, telemetria autorizada e estado local.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using Clara.Dominio.Contratos;

namespace Clara.Agente.Inventario;

/// <summary>
/// Objetivo: coletar automaticamente identidade e inventário básico da estação Windows sem exigir digitação no painel.
/// Origem: CIM/WMI do Windows, Environment e interfaces de rede locais.
/// Destino: RegistroAgenteSolicitacao e HeartbeatAgente enviados à API.
/// Dependências: PowerShell/CIM disponível no Windows e System.Net.NetworkInformation.
/// Impacto: preenche hostname, usuário, serial, UUID, fabricante, modelo e capacidade técnica do ativo durante o onboarding.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class ColetorInventarioWindows(ILogger<ColetorInventarioWindows> logger)
{
    private readonly SemaphoreSlim _semaforo = new(1, 1);
    private InventarioEquipamento? _cache;
    private DateTimeOffset _cacheColetadoEmUtc = DateTimeOffset.MinValue;

    public async Task<InventarioEquipamento> ObterAsync(bool atualizar = false, CancellationToken ct = default)
    {
        if (!atualizar && _cache is not null && DateTimeOffset.UtcNow - _cacheColetadoEmUtc < TimeSpan.FromHours(24)) return _cache;
        await _semaforo.WaitAsync(ct);
        try
        {
            if (!atualizar && _cache is not null && DateTimeOffset.UtcNow - _cacheColetadoEmUtc < TimeSpan.FromHours(24)) return _cache;
            _cache = await ColetarAsync(ct);
            _cacheColetadoEmUtc = DateTimeOffset.UtcNow;
            return _cache;
        }
        finally { _semaforo.Release(); }
    }

    private async Task<InventarioEquipamento> ColetarAsync(CancellationToken ct)
    {
        var fallback = new InventarioEquipamento(
            Environment.MachineName,
            ObterUsuarioFallback(),
            Environment.OSVersion.VersionString,
            null,
            null,
            null,
            null,
            System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            null,
            null,
            ObterIpv4Local());

        try
        {
            const string script = """
                $bios = Get-CimInstance Win32_BIOS | Select-Object -First 1
                $cs = Get-CimInstance Win32_ComputerSystem | Select-Object -First 1
                $prod = Get-CimInstance Win32_ComputerSystemProduct | Select-Object -First 1
                $os = Get-CimInstance Win32_OperatingSystem | Select-Object -First 1
                $disk = (Get-CimInstance Win32_LogicalDisk -Filter \"DriveType=3\" | Measure-Object -Property Size -Sum).Sum
                [pscustomobject]@{
                    NumeroSerie = $bios.SerialNumber
                    UuidEquipamento = $prod.UUID
                    Fabricante = $cs.Manufacturer
                    Modelo = $cs.Model
                    UsuarioWindows = $cs.UserName
                    SistemaOperacional = $os.Caption + ' ' + $os.Version
                    Arquitetura = $os.OSArchitecture
                    MemoriaTotalBytes = [int64]$cs.TotalPhysicalMemory
                    DiscoTotalBytes = [int64]$disk
                } | ConvertTo-Json -Compress
                """;

            var comandoCodificado = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var inicio = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {comandoCodificado}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var processo = Process.Start(inicio);
            if (processo is null) return fallback;
            var saidaTask = processo.StandardOutput.ReadToEndAsync(ct);
            var erroTask = processo.StandardError.ReadToEndAsync(ct);
            await processo.WaitForExitAsync(ct);
            var saida = await saidaTask;
            var erro = await erroTask;
            if (processo.ExitCode != 0 || string.IsNullOrWhiteSpace(saida))
            {
                logger.LogDebug("Inventário CIM indisponível. Código {Codigo}. {Erro}", processo.ExitCode, erro);
                return fallback;
            }

            var dados = JsonSerializer.Deserialize<DadosCim>(saida, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dados is null) return fallback;

            return fallback with
            {
                UsuarioWindows = Limpar(dados.UsuarioWindows) ?? fallback.UsuarioWindows,
                SistemaOperacional = Limpar(dados.SistemaOperacional) ?? fallback.SistemaOperacional,
                NumeroSerie = Limpar(dados.NumeroSerie),
                UuidEquipamento = Limpar(dados.UuidEquipamento),
                Fabricante = Limpar(dados.Fabricante),
                Modelo = Limpar(dados.Modelo),
                Arquitetura = Limpar(dados.Arquitetura) ?? fallback.Arquitetura,
                MemoriaTotalBytes = dados.MemoriaTotalBytes > 0 ? dados.MemoriaTotalBytes : null,
                DiscoTotalBytes = dados.DiscoTotalBytes > 0 ? dados.DiscoTotalBytes : null
            };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Falha não bloqueante ao coletar inventário detalhado; usando dados básicos.");
            return fallback;
        }
    }

    private static string? ObterUsuarioFallback()
    {
        try { return Environment.UserDomainName + "\\" + Environment.UserName; }
        catch { return Environment.UserName; }
    }

    private static string? ObterIpv4Local()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a))
                ?.ToString();
        }
        catch { return null; }
    }

    private static string? Limpar(string? valor)
    {
        valor = valor?.Trim();
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (valor.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase)) return null;
        if (valor.Equals("Default string", StringComparison.OrdinalIgnoreCase)) return null;
        return valor;
    }

    /// <summary>
    /// Objetivo: desserializar somente os campos técnicos retornados pela consulta CIM local.
    /// Origem: PowerShell/CIM executado na própria estação.
    /// Destino: InventarioEquipamento.
    /// Dependências: System.Text.Json.
    /// Impacto: mantém o formato intermediário isolado do contrato enviado ao servidor.
    /// Desenvolvedor: Paulo Andrade
    /// </summary>
    private sealed class DadosCim
    {
        public string? NumeroSerie { get; set; }
        public string? UuidEquipamento { get; set; }
        public string? Fabricante { get; set; }
        public string? Modelo { get; set; }
        public string? UsuarioWindows { get; set; }
        public string? SistemaOperacional { get; set; }
        public string? Arquitetura { get; set; }
        public long MemoriaTotalBytes { get; set; }
        public long DiscoTotalBytes { get; set; }
    }
}
