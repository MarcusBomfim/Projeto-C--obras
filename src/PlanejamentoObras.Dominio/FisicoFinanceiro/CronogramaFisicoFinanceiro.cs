using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.FisicoFinanceiro;

/// <summary>
/// O orçamento distribuído nos meses da obra: quanto se executa — e quanto se
/// fatura — em cada mês, por etapa e por item.
/// </summary>
/// <remarks>
/// Fotografia de um orçamento e de um cronograma num momento. É derivado dos
/// dois: mudou a quantidade, o preço ou a rede, calcula-se outro.
/// </remarks>
public sealed class CronogramaFisicoFinanceiro
{
    private readonly Dictionary<NumeroDaEap, LinhaDoFisicoFinanceiro> _porNumero;

    internal CronogramaFisicoFinanceiro(
        DatasDoCronograma datas,
        IReadOnlyList<PeriodoDoFisicoFinanceiro> periodos,
        IReadOnlyList<LinhaDoFisicoFinanceiro> linhas,
        Dinheiro total)
    {
        Datas = datas;
        Periodos = periodos;
        Linhas = linhas;
        Total = total;
        _porNumero = linhas.ToDictionary(linha => linha.Numero);
    }

    /// <summary>Se o dinheiro foi distribuído pelas datas cedo ou pelas datas tarde.</summary>
    public DatasDoCronograma Datas { get; }

    /// <summary>Os meses da obra, do primeiro ao último, inclusive os que não tiverem valor.</summary>
    public IReadOnlyList<PeriodoDoFisicoFinanceiro> Periodos { get; }

    /// <summary>Uma linha por etapa e por item do orçamento, na ordem da planilha.</summary>
    public IReadOnlyList<LinhaDoFisicoFinanceiro> Linhas { get; }

    /// <summary>O total da obra: o mesmo do orçamento, ao centavo.</summary>
    public Dinheiro Total { get; }

    public LinhaDoFisicoFinanceiro Linha(string numero)
    {
        var chave = NumeroDaEap.Ler(numero);

        return _porNumero.TryGetValue(chave, out var linha)
            ? linha
            : throw new ExcecaoDeDominio($"Não existe a linha {chave} neste cronograma físico-financeiro.");
    }
}
