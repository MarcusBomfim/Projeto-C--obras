using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.LinhasDeBase;

/// <summary>Uma etapa da linha de base: a soma, mês a mês, do que está dentro dela.</summary>
public sealed class EtapaPlanejada : LinhaPlanejada
{
    internal EtapaPlanejada(
        NumeroDaEap numero,
        string descricao,
        Dinheiro total,
        IEnumerable<Dinheiro> valoresPlanejados,
        Dinheiro custoTotal,
        IEnumerable<Dinheiro> custosPlanejados)
        : base(numero, descricao, total, valoresPlanejados, custoTotal, custosPlanejados)
    {
    }
}
