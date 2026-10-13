using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Medicoes;

/// <summary>
/// Um item numa medição registrada: a quantidade executada e o custo real no
/// mês.
/// </summary>
/// <remarks>
/// Só nasce dentro do <see cref="Acompanhamento"/>, depois de conferido contra
/// a linha de base e o que já foi medido.
/// </remarks>
public sealed record ItemDaMedicao
{
    internal ItemDaMedicao(NumeroDaEap item, decimal quantidade, Dinheiro custoReal)
    {
        Item = item;
        Quantidade = quantidade;
        CustoReal = custoReal;
    }

    /// <summary>O número do item na linha de base.</summary>
    public NumeroDaEap Item { get; }

    /// <summary>Executada no mês; negativa é glosa.</summary>
    public decimal Quantidade { get; }

    /// <summary>O custo real no mês.</summary>
    public Dinheiro CustoReal { get; }
}
