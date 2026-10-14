using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Medicoes;

/// <summary>A identidade de um acompanhamento.</summary>
public sealed record AcompanhamentoId : Identificador
{
    public AcompanhamentoId(Guid valor)
        : base(valor)
    {
    }

    public static AcompanhamentoId Novo() => new(Guid.CreateVersion7());
}
