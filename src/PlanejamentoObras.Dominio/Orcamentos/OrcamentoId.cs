using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>A identidade de um orçamento.</summary>
public sealed record OrcamentoId : Identificador
{
    public OrcamentoId(Guid valor)
        : base(valor)
    {
    }

    public static OrcamentoId Novo() => new(Guid.CreateVersion7());
}
