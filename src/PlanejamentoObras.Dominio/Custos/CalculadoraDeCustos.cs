using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Custos;

/// <summary>
/// Calcula o custo unitário de composições, abrindo as composições auxiliares.
/// </summary>
/// <remarks>
/// <para>
/// Cada linha vale coeficiente vezes custo unitário, arredondado ao centavo
/// (NBR 5891); a composição vale a soma das linhas. Uma composição auxiliar
/// entra na de cima pelo custo unitário dela, já em centavos. Assim todo
/// número da composição analítica se refaz com uma calculadora a partir dos
/// números que ela mesma mostra — ver ADR-0004.
/// </para>
/// <para>
/// Cada composição é calculada uma vez por chamada, mesmo que apareça como
/// auxiliar de várias: a argamassa usada em alvenaria, reboco e contrapiso sai
/// do mesmo cálculo. A recursão termina porque a base não admite ciclo.
/// </para>
/// </remarks>
public static class CalculadoraDeCustos
{
    public static ComposicaoAnalitica Calcular(BaseDeCustos baseDeCustos, string composicao)
    {
        ArgumentNullException.ThrowIfNull(baseDeCustos);

        return Calcular(baseDeCustos, Localizar(baseDeCustos, new CodigoDeCusto(composicao)), []);
    }

    /// <summary>
    /// Calcula várias composições de uma vez, compartilhando as auxiliares
    /// entre elas.
    /// </summary>
    public static IReadOnlyDictionary<CodigoDeCusto, ComposicaoAnalitica> Calcular(
        BaseDeCustos baseDeCustos,
        IEnumerable<string> composicoes)
    {
        ArgumentNullException.ThrowIfNull(baseDeCustos);
        ArgumentNullException.ThrowIfNull(composicoes);

        var calculadas = new Dictionary<CodigoDeCusto, ComposicaoAnalitica>();
        var pedidas = new Dictionary<CodigoDeCusto, ComposicaoAnalitica>();

        foreach (var codigo in composicoes.Select(codigo => new CodigoDeCusto(codigo)))
        {
            pedidas[codigo] = Calcular(baseDeCustos, Localizar(baseDeCustos, codigo), calculadas);
        }

        return pedidas;
    }

    private static ComposicaoAnalitica Calcular(
        BaseDeCustos baseDeCustos,
        Composicao composicao,
        Dictionary<CodigoDeCusto, ComposicaoAnalitica> calculadas)
    {
        if (calculadas.TryGetValue(composicao.Codigo, out var pronta))
        {
            return pronta;
        }

        /*
         * Composição sem itens é composição em construção. Custo zero seria
         * uma resposta errada com cara de certa: o orçamento sairia mais
         * barato sem ninguém perceber.
         */
        if (composicao.Itens.Count == 0)
        {
            throw new ExcecaoDeDominio(
                $"A composição {composicao.Codigo} não tem itens. Complete-a antes de calcular o custo.");
        }

        var linhas = new List<LinhaDaComposicao>(composicao.Itens.Count);

        foreach (var item in composicao.Itens)
        {
            var linha = baseDeCustos.TentarInsumo(item.Codigo, out var insumo)
                ? Linha(item, insumo.Descricao, insumo.Unidade, insumo.Tipo, insumo.Preco)
                : LinhaAuxiliar(item, Calcular(baseDeCustos, Localizar(baseDeCustos, item.Codigo), calculadas));

            linhas.Add(linha);
        }

        var analitica = new ComposicaoAnalitica(composicao, linhas);
        calculadas[composicao.Codigo] = analitica;

        return analitica;
    }

    private static LinhaDaComposicao LinhaAuxiliar(ItemDaComposicao item, ComposicaoAnalitica auxiliar) =>
        Linha(item, auxiliar.Descricao, auxiliar.Unidade, tipo: null, auxiliar.CustoUnitario);

    private static LinhaDaComposicao Linha(
        ItemDaComposicao item,
        string descricao,
        UnidadeDeMedida unidade,
        TipoDeInsumo? tipo,
        Dinheiro custoUnitario) =>
        new(
            item.Codigo,
            descricao,
            unidade,
            tipo,
            item.Coeficiente,
            custoUnitario,
            (custoUnitario * item.Coeficiente).EmCentavos());

    private static Composicao Localizar(BaseDeCustos baseDeCustos, CodigoDeCusto codigo)
    {
        if (baseDeCustos.TentarComposicao(codigo, out var composicao))
        {
            return composicao;
        }

        throw new ExcecaoDeDominio(
            baseDeCustos.TentarInsumo(codigo, out _)
                ? $"{codigo} é um insumo, e não uma composição: o custo dele é o preço."
                : $"Não existe a composição {codigo} nesta base.");
    }
}
