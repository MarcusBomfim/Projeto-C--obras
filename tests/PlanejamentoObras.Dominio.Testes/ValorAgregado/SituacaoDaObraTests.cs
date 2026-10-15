using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.ValorAgregado;

namespace PlanejamentoObras.Dominio.Testes.ValorAgregado;

/*
 * A régua da situação da obra: o pior dos dois índices decide, com as faixas
 * em 0,95 e 0,85 — fronteiras incluídas na faixa de cima.
 */
public sealed class SituacaoDaObraTests
{
    private static IndicadoresDeValorAgregado Indicadores(decimal planejado, decimal agregado, decimal real) =>
        new(Dinheiro.Reais(1_000_000m), Dinheiro.Reais(planejado), Dinheiro.Reais(agregado), Dinheiro.Reais(real));

    [Theory]
    [InlineData(100, 100, 100, SituacaoDaObra.EmDia)]
    [InlineData(100, 95, 95, SituacaoDaObra.EmDia)]
    [InlineData(100, 94.99, 94.99, SituacaoDaObra.Atencao)]
    [InlineData(100, 85, 85, SituacaoDaObra.Atencao)]
    [InlineData(100, 84.99, 84.99, SituacaoDaObra.Critica)]
    [InlineData(100, 120, 200, SituacaoDaObra.Critica)]
    [InlineData(200, 120, 100, SituacaoDaObra.Critica)]
    public void O_pior_indice_decide(decimal planejado, decimal agregado, decimal real, SituacaoDaObra esperada)
    {
        Assert.Equal(esperada, ClassificacaoDaObra.Classificar(Indicadores(planejado, agregado, real)));
    }

    [Fact]
    public void Indice_sem_base_nao_acusa_nada()
    {
        // Nada planejado nem gasto ainda: os dois índices são nulos.
        Assert.Equal(SituacaoDaObra.EmDia, ClassificacaoDaObra.Classificar(Indicadores(0, 0, 0)));
    }

    [Fact]
    public void A_obra_do_README_em_novembro_esta_critica()
    {
        // IDP 0,74 e IDC 0,96: o prazo decide.
        Assert.Equal(SituacaoDaObra.Critica, ClassificacaoDaObra.Classificar(Indicadores(563_875.75m, 416_697.00m, 432_700.00m)));
    }
}
