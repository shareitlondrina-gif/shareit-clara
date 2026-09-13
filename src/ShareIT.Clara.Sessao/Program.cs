using System.Security.Principal;
using ShareIT.Clara.Dominio.Modelos;
using ShareIT.Clara.Sessao;
using ShareIT.Clara.Sessao.Comunicacao;
using ShareIT.Clara.Sessao.Windows;

var configuracao = ConfiguracaoSessaoLocal.Carregar();
var proximaLeituraConfiguracao = DateTimeOffset.MinValue;
var usuario = WindowsIdentity.GetCurrent().Name;
using var encerramento = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; encerramento.Cancel(); };

while (!encerramento.IsCancellationRequested)
{
    try
    {
        if (DateTimeOffset.UtcNow >= proximaLeituraConfiguracao)
        {
            configuracao = ConfiguracaoSessaoLocal.Carregar();
            proximaLeituraConfiguracao = DateTimeOffset.UtcNow.AddMinutes(1);
        }

        if (configuracao.EstaDentroHorario(DateTimeOffset.Now))
        {
            var (estado, processo) = LeitorAtividadeWindows.Obter(configuracao.LimiarOciosidadeMinutos);
            await ClientePipeAgente.EnviarAsync(new EventoSessao(DateTimeOffset.UtcNow, usuario, estado, processo), encerramento.Token);
        }
    }
    catch (OperationCanceledException) when (encerramento.IsCancellationRequested) { break; }
    catch { /* agente pode estar iniciando/atualizando; a próxima amostra tentará novamente */ }

    try { await Task.Delay(TimeSpan.FromSeconds(15), encerramento.Token); }
    catch (OperationCanceledException) { break; }
}
