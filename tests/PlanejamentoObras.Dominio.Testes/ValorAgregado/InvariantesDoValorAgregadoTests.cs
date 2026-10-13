using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Testes.Cronogramas;
using PlanejamentoObras.Dominio.ValorAgregado;

namespace PlanejamentoObras.Dominio.Testes.ValorAgregado;

/*
 * Uma obra sorteada, a sua linha de base e medições sorteadas — com meses
 * pulados, glosas, obra que começa antes e que termina depois do planejado.
 * Em cada medição, a análise de valor agregado tem que bater com a linha de
 * base e com as próprias definições — tudo a custo.
 *
 * Os acumulados e os valores são refeitos aqui a partir dos lançamentos, com
 * decimal.Round, sem usar o código que está sendo conferido.
 */
public sealed class InvariantesDoValorAgregadoTests
{
    [Theory]
    [MemberData(nameof(RedesAleatorias.Lotes), MemberType = typeof(RedesAleatorias))]
    public void Toda_analise_fecha_com_a_linha_de_base_e_as_definicoes(int lote)
    {
        foreach (var semente in RedesAleatorias.SementesDoLote(lote))
        {
            var (obra, planilha) = ObrasAleatorias.Gerar(semente);

            if (planilha.Itens.Count == 0)
            {
                continue;
            }

            var datas = semente % 2 == 0 ? DatasDoCronograma.Cedo : DatasDoCronograma.Tarde;
            var acompanhamento = new Acompanhamento(LinhaDeBase.Estabelecer("LB", obra.DataDeInicio, planilha, obra, datas));
            var terminou = MedicoesAleatorias.Registrar(acompanhamento, semente);

            foreach (var medicao in acompanhamento.Medicoes)
            {
                var onde = $"semente {semente}, medição {medicao.Numero}";
                var analise = CalculadoraDeValorAgregado.Calcular(acompanhamento, medicao.Mes);

                ConferirLinhas(acompanhamento, medicao, analise, onde);
                ConferirObra(acompanhamento, medicao, analise, onde);
                ConferirPrazo(acompanhamento.LinhaDeBase, analise, onde);
                ConferirCurva(analise, onde);
            }

            if (terminou)
            {
                var final = CalculadoraDeValorAgregado.Calcular(acompanhamento);

                Assert.Equal(acompanhamento.LinhaDeBase.CustoTotal, final.Obra.ValorAgregado);
                Assert.Equal(acompanhamento.LinhaDeBase.Meses.Count, final.Prazo.PrazoAgregadoEmMeses);
            }
        }
    }

    private static void ConferirLinhas(Acompanhamento acompanhamento, Medicao status, AnaliseDeValorAgregado analise, string onde)
    {
        var linhaDeBase = acompanhamento.LinhaDeBase;
        var lancados = acompanhamento.Medicoes.Take(status.Numero).SelectMany(medicao => medicao.Itens).ToList();
        var mesesAteOStatus = linhaDeBase.Meses.Count(mes => mes <= status.Mes);

        foreach (var linha in linhaDeBase.Linhas)
        {
            var aqui = $"{onde}, linha {linha.Numero}";
            var indicadores = analise.Linha(linha.Numero.Valor).Indicadores;

            Assert.Equal(linha.CustoTotal, indicadores.OrcamentoNoTermino);
            Assert.True(
                linha.CustosPlanejados.Take(mesesAteOStatus).Sum(custo => custo.Valor) == indicadores.ValorPlanejado.Valor,
                $"{aqui}: valor planejado diferente da soma dos custos planejados dos meses");

            if (linha is ItemPlanejado item)
            {
                var quantidade = lancados.Where(lancado => lancado.Item == item.Numero).Sum(lancado => lancado.Quantidade);
                var custo = lancados.Where(lancado => lancado.Item == item.Numero).Sum(lancado => lancado.CustoReal.Valor);
                var valor = decimal.Round(quantidade * item.CustoUnitario.Valor, 2, MidpointRounding.ToEven);

                Assert.True(valor == indicadores.ValorAgregado.Valor, $"{aqui}: {quantidade} × {item.CustoUnitario} não dá {indicadores.ValorAgregado}");
                Assert.True(custo == indicadores.CustoReal.Valor, $"{aqui}: custo real diferente do lançado");
                Assert.True(indicadores.ValorAgregado >= Dinheiro.Zero && indicadores.ValorAgregado <= item.CustoTotal, $"{aqui}: valor agregado fora de 0 a ONT");

                continue;
            }

            var filhos = analise.Linhas
                .Where(outra => outra.Nivel == linha.Nivel + 1 && outra.Numero.Valor.StartsWith(linha.Numero.Valor + ".", StringComparison.Ordinal))
                .Select(filho => filho.Indicadores)
                .ToList();

            Assert.True(filhos.Sum(filho => filho.ValorAgregado.Valor) == indicadores.ValorAgregado.Valor, $"{aqui}: VA não soma os filhos");
            Assert.True(filhos.Sum(filho => filho.CustoReal.Valor) == indicadores.CustoReal.Valor, $"{aqui}: CR não soma os filhos");
            Assert.True(filhos.Sum(filho => filho.ValorPlanejado.Valor) == indicadores.ValorPlanejado.Valor, $"{aqui}: VP não soma os filhos");
        }
    }

