using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// A planilha orçamentária: cada linha com quantidade, custo, BDI, preço e
/// total, e os subtotais das etapas.
/// </summary>
/// <remarks>
/// Fotografia do orçamento contra uma base de custos, num momento. Mudou a
/// quantidade, o BDI ou um preço na base, calcula-se outra.
/// </remarks>
public sealed class OrcamentoCalculado
{
    /// <summary>Até 80 % do valor acumulado, classe A.</summary>
    public const decimal LimiteDaClasseA = 0.80m;

    /// <summary>De 80 % a 95 %, classe B; o resto, classe C.</summary>
    public const decimal LimiteDaClasseB = 0.95m;

    private readonly Dictionary<NumeroDaEap, LinhaDoOrcamento> _porNumero;

    internal OrcamentoCalculado(
        string nome,
        Bdi bdi,
        string nomeDaBase,
        DateOnly dataBase,
        IReadOnlyList<LinhaDoOrcamento> linhas,
        Dinheiro custoDireto,
        Dinheiro total)
    {
        Nome = nome;
        Bdi = bdi;
        NomeDaBase = nomeDaBase;
        DataBase = dataBase;
        Linhas = linhas;
        Itens = [.. linhas.OfType<LinhaDeItem>()];
        CustoDireto = custoDireto;
        Total = total;
        _porNumero = linhas.ToDictionary(linha => linha.Numero);
    }

    public string Nome { get; }

    public Bdi Bdi { get; }

    /// <summary>A base de custos de que vieram os preços.</summary>
    public string NomeDaBase { get; }

    /// <summary>A data a que os preços se referem.</summary>
    public DateOnly DataBase { get; }

    /// <summary>Etapas e itens, na ordem da planilha: cada etapa antes do que está dentro dela.</summary>
    public IReadOnlyList<LinhaDoOrcamento> Linhas { get; }

    /// <summary>Só os itens, na ordem da planilha.</summary>
    public IReadOnlyList<LinhaDeItem> Itens { get; }

    /// <summary>A soma dos custos dos itens, sem BDI.</summary>
    public Dinheiro CustoDireto { get; }

    /// <summary>O preço de venda da obra: a soma dos totais dos itens.</summary>
    public Dinheiro Total { get; }

    public LinhaDoOrcamento Linha(string numero)
    {
        var chave = NumeroDaEap.Ler(numero);

        return _porNumero.TryGetValue(chave, out var linha)
            ? linha
            : throw new ExcecaoDeDominio($"Não existe a linha {chave} neste orçamento.");
    }

    public LinhaDeItem Item(string numero) =>
        Linha(numero) as LinhaDeItem
        ?? throw new ExcecaoDeDominio($"A linha {numero} é uma etapa, e não um item.");

    /// <summary>
    /// Os itens do mais caro ao mais barato, com o peso acumulado e a classe
    /// de cada um.
    /// </summary>
    /// <remarks>
    /// A classe olha o acumulado ANTES do item: ele é A se, quando chega a
    /// vez dele, os mais caros ainda não somaram 80 %. Olhando o acumulado
    /// depois, um item que sozinho valesse 85 % do orçamento seria B — o
    /// contrário do que a curva quer mostrar. Empate de valor fica na ordem da
    /// planilha.
    /// </remarks>
    public IReadOnlyList<ItemDaCurvaAbc> CurvaAbc()
    {
        var curva = new List<ItemDaCurvaAbc>(Itens.Count);
        var acumulado = Dinheiro.Zero;

        // OrderByDescending é estável: no empate, vale a ordem da planilha.
        foreach (var item in Itens.OrderByDescending(item => item.Total))
        {
            var antes = Fracao(acumulado);
            var classe = antes < LimiteDaClasseA ? ClasseAbc.A : antes < LimiteDaClasseB ? ClasseAbc.B : ClasseAbc.C;

            acumulado += item.Total;
            curva.Add(new ItemDaCurvaAbc(item.Numero, item.Descricao, item.Total, item.Peso, Fracao(acumulado), classe));
        }

        return curva;
    }

    private decimal Fracao(Dinheiro parte) => Total == Dinheiro.Zero ? 0m : parte / Total;
}
