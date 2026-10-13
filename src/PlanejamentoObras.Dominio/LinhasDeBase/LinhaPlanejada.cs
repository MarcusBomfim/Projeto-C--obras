using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.LinhasDeBase;

/// <summary>
/// Uma linha da linha de base — uma <see cref="EtapaPlanejada"/> ou um
/// <see cref="ItemPlanejado"/> — com o planejado de cada mês, a preço e a
/// custo.
/// </summary>
/// <remarks>
/// <para>
/// A preço é o que se mede e se paga; a custo é o que se gasta, e é contra
/// ele que se mede o desempenho de custo.
/// </para>
/// <para>
/// Os meses são copiados para coleções só de leitura de verdade: nem
/// convertendo de volta para lista ou vetor dá para mudar um valor planejado.
/// É a promessa da linha de base (ADR-0007).
/// </para>
/// </remarks>
public abstract class LinhaPlanejada
{
    private protected LinhaPlanejada(
        NumeroDaEap numero,
        string descricao,
        Dinheiro total,
        IEnumerable<Dinheiro> valoresPlanejados,
        Dinheiro custoTotal,
        IEnumerable<Dinheiro> custosPlanejados)
    {
        Numero = numero;
        Descricao = descricao;
        Total = total;
        ValoresPlanejados = Array.AsReadOnly<Dinheiro>([.. valoresPlanejados]);
        CustoTotal = custoTotal;
        CustosPlanejados = Array.AsReadOnly<Dinheiro>([.. custosPlanejados]);
    }

    public NumeroDaEap Numero { get; }

    public int Nivel => Numero.Nivel;

    public string Descricao { get; }

    /// <summary>Quanto a linha vale, a preço de venda, toda executada.</summary>
    public Dinheiro Total { get; }

    /// <summary>Um valor por mês da linha de base, a preço de venda.</summary>
    public IReadOnlyList<Dinheiro> ValoresPlanejados { get; }

    /// <summary>Quanto a linha custa, toda executada: o orçamento no término do valor agregado.</summary>
    public Dinheiro CustoTotal { get; }

    /// <summary>Um custo por mês da linha de base.</summary>
    public IReadOnlyList<Dinheiro> CustosPlanejados { get; }
}