    private static void ConferirObra(Acompanhamento acompanhamento, Medicao status, AnaliseDeValorAgregado analise, string onde)
    {
        var obra = analise.Obra;
        var primeiroNivel = analise.Linhas.Where(linha => linha.Nivel == 1).Select(linha => linha.Indicadores).ToList();

        Assert.Equal(acompanhamento.LinhaDeBase.CustoTotal, obra.OrcamentoNoTermino);
        Assert.Equal(acompanhamento.LinhaDeBase.CustoPlanejadoAcumulado(status.Mes), obra.ValorPlanejado);
        Assert.Equal(primeiroNivel.Sum(linha => linha.ValorAgregado.Valor), obra.ValorAgregado.Valor);
        Assert.Equal(primeiroNivel.Sum(linha => linha.CustoReal.Valor), obra.CustoReal.Valor);

        // O boletim mede a preço; o valor agregado é a custo, e nunca passa do medido.
        Assert.True(obra.ValorAgregado <= EmissaoDoBoletim.Emitir(acompanhamento, status.Numero).ValorAcumulado, $"{onde}: VA a custo acima do medido a preço");

        Assert.Equal(obra.ValorAgregado - obra.ValorPlanejado, obra.VariacaoDePrazo);
        Assert.Equal(obra.ValorAgregado - obra.CustoReal, obra.VariacaoDeCusto);
        Assert.Equal(obra.ValorPlanejado == Dinheiro.Zero ? null : obra.ValorAgregado / obra.ValorPlanejado, obra.IndiceDeDesempenhoDePrazo);
        Assert.Equal(obra.CustoReal == Dinheiro.Zero ? null : obra.ValorAgregado / obra.CustoReal, obra.IndiceDeDesempenhoDeCusto);

        if (obra.EstimativaNoTermino is { } estimativa)
        {
            var exata = obra.OrcamentoNoTermino.Valor * obra.CustoReal.Valor / obra.ValorAgregado.Valor;

            Assert.True(Math.Abs(estimativa.Valor - exata) <= 0.005m, $"{onde}: ENT {estimativa} longe de ONT × CR / VA = {exata}");
            Assert.Equal(estimativa - obra.CustoReal, obra.EstimativaParaTerminar);
        }
    }

    private static void ConferirPrazo(LinhaDeBase linhaDeBase, AnaliseDeValorAgregado analise, string onde)
    {
        var prazo = analise.Prazo;
        var duracao = linhaDeBase.Meses.Count;
        var decorrido = ((analise.Mes.Year - linhaDeBase.Meses[0].Year) * 12) + analise.Mes.Month - linhaDeBase.Meses[0].Month + 1;

        Assert.Equal(duracao, prazo.DuracaoPlanejadaEmMeses);
        Assert.Equal(decorrido, prazo.PrazoDecorridoEmMeses);
        Assert.True(prazo.PrazoAgregadoEmMeses >= 0 && prazo.PrazoAgregadoEmMeses <= duracao, $"{onde}: prazo agregado fora de 0 a {duracao}");

        /*
         * Dentro do período planejado, o prazo agregado e o valor agregado
         * contam a mesma história: adiantado em tempo exatamente quando
         * adiantado em dinheiro.
         */
        if (decorrido >= 1 && decorrido <= duracao)
        {
            Assert.True(
                (prazo.PrazoAgregadoEmMeses >= decorrido) == (analise.Obra.ValorAgregado >= analise.Obra.ValorPlanejado),
                $"{onde}: prazo agregado {prazo.PrazoAgregadoEmMeses} em {decorrido} meses, VA {analise.Obra.ValorAgregado}, VP {analise.Obra.ValorPlanejado}");
        }
    }

    private static void ConferirCurva(AnaliseDeValorAgregado analise, string onde)
    {
        var curva = analise.Curva;

        Assert.All(curva.Skip(1).Zip(curva), par =>
        {
            Assert.Equal(par.Second.Mes.AddMonths(1), par.First.Mes);
            Assert.True(par.First.ValorPlanejado >= par.Second.ValorPlanejado, $"{onde}: VP que desce na curva");
        });

        Assert.All(curva, ponto => Assert.Equal(ponto.Mes <= analise.Mes, ponto.ValorAgregado is not null));

        var status = curva.Single(ponto => ponto.Mes == analise.Mes);

        Assert.Equal(analise.Obra.ValorAgregado, status.ValorAgregado);
        Assert.Equal(analise.Obra.CustoReal, status.CustoReal);
    }
}
