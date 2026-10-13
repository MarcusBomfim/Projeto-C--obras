using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Custos;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// Monta a planilha orçamentária: preço de cada item contra uma base de
/// custos, subtotais das etapas e total da obra.
/// </summary>
/// <remarks>
/// <para>
/// Por item, seguindo a ADR-0004 — arredonda-se o que aparece, e o número
/// seguinte sai do arredondado:
/// </para>
/// <list type="number">
/// <item>custo unitário: o da composição (já em centavos) ou o preço do insumo;</item>
/// <item>preço unitário: custo unitário × (1 + BDI), ao centavo;</item>
/// <item>total: quantidade × preço unitário, ao centavo.</item>
/// </list>
/// <para>
/// Subtotal de etapa e total da obra são somas de valores em centavos, e não
/// arredondam nada. A planilha fecha à mão em todas as linhas.
/// </para>
/// </remarks>
public static class CalculadoraDoOrcamento
{
    public static OrcamentoCalculado Calcular(Orcamento orcamento, BaseDeCustos baseDeCustos)
    {
        ArgumentNullException.ThrowIfNull(orcamento);
        ArgumentNullException.ThrowIfNull(baseDeCustos);

        var itens = ItensEmOrdem(orcamento).ToList();

        /*
         * Todos os códigos que faltam numa mensagem só. Acusar um por vez
         * obriga a corrigir, recalcular e descobrir o próximo — num orçamento
         * importado de outra base, dezenas de vezes.
         */
        var faltando = itens
            .Where(par => !baseDeCustos.TentarInsumo(par.Item.Codigo, out _) && !baseDeCustos.TentarComposicao(par.Item.Codigo, out _))
            .Select(par => $"{par.Numero} ({par.Item.Codigo})")
            .ToList();

        if (faltando.Count > 0)
        {
            throw new ExcecaoDeDominio(
                $"Há itens com código que não está na base {baseDeCustos.Nome}: {string.Join(", ", faltando)}.");
        }

        var composicoes = CalculadoraDeCustos.Calcular(
            baseDeCustos,
            itens
                .Select(par => par.Item.Codigo)
                .Where(codigo => baseDeCustos.TentarComposicao(codigo, out _))
                .Distinct()
                .Select(codigo => codigo.Valor));

        var precificados = itens.ToDictionary(
            par => par.Item,
            par => Precificar(par.Item, orcamento.Bdi, baseDeCustos, composicoes));

        var custoDireto = precificados.Values.Aggregate(Dinheiro.Zero, (soma, item) => soma + item.CustoTotal);
        var total = precificados.Values.Aggregate(Dinheiro.Zero, (soma, item) => soma + item.Total);

        var linhas = new List<LinhaDoOrcamento>();

        for (var posicao = 1; posicao <= orcamento.Etapas.Count; posicao++)
        {
            Montar(orcamento.Etapas[posicao - 1], NumeroDaEap.De([posicao]), precificados, total, linhas);
        }

        return new OrcamentoCalculado(
            orcamento.Nome,
            orcamento.Bdi,
            baseDeCustos.Nome,
            baseDeCustos.DataBase,
            linhas,
            custoDireto,
            total);
    }

    private static Precificado Precificar(
        ItemDoOrcamento item,
        Bdi bdiDoOrcamento,
        BaseDeCustos baseDeCustos,
        IReadOnlyDictionary<CodigoDeCusto, ComposicaoAnalitica> composicoes)
    {
        var (descricao, unidade, custoUnitario) = baseDeCustos.TentarInsumo(item.Codigo, out var insumo)
            ? (insumo.Descricao, insumo.Unidade, insumo.Preco)
            : (composicoes[item.Codigo].Descricao, composicoes[item.Codigo].Unidade, composicoes[item.Codigo].CustoUnitario);

        var bdi = item.BdiDiferenciado ?? bdiDoOrcamento;
        var precoUnitario = bdi.Aplicar(custoUnitario);

        return new Precificado(
            descricao,
            unidade,
            custoUnitario,
            bdi,
            precoUnitario,
            CustoTotal: (custoUnitario * item.Quantidade).EmCentavos(),
            Total: (precoUnitario * item.Quantidade).EmCentavos());
    }

    /// <summary>
    /// Acrescenta a linha do nó e as de tudo o que está dentro dele, na ordem
    /// da planilha, e devolve o custo e o total do nó.
    /// </summary>
    private static (Dinheiro Custo, Dinheiro Total) Montar(
        NoDaEap no,
        NumeroDaEap numero,
        Dictionary<ItemDoOrcamento, Precificado> precificados,
        Dinheiro totalDaObra,
        List<LinhaDoOrcamento> linhas)
    {
        if (no is ItemDoOrcamento item)
        {
            var preco = precificados[item];

            linhas.Add(new LinhaDeItem(
                numero,
                preco.Descricao,
                preco.CustoTotal,
                preco.Total,
                Peso(preco.Total, totalDaObra),
                item.Codigo,
                preco.Unidade,
                item.Quantidade,
                preco.CustoUnitario,
                preco.Bdi,
                preco.PrecoUnitario,
                item.Atividade));

            return (preco.CustoTotal, preco.Total);
        }

        // O subtotal só se conhece depois dos filhos, mas a linha da etapa
        // vem antes deles na planilha.
        var etapa = (Etapa)no;
        var abaixo = new List<LinhaDoOrcamento>();
        var custo = Dinheiro.Zero;
        var total = Dinheiro.Zero;

        for (var posicao = 1; posicao <= etapa.Filhos.Count; posicao++)
        {
            var (custoDoFilho, totalDoFilho) = Montar(etapa.Filhos[posicao - 1], numero.Filho(posicao), precificados, totalDaObra, abaixo);
            custo += custoDoFilho;
            total += totalDoFilho;
        }

        linhas.Add(new LinhaDeEtapa(numero, etapa.Titulo, custo, total, Peso(total, totalDaObra)));
        linhas.AddRange(abaixo);

        return (custo, total);
    }

    private static IEnumerable<(NumeroDaEap Numero, ItemDoOrcamento Item)> ItensEmOrdem(Orcamento orcamento)
    {
        for (var posicao = 1; posicao <= orcamento.Etapas.Count; posicao++)
        {
            foreach (var par in ItensEmOrdem(orcamento.Etapas[posicao - 1], NumeroDaEap.De([posicao])))
            {
                yield return par;
            }
        }
    }

    private static IEnumerable<(NumeroDaEap Numero, ItemDoOrcamento Item)> ItensEmOrdem(Etapa etapa, NumeroDaEap numero)
    {
        for (var posicao = 1; posicao <= etapa.Filhos.Count; posicao++)
        {
            var numeroDoFilho = numero.Filho(posicao);

            if (etapa.Filhos[posicao - 1] is ItemDoOrcamento item)
            {
                yield return (numeroDoFilho, item);
                continue;
            }

            foreach (var par in ItensEmOrdem((Etapa)etapa.Filhos[posicao - 1], numeroDoFilho))
            {
                yield return par;
            }
        }
    }

    private static decimal Peso(Dinheiro parte, Dinheiro todo) => todo == Dinheiro.Zero ? 0m : parte / todo;

    private sealed record Precificado(
        string Descricao,
        UnidadeDeMedida Unidade,
        Dinheiro CustoUnitario,
        Bdi Bdi,
        Dinheiro PrecoUnitario,
        Dinheiro CustoTotal,
        Dinheiro Total);
}
