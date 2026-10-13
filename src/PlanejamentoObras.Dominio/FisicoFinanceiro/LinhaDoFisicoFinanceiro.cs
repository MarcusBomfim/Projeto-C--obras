using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.FisicoFinanceiro;

/// <summary>
/// Uma linha do orçamento distribuída nos meses da obra: o valor, a preço de
/// venda, e o custo direto.
/// </summary>
/// <remarks>
/// Classe, e não record: a igualdade de um record compararia as listas por
/// referência, e duas linhas iguais pareceriam diferentes. As listas são
/// cópias só de leitura: nem convertendo de volta dá para mudar um mês.
/// </remarks>
public sealed class LinhaDoFisicoFinanceiro
{
    internal LinhaDoFisicoFinanceiro(
        NumeroDaEap numero,
        string descricao,
        Dinheiro total,
        IEnumerable<Dinheiro> valores,
        Dinheiro custoTotal,
        IEnumerable<Dinheiro> custos)
    {
        Numero = numero;
        Descricao = descricao;
        Total = total;
        Valores = Array.AsReadOnly<Dinheiro>([.. valores]);
        CustoTotal = custoTotal;
        Custos = Array.AsReadOnly<Dinheiro>([.. custos]);
    }

    public NumeroDaEap Numero { get; }

    public int Nivel => Numero.Nivel;

    public string Descricao { get; }

    /// <summary>O total da linha no orçamento — que é, centavo a centavo, a soma dos valores.</summary>
    public Dinheiro Total { get; }

    /// <summary>Um valor por mês, a preço de venda, na ordem dos períodos do cronograma físico-financeiro.</summary>
    public IReadOnlyList<Dinheiro> Valores { get; }

    /// <summary>O custo direto da linha — a soma dos custos.</summary>
    public Dinheiro CustoTotal { get; }

    /// <summary>O custo direto de cada mês, distribuído pelos mesmos dias do valor.</summary>
    public IReadOnlyList<Dinheiro> Custos { get; }
}
