namespace PlanejamentoObras.Dominio.Comum;

/// <summary>
/// Um fato do domínio, no passado: algo que aconteceu com um agregado e que
/// outras partes do sistema podem querer saber (ADR-0016).
/// </summary>
/// <remarks>
/// Leva o que identifica o fato — o agregado, e o mínimo para dizer o que
/// houve —, e não o estado inteiro. Quem precisa do estado lê o agregado,
/// que é a fonte da verdade.
/// </remarks>
public abstract record EventoDeDominio
{
    /// <summary>O agregado com que o fato aconteceu.</summary>
    public abstract Identificador Agregado { get; }
}

/// <summary>
/// Um agregado que registra os fatos que acontecem com ele, na ordem em que
/// acontecem. A persistência os publica junto com a gravação, e os descarta.
/// </summary>
public abstract class AgregadoComEventos
{
    private readonly List<EventoDeDominio> _eventos = [];

    /// <summary>Os fatos desde a última gravação, do mais antigo ao mais novo.</summary>
    public IReadOnlyList<EventoDeDominio> Eventos => _eventos.AsReadOnly();

    protected void Registrar(EventoDeDominio evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        _eventos.Add(evento);
    }

    /// <summary>
    /// Esquece os eventos: depois que a persistência os publicou, ou depois de
    /// reconstituir o agregado — os fatos de então já tinham sido publicados.
    /// </summary>
    internal void DescartarEventos() => _eventos.Clear();
}
