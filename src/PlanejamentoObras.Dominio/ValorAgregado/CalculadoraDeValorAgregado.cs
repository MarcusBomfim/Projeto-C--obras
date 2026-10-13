using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.ValorAgregado;

/// <summary>
/// Compara a execução medida com a linha de base: o gerenciamento do valor
/// agregado (EVM).
/// </summary>
/// <remarks>
/// <para>
/// Tudo a custo, e não a preço: o custo real se compara com o custo orçado do
/// que foi executado, e não com o preço de venda, que inclui o BDI. Com BDI de
/// 22,88 %, a obra exatamente no orçado mostraria IDC de 1,23 (ADR-0009).
/// Numa data de status — o mês de uma medição —, para cada item:
/// </para>
/// <list type="bullet">
/// <item><b>valor planejado</b>: a soma dos custos planejados da linha de base até a data de status;</item>
/// <item><b>valor agregado</b>: quantidade acumulada × custo unitário da linha de base,
/// ao centavo — o custo orçado do que foi executado;</item>
/// <item><b>custo real</b>: a soma dos custos lançados nas medições.</item>
/// </list>
/// <para>
/// Etapas e obra são somas dos itens. A data de status precisa ter medição:
/// mês sem medição é mês que ninguém mediu, e o valor agregado dele seria um
/// palpite (ADR-0009).
/// </para>
/// </remarks>
public static class CalculadoraDeValorAgregado
{
    /// <summary>A análise na última medição.</summary>
    public static AnaliseDeValorAgregado Calcular(Acompanhamento acompanhamento)
    {
        ArgumentNullException.ThrowIfNull(acompanhamento);

        if (acompanhamento.Medicoes.Count == 0)
        {
            throw new ExcecaoDeDominio("Ainda não há medição: sem medição não há valor agregado.");
        }

        return Calcular(acompanhamento, acompanhamento.Medicoes[^1].Mes);
    }

    /// <summary>A análise no mês de uma medição; qualquer dia do mês serve.</summary>
    public static AnaliseDeValorAgregado Calcular(Acompanhamento acompanhamento, DateOnly mes)
    {
        ArgumentNullException.ThrowIfNull(acompanhamento);

        var medicao = acompanhamento.MedicaoDoMes(mes);
        var linhaDeBase = acompanhamento.LinhaDeBase;
        var (agregado, real) = Acumulados(acompanhamento, medicao.Numero);

        var linhas = linhaDeBase.Linhas
            .Select(linha => new LinhaDoValorAgregado(
                linha.Numero,
                linha.Descricao,
                new IndicadoresDeValorAgregado(
                    linha.CustoTotal,
                    linhaDeBase.CustoPlanejadoAcumulado(linha, medicao.Mes),
                    agregado[linha.Numero],
                    real[linha.Numero])))
            .ToList();

        var primeiroNivel = linhas.Where(linha => linha.Nivel == 1).Select(linha => linha.Indicadores).ToList();

        var obra = new IndicadoresDeValorAgregado(
            linhaDeBase.CustoTotal,
            linhaDeBase.CustoPlanejadoAcumulado(medicao.Mes),
            primeiroNivel.Aggregate(Dinheiro.Zero, (soma, linha) => soma + linha.ValorAgregado),
            primeiroNivel.Aggregate(Dinheiro.Zero, (soma, linha) => soma + linha.CustoReal));

        return new AnaliseDeValorAgregado(
            medicao.Numero,
            medicao.Mes,
            obra,
            linhas,
            Prazo(linhaDeBase, medicao.Mes, obra.ValorAgregado),
            Curva(acompanhamento, medicao));
    }

