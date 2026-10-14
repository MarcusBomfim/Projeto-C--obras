using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.LinhasDeBase;

/// <summary>A identidade de uma linha de base.</summary>
public sealed record LinhaDeBaseId : Identificador
{
    public LinhaDeBaseId(Guid valor)
        : base(valor)
    {
    }

    public static LinhaDeBaseId Novo() => new(Guid.CreateVersion7());
}
