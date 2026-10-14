using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Cronogramas;

/// <summary>A identidade de um cronograma.</summary>
public sealed record CronogramaId : Identificador
{
    public CronogramaId(Guid valor)
        : base(valor)
    {
    }

    public static CronogramaId Novo() => new(Guid.CreateVersion7());
}
