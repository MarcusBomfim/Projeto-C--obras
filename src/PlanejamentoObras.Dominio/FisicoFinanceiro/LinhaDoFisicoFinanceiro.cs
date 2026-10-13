using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.FisicoFinanceiro;

/// <summary>
/// Uma linha do orçamento distribuída nos meses da obra.
/// </summary>
/// <remarks>
/// Classe, e não record: a igualdade de um record compararia a lista de
/// valores por referência, e duas linhas iguais pareceriam diferentes.
/// </remarks>
public sealed class LinhaDoFisicoFinanceiro
{
    internal LinhaDoFisicoFinanceiro(NumeroDaEap numero, string descricao, Dinheiro total, IReadOnlyList<Dinheiro> valores)
    {
        Numero = numero;
        Descricao = descricao;
        Total = total;
        Valores = valores;
    }

    public NumeroDaEap Numero { get; }

    public int Nivel => Numero.Nivel;

    public string Descricao { get; }

    /// <summary>O total da linha no orçamento — que é, centavo a centavo, a soma dos valores.</summary>
    public Dinheiro Total { get; }

    /// <summary>Um valor por mês, na ordem dos períodos do cronograma físico-financeiro.</summary>
    public IReadOnlyList<Dinheiro> Valores { get; }
}
