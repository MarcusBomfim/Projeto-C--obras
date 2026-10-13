using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Testes.Orcamentos;

/*
 * A promessa da ADR-0004, conferida em 400 orçamentos sorteados: todo número
 * da planilha se refaz com uma calculadora a partir dos números que a própria
 * planilha mostra — da composição analítica ao total da obra.
 *
 * O arredondamento aqui é escrito de novo, com decimal.Round, e não com o
 * Dinheiro: o teste não confere o código usando o próprio código.
 */
public sealed class InvariantesDoOrcamentoTests
{
    [Theory]
    [MemberData(nameof(OrcamentosAleatorios.Lotes), MemberType = typeof(OrcamentosAleatorios))]
    public void Toda_planilha_fecha_a_mao(int lote)
    {
        foreach (var semente in OrcamentosAleatorios.SementesDoLote(lote))
        {
            var (baseDeCustos, orcamento) = OrcamentosAleatorios.Gerar(semente);
            var planilha = CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos);
            var analiticas = CalculadoraDeCustos.Calcular(
                baseDeCustos,
                baseDeCustos.Composicoes.Select(composicao => composicao.Codigo.Valor));

            ConferirComposicoes(baseDeCustos, analiticas, semente);
            ConferirItens(baseDeCustos, analiticas, orcamento, planilha, semente);
            ConferirSomas(planilha, semente);
            ConferirCurvaAbc(planilha, semente);
        }
    }

    private static decimal Centavos(decimal valor) => decimal.Round(valor, 2, MidpointRounding.ToEven);

    /// <summary>O custo unitário de um código, segundo a base: preço se é insumo, custo calculado se é composição.</summary>
    private static Dinheiro CustoNaBase(
        BaseDeCustos baseDeCustos,
        IReadOnlyDictionary<CodigoDeCusto, ComposicaoAnalitica> analiticas,
        CodigoDeCusto codigo) =>
        baseDeCustos.Insumos.Any(insumo => insumo.Codigo == codigo)
            ? baseDeCustos.Insumo(codigo.Valor).Preco
            : analiticas[codigo].CustoUnitario;

    private static void ConferirComposicoes(
        BaseDeCustos baseDeCustos,
        IReadOnlyDictionary<CodigoDeCusto, ComposicaoAnalitica> analiticas,
        int semente)
    {
        foreach (var analitica in analiticas.Values)
        {
            var onde = $"semente {semente}, composição {analitica.Codigo}";

            foreach (var linha in analitica.Linhas)
            {
                Assert.True(
                    CustoNaBase(baseDeCustos, analiticas, linha.Codigo) == linha.CustoUnitario,
                    $"{onde}: custo unitário de {linha.Codigo} não é o da base");
                Assert.True(
                    Centavos(linha.Coeficiente * linha.CustoUnitario.Valor) == linha.CustoParcial.Valor,
                    $"{onde}: {linha.Coeficiente} × {linha.CustoUnitario} não dá {linha.CustoParcial}");
            }

            Assert.True(
                analitica.Linhas.Sum(linha => linha.CustoParcial.Valor) == analitica.CustoUnitario.Valor,
                $"{onde}: as parcelas não somam o custo unitário");
        }
    }

    private static void ConferirItens(
        BaseDeCustos baseDeCustos,
        IReadOnlyDictionary<CodigoDeCusto, ComposicaoAnalitica> analiticas,
        Orcamento orcamento,
        OrcamentoCalculado planilha,
        int semente)
    {
        foreach (var linha in planilha.Itens)
        {
            var onde = $"semente {semente}, item {linha.Numero}";
            var item = orcamento.Item(linha.Numero.Valor);
            var bdi = item.BdiDiferenciado ?? orcamento.Bdi;

            Assert.Equal(item.Codigo, linha.Codigo);
            Assert.Equal(item.Quantidade, linha.Quantidade);
            Assert.Equal(bdi, linha.Bdi);
            Assert.True(CustoNaBase(baseDeCustos, analiticas, linha.Codigo) == linha.CustoUnitario, $"{onde}: custo unitário não é o da base");
            Assert.True(linha.CustoUnitario.EstaEmCentavos, $"{onde}: custo unitário fora do centavo");

            Assert.True(
                Centavos(linha.CustoUnitario.Valor * (1 + (bdi.Percentual / 100))) == linha.PrecoUnitario.Valor,
                $"{onde}: {linha.CustoUnitario} com BDI de {bdi} não dá {linha.PrecoUnitario}");

            Assert.True(
                Centavos(linha.Quantidade * linha.PrecoUnitario.Valor) == linha.Total.Valor,
                $"{onde}: {linha.Quantidade} × {linha.PrecoUnitario} não dá {linha.Total}");

            Assert.True(
                Centavos(linha.Quantidade * linha.CustoUnitario.Valor) == linha.CustoTotal.Valor,
                $"{onde}: {linha.Quantidade} × {linha.CustoUnitario} não dá {linha.CustoTotal}");
        }

        // Uma linha na planilha para cada etapa e cada item do orçamento.
        Assert.Equal(
            orcamento.Etapas.Count + orcamento.Etapas.Sum(Contar),
            planilha.Linhas.Count);
    }

    private static int Contar(Etapa etapa) => etapa.Filhos.Count + etapa.Filhos.OfType<Etapa>().Sum(Contar);

    private static void ConferirSomas(OrcamentoCalculado planilha, int semente)
    {
        foreach (var etapa in planilha.Linhas.OfType<LinhaDeEtapa>())
        {
            var filhos = planilha.Linhas
                .Where(linha => linha.Nivel == etapa.Nivel + 1 && linha.Numero.Valor.StartsWith(etapa.Numero.Valor + ".", StringComparison.Ordinal))
                .ToList();

            Assert.True(filhos.Sum(linha => linha.Total.Valor) == etapa.Total.Valor, $"semente {semente}: a etapa {etapa.Numero} não soma os filhos");
            Assert.True(filhos.Sum(linha => linha.CustoTotal.Valor) == etapa.CustoTotal.Valor, $"semente {semente}: o custo da etapa {etapa.Numero} não soma os filhos");
        }

        var primeiroNivel = planilha.Linhas.Where(linha => linha.Nivel == 1).ToList();

        Assert.Equal(planilha.Itens.Sum(item => item.Total.Valor), planilha.Total.Valor);
        Assert.Equal(primeiroNivel.Sum(linha => linha.Total.Valor), planilha.Total.Valor);
        Assert.Equal(planilha.Itens.Sum(item => item.CustoTotal.Valor), planilha.CustoDireto.Valor);
        Assert.Equal(primeiroNivel.Sum(linha => linha.CustoTotal.Valor), planilha.CustoDireto.Valor);
        Assert.All(planilha.Linhas, linha => Assert.True(linha.Total.EstaEmCentavos && linha.CustoTotal.EstaEmCentavos));
    }

    private static void ConferirCurvaAbc(OrcamentoCalculado planilha, int semente)
    {
        var curva = planilha.CurvaAbc();

        Assert.Equal(planilha.Itens.Count, curva.Count);

        if (curva.Count == 0)
        {
            return;
        }

        for (var i = 1; i < curva.Count; i++)
        {
            Assert.True(curva[i].Total <= curva[i - 1].Total, $"semente {semente}: curva ABC fora de ordem");
            Assert.True(curva[i].PesoAcumulado >= curva[i - 1].PesoAcumulado, $"semente {semente}: acumulado que desce");
            Assert.True(curva[i].Classe >= curva[i - 1].Classe, $"semente {semente}: classe que volta");
        }

        Assert.Equal(ClasseAbc.A, curva[0].Classe);

        if (planilha.Total > Dinheiro.Zero)
        {
            Assert.Equal(1m, curva[^1].PesoAcumulado);
        }
    }
}
