using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Custos;

/// <summary>A identidade de uma base de custos.</summary>
public sealed record BaseDeCustosId : Identificador
{
    public BaseDeCustosId(Guid valor)
        : base(valor)
    {
    }

    public static BaseDeCustosId Novo() => new(Guid.CreateVersion7());
}
