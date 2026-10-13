using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Testes.Orcamentos;

public sealed class BdiTests
{
    /// <summary>
    /// Parcelas típicas de construção de edifício: AC 4,00; S 0,40; R 1,27;
    /// G 0,40; DF 1,23; L 7,40; e tributos 6,15 (PIS 0,65 + COFINS 3,00 +
    /// ISS 2,50).
    /// </summary>
    private static ParcelasDoBdi ParcelasDeEdificio() => new(
        administracaoCentral: 4.00m,
        seguros: 0.40m,
        riscos: 1.27m,
        garantias: 0.40m,
        despesasFinanceiras: 1.23m,
        lucro: 7.40m,
        tributos: 6.15m);

    [Fact]
    public void Calcula_pela_formula_do_TCU()
    {
        /*
         * (1 + 0,0607) × (1 + 0,0123) × (1 + 0,0740) / (1 − 0,0615) − 1
         *   = 1,0607 × 1,0123 × 1,074 / 0,9385 − 1
         *   = 1,15320385914 / 0,9385 − 1
         *   = 0,22877342… → 22,88 %
         */
        var bdi = Bdi.PelaFormulaDoTcu(ParcelasDeEdificio());

        Assert.Equal(22.88m, bdi.Percentual);
        Assert.Equal(ParcelasDeEdificio(), bdi.Parcelas);
    }

    [Fact]
    public void Tributos_dividem_e_nao_somam()
    {
        // Só 10 % de tributo: 1 / 0,9 − 1 = 11,11 %, e não 10 %. Tributo
        // incide sobre o preço de venda, que já inclui o próprio tributo.
        var bdi = Bdi.PelaFormulaDoTcu(new ParcelasDoBdi(0, 0, 0, 0, 0, 0, tributos: 10m));

        Assert.Equal(11.11m, bdi.Percentual);
    }

    [Fact]
    public void Aplica_o_BDI_e_arredonda_o_preco_pela_NBR_5891()
    {
        // 57,14 × 1,25 = 71,425: meio centavo exato, e o 2 é par. Fica 71,42.
        Assert.Equal(Dinheiro.Reais(71.42m), Bdi.Informado(25m).Aplicar(Dinheiro.Reais(57.14m)));
    }

    [Fact]
    public void Aplica_o_percentual_de_duas_casas_e_nao_o_exato_da_formula()
    {
        /*
         * 1.000,00 × 1,2288 = 1.228,80. Com o fator exato da fórmula,
         * 1,22877342…, seria 1.228,77 — e quem conferisse com o BDI impresso,
         * 22,88 %, não chegaria a esse número.
         */
        var bdi = Bdi.PelaFormulaDoTcu(ParcelasDeEdificio());

        Assert.Equal(Dinheiro.Reais(1228.80m), bdi.Aplicar(Dinheiro.Reais(1000m)));
        Assert.Equal(Dinheiro.Reais(70.21m), bdi.Aplicar(Dinheiro.Reais(57.14m)));
    }

    [Fact]
    public void BDI_zero_nao_muda_o_custo()
    {
        Assert.Equal(Dinheiro.Reais(57.14m), Bdi.Zero.Aplicar(Dinheiro.Reais(57.14m)));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("100.01")]
    [InlineData("25.005")]
    public void Recusa_BDI_informado_fora_da_faixa_ou_com_casas_demais(string percentual)
    {
        var valor = decimal.Parse(percentual, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ExcecaoDeDominio>(() => Bdi.Informado(valor));
    }

    [Fact]
    public void Recusa_parcela_que_zeraria_o_divisor()
    {
        Assert.Throws<ExcecaoDeDominio>(() => new ParcelasDoBdi(0, 0, 0, 0, 0, 0, tributos: 100m));
    }

    [Fact]
    public void Recusa_parcela_com_mais_de_duas_casas()
    {
        Assert.Throws<ExcecaoDeDominio>(() => new ParcelasDoBdi(4.001m, 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void Recusa_parcelas_que_levariam_o_BDI_acima_de_100_por_cento()
    {
        var absurdas = new ParcelasDoBdi(50m, 0, 0, 0, 0, lucro: 50m, tributos: 30m);

        Assert.Throws<ExcecaoDeDominio>(() => Bdi.PelaFormulaDoTcu(absurdas));
    }

    [Fact]
    public void Escreve_como_vai_na_planilha()
    {
        Assert.Equal("22,88 %", Bdi.PelaFormulaDoTcu(ParcelasDeEdificio()).ToString());
        Assert.Equal("25,00 %", Bdi.Informado(25m).ToString());
    }

    [Fact]
    public void Informado_nao_tem_parcelas()
    {
        Assert.Null(Bdi.Informado(25m).Parcelas);
    }
}
