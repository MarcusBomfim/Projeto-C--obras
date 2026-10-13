using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.LinhasDeBase;

/// <summary>
/// Um item da linha de base: o que foi contratado — quantidade, preço e custo
/// unitários — e quando se planejou executá-lo.
/// </summary>
public sealed class ItemPlanejado : LinhaPlanejada
{
    internal ItemPlanejado(LinhaDeItem item, IEnumerable<Dinheiro> valoresPlanejados, IEnumerable<Dinheiro> custosPlanejados)
        : base(item.Numero, item.Descricao, item.Total, valoresPlanejados, item.CustoTotal, custosPlanejados)
    {
        Unidade = item.Unidade;
        Quantidade = item.Quantidade;
        PrecoUnitario = item.PrecoUnitario;
        CustoUnitario = item.CustoUnitario;
        Atividade = item.Atividade!;
    }

    public UnidadeDeMedida Unidade { get; }

    /// <summary>A quantidade contratada: medir além dela exige aditivo.</summary>
    public decimal Quantidade { get; }

    /// <summary>O preço unitário, com BDI: é a ele que se mede e se paga.</summary>
    public Dinheiro PrecoUnitario { get; }

    /// <summary>O custo unitário, sem BDI: é contra ele que se mede o desempenho de custo.</summary>
    public Dinheiro CustoUnitario { get; }

    /// <summary>A atividade do cronograma a que o item estava vinculado.</summary>
    public CodigoDaAtividade Atividade { get; }
}
