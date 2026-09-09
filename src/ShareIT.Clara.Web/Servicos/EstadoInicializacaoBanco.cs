namespace ShareIT.Clara.Web.Servicos;

/// <summary>
/// Objetivo: registrar o estado da inicializacao do banco sem impedir que a Web/API suba e responda ao health de liveness.
/// Origem: processo de startup da aplicacao.
/// Destino: endpoints tecnicos de health e logs de diagnostico.
/// Dependencias: nenhuma dependencia externa.
/// Impacto: permite ao Render distinguir aplicacao viva de banco ainda inicializando ou com falha.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed class EstadoInicializacaoBanco
{
    private readonly object _sincronizacao = new();
    private string _situacao = "aguardando";
    private string? _erro;
    private DateTimeOffset? _iniciadoEmUtc;
    private DateTimeOffset? _concluidoEmUtc;

    public string Situacao
    {
        get { lock (_sincronizacao) return _situacao; }
    }

    public string? Erro
    {
        get { lock (_sincronizacao) return _erro; }
    }

    public DateTimeOffset? IniciadoEmUtc
    {
        get { lock (_sincronizacao) return _iniciadoEmUtc; }
    }

    public DateTimeOffset? ConcluidoEmUtc
    {
        get { lock (_sincronizacao) return _concluidoEmUtc; }
    }

    public bool ConcluidaComSucesso => Situacao is "ok" or "desabilitada";
    public bool Falhou => Situacao == "erro";

    public void Iniciar()
    {
        lock (_sincronizacao)
        {
            _situacao = "inicializando";
            _erro = null;
            _iniciadoEmUtc = DateTimeOffset.UtcNow;
            _concluidoEmUtc = null;
        }
    }

    public void Concluir()
    {
        lock (_sincronizacao)
        {
            _situacao = "ok";
            _erro = null;
            _concluidoEmUtc = DateTimeOffset.UtcNow;
        }
    }

    public void MarcarDesabilitada()
    {
        lock (_sincronizacao)
        {
            _situacao = "desabilitada";
            _erro = null;
            _iniciadoEmUtc = DateTimeOffset.UtcNow;
            _concluidoEmUtc = _iniciadoEmUtc;
        }
    }

    public void Falhar(Exception excecao)
    {
        lock (_sincronizacao)
        {
            _situacao = "erro";
            _erro = excecao.GetType().Name;
            _concluidoEmUtc = DateTimeOffset.UtcNow;
        }
    }
}