    /// <summary>Valor agregado e custo real acumulados de cada linha, até a medição informada.</summary>
    private static (Dictionary<NumeroDaEap, Dinheiro> Agregado, Dictionary<NumeroDaEap, Dinheiro> Real) Acumulados(
        Acompanhamento acompanhamento,
        int ateAMedicao)
    {
        var linhaDeBase = acompanhamento.LinhaDeBase;
        var quantidades = acompanhamento.QuantidadesAcumuladas(ateAMedicao);
        var custos = acompanhamento.CustosAcumulados(ateAMedicao);
        var agregado = linhaDeBase.Linhas.ToDictionary(linha => linha.Numero, _ => Dinheiro.Zero);
        var real = linhaDeBase.Linhas.ToDictionary(linha => linha.Numero, _ => Dinheiro.Zero);

        foreach (var item in linhaDeBase.Itens)
        {
            var valor = (item.CustoUnitario * quantidades.GetValueOrDefault(item.Numero)).EmCentavos();
            var custo = custos.GetValueOrDefault(item.Numero);

            foreach (var numero in item.Numero.Ancestrais().Append(item.Numero))
            {
                agregado[numero] += valor;
                real[numero] += custo;
            }
        }

        return (agregado, real);
    }

    private static PrazoAgregado Prazo(LinhaDeBase linhaDeBase, DateOnly status, Dinheiro valorAgregado)
    {
        var meses = linhaDeBase.Meses;
        var planejado = meses.Select(linhaDeBase.CustoPlanejadoAcumulado).ToList();

        // C: quantos meses do plano o valor agregado já cobre. O planejado
        // acumulado só cresce, então os meses cobertos são um prefixo.
        var cobertos = 0;

        while (cobertos < meses.Count && planejado[cobertos] <= valorAgregado)
        {
            cobertos++;
        }

        decimal prazoAgregado = cobertos;

        if (cobertos < meses.Count)
        {
            var ateAqui = cobertos == 0 ? Dinheiro.Zero : planejado[cobertos - 1];
            prazoAgregado += (valorAgregado - ateAqui) / (planejado[cobertos] - ateAqui);
        }

        var decorrido = Indice(status) - Indice(meses[0]) + 1;

        return new PrazoAgregado(meses.Count, decorrido, prazoAgregado);
    }

    /// <summary>
    /// As curvas S, do primeiro mês da linha de base — ou da primeira medição,
    /// se veio antes — ao último dela, ou à data de status, se vier depois.
    /// </summary>
    private static List<PontoDaCurva> Curva(Acompanhamento acompanhamento, Medicao status)
    {
        var linhaDeBase = acompanhamento.LinhaDeBase;
        var medicoes = acompanhamento.Medicoes.Take(status.Numero).ToList();
        var primeiro = Min(linhaDeBase.Meses[0], medicoes[0].Mes);
        var ultimo = Max(linhaDeBase.Meses[^1], status.Mes);

        // Valor agregado e custo real da obra depois de cada medição até a de status.
        var depoisDe = Enumerable.Range(1, medicoes.Count)
            .Select(numero =>
            {
                var (agregado, real) = Acumulados(acompanhamento, numero);
                var primeiroNivel = linhaDeBase.Linhas.Where(linha => linha.Nivel == 1).ToList();

                return (
                    Agregado: primeiroNivel.Aggregate(Dinheiro.Zero, (soma, linha) => soma + agregado[linha.Numero]),
                    Real: primeiroNivel.Aggregate(Dinheiro.Zero, (soma, linha) => soma + real[linha.Numero]));
            })
            .ToList();

        var curva = new List<PontoDaCurva>();

        for (var mes = primeiro; mes <= ultimo; mes = mes.AddMonths(1))
        {
            var planejado = linhaDeBase.CustoPlanejadoAcumulado(mes);

            if (mes > status.Mes)
            {
                curva.Add(new PontoDaCurva(mes, planejado, null, null));
                continue;
            }

            // Mês sem medição repete o acumulado da última medição antes dele.
            var medidas = medicoes.Count(medicao => medicao.Mes <= mes);

            curva.Add(medidas == 0
                ? new PontoDaCurva(mes, planejado, Dinheiro.Zero, Dinheiro.Zero)
                : new PontoDaCurva(mes, planejado, depoisDe[medidas - 1].Agregado, depoisDe[medidas - 1].Real));
        }

        return curva;
    }

    private static int Indice(DateOnly mes) => (mes.Year * 12) + mes.Month;

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
