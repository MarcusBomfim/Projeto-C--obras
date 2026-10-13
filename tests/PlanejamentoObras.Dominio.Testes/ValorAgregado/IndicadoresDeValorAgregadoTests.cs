using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.ValorAgregado;

namespace PlanejamentoObras.Dominio.Testes.ValorAgregado;

public sealed class IndicadoresDeValorAgregadoTests
{
    private static Dinheiro R(decimal valor) => Dinheiro.Reais(valor);

    [Fact]
    public void Sem_valor_planejado_nao_ha_indice_de_prazo()
    {
        var indicadores = new IndicadoresDeValorAgregado(R(100m), R(0m), R(10m), R(10m));

        Assert.Null(indicadores.IndiceDeDesempenhoDePrazo);
        Assert.Null(indicadores.EstimativaNoTerminoComCustoEPrazo);
    }

    [Fact]
    public void Sem_custo_real_nao_ha_indice_de_custo_nem_estimativa_por_ele()
    {
        var indicadores = new IndicadoresDeValorAgregado(R(100m), R(10m), R(10m), R(0m));

        Assert.Null(indicadores.IndiceDeDesempenhoDeCusto);
        Assert.Null(indicadores.EstimativaNoTermino);
        Assert.Null(indicadores.EstimativaParaTerminar);
        Assert.Null(indicadores.VariacaoNoTermino);
        Assert.Equal(R(90m), indicadores.EstimativaNoTerminoComRestanteNoOrcamento);
    }

    [Fact]
    public void Sem_nada_executado_nao_ha_estimativa_pelo_desempenho()
    {
        // Gastou 30 e não entregou nada: o desempenho de custo é zero, e ONT / 0 não existe.
        var indicadores = new IndicadoresDeValorAgregado(R(100m), R(20m), R(0m), R(30m));

        Assert.Equal(0m, indicadores.IndiceDeDesempenhoDeCusto);
        Assert.Null(indicadores.EstimativaNoTermino);
        Assert.Equal(R(130m), indicadores.EstimativaNoTerminoComRestanteNoOrcamento);
    }

    [Fact]
    public void Orcamento_ja_gasto_nao_tem_indice_para_terminar()
    {
        var indicadores = new IndicadoresDeValorAgregado(R(100m), R(80m), R(60m), R(100m));

        Assert.Null(indicadores.IndiceDeDesempenhoParaTerminar);
    }

    [Fact]
    public void Obra_abaixo_do_custo_tem_variacao_no_termino_positiva()
    {
        // Executou 50 gastando 40: IDC 1,25, ENT 100 × 40 / 50 = 80, sobra 20.
        var indicadores = new IndicadoresDeValorAgregado(R(100m), R(50m), R(50m), R(40m));

        Assert.Equal(1.25m, indicadores.IndiceDeDesempenhoDeCusto);
        Assert.Equal(R(80m), indicadores.EstimativaNoTermino);
        Assert.Equal(R(40m), indicadores.EstimativaParaTerminar);
        Assert.Equal(R(20m), indicadores.VariacaoNoTermino);
        Assert.Equal(50m / 60m, indicadores.IndiceDeDesempenhoParaTerminar);
    }

    [Fact]
    public void Orcamento_zero_nao_divide_por_zero()
    {
        var indicadores = new IndicadoresDeValorAgregado(R(0m), R(0m), R(0m), R(0m));

        Assert.Equal(0m, indicadores.PercentualExecutado);
        Assert.Equal(0m, indicadores.PercentualPlanejado);
    }
}
