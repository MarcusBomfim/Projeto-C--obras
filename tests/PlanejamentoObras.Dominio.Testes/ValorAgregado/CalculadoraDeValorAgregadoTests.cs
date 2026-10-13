using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;
using PlanejamentoObras.Dominio.ValorAgregado;

namespace PlanejamentoObras.Dominio.Testes.ValorAgregado;

/*
 * A CasaTerrea, planejada para outubro e novembro, executada em três meses.
 * Tudo a custo: o orçamento no término é o custo direto, 33.200,00 — o
 * contrato, com BDI de 25 %, é de 41.500,00.
 *
 *              planejado (VP)   agregado (VA)   custo real (CR)
 *   outubro         15.466,67       13.600,00         16.000,00
 *   novembro        33.200,00       24.800,00         29.400,00
 *   dezembro        33.200,00       33.200,00         39.950,00
 *
 * Em outubro, atrasada e acima do custo:
 *   IDP = 13.600 / 15.466,67 = 0,8793      IDC = 13.600 / 16.000 = 0,85
 *   ENT = 33.200 × 16.000 / 13.600 = 39.058,8235… → 39.058,82
 *   prazo agregado = 13.600 / 15.466,67 = 0,8793 mês, em 1 mês decorrido
 *
 * Em dezembro a obra acabou, um mês atrasada. O IDP clássico volta a 1 —
 * VA e VP chegam os dois ao orçamento —, mas o prazo agregado fica em 2
 * meses para 3 decorridos: IDP(t) = 0,67.
 */
public sealed class CalculadoraDeValorAgregadoTests
{
    private static readonly DateOnly Outubro = new(2026, 10, 1);
    private static readonly DateOnly Novembro = new(2026, 11, 1);
    private static readonly DateOnly Dezembro = new(2026, 12, 1);

    private static Dinheiro R(decimal valor) => Dinheiro.Reais(valor);

    private static Acompanhamento TresMedicoes()
    {
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());

        acompanhamento.RegistrarMedicao(
            Outubro,
            new LancamentoDeMedicao("1.1", 1m, 9_000m),
            new LancamentoDeMedicao("1.2", 1m, 800m),
            new LancamentoDeMedicao("2.1", 0.6m, 6_200m));

        acompanhamento.RegistrarMedicao(
            Novembro,
            new LancamentoDeMedicao("2.1", 0.4m, 3_900m),
            new LancamentoDeMedicao("2.2", 0.5m, 9_500m));

        acompanhamento.RegistrarMedicao(
            Dezembro,
            new LancamentoDeMedicao("2.2", 0.5m, 10_100m),
            new LancamentoDeMedicao("2.3", 1m, 450m));

        return acompanhamento;
    }

    private static AnaliseDeValorAgregado Em(DateOnly mes) => CalculadoraDeValorAgregado.Calcular(TresMedicoes(), mes);

    [Fact]
    public void Compara_planejado_agregado_e_custo_real_na_data_de_status()
    {
        var outubro = Em(Outubro);

        Assert.Equal((1, Outubro), (outubro.Medicao, outubro.Mes));
        Assert.Equal(R(33_200m), outubro.Obra.OrcamentoNoTermino);
        Assert.Equal(R(15_466.67m), outubro.Obra.ValorPlanejado);
        Assert.Equal(R(13_600m), outubro.Obra.ValorAgregado);
        Assert.Equal(R(16_000m), outubro.Obra.CustoReal);
    }

    [Fact]
    public void Tudo_a_custo_e_nao_a_preco()
    {
        /*
         * Em novembro o boletim mede 31.000,00 a preço de contrato; o valor
         * agregado é o custo orçado do mesmo executado, 24.800,00. Comparar o
         * custo real com o preço daria IDC de 31.000 / 29.400 = 1,05, e a obra
         * acima do custo pareceria abaixo dele.
         */
        var acompanhamento = TresMedicoes();
        var novembro = CalculadoraDeValorAgregado.Calcular(acompanhamento, Novembro);

        Assert.Equal(R(31_000m), EmissaoDoBoletim.Emitir(acompanhamento, 2).ValorAcumulado);
        Assert.Equal(R(24_800m), novembro.Obra.ValorAgregado);
        Assert.Equal(0.8435m, decimal.Round(novembro.Obra.IndiceDeDesempenhoDeCusto!.Value, 4));
    }

    [Fact]
    public void Variacoes_e_indices()
    {
        var obra = Em(Outubro).Obra;

        Assert.Equal(R(-1_866.67m), obra.VariacaoDePrazo);
        Assert.Equal(R(-2_400m), obra.VariacaoDeCusto);
        Assert.Equal(0.8793m, decimal.Round(obra.IndiceDeDesempenhoDePrazo!.Value, 4));
        Assert.Equal(0.85m, obra.IndiceDeDesempenhoDeCusto);
        Assert.Equal(0.4096m, decimal.Round(obra.PercentualExecutado, 4));
        Assert.Equal(0.4659m, decimal.Round(obra.PercentualPlanejado, 4));
    }

    [Fact]
    public void Estimativas_no_termino()
    {
        /*
         * Pelo desempenho de custo:  33.200 × 16.000 / 13.600 = 39.058,82
         * Com o restante no orçado:  16.000 + (33.200 − 13.600) = 35.600,00
         * Com custo e prazo:         16.000 + 19.600 × 16.000 × 15.466,67 / 13.600²
         *                          = 16.000 + 26.223,7657… = 42.223,77
         */
        var obra = Em(Outubro).Obra;

        Assert.Equal(R(39_058.82m), obra.EstimativaNoTermino);
        Assert.Equal(R(35_600m), obra.EstimativaNoTerminoComRestanteNoOrcamento);
        Assert.Equal(R(42_223.77m), obra.EstimativaNoTerminoComCustoEPrazo);
        Assert.Equal(R(23_058.82m), obra.EstimativaParaTerminar);
        Assert.Equal(R(-5_858.82m), obra.VariacaoNoTermino);
        Assert.Equal(1.1395m, decimal.Round(obra.IndiceDeDesempenhoParaTerminar!.Value, 4));
    }

    [Fact]
    public void Prazo_agregado_mede_o_atraso_em_meses()
    {
        var prazo = Em(Outubro).Prazo;

        Assert.Equal((2, 1), (prazo.DuracaoPlanejadaEmMeses, prazo.PrazoDecorridoEmMeses));
        Assert.Equal(0.8793m, decimal.Round(prazo.PrazoAgregadoEmMeses, 4));
        Assert.Equal(0.8793m, decimal.Round(prazo.IndiceDeDesempenhoDePrazo!.Value, 4));
        Assert.Equal(-0.1207m, decimal.Round(prazo.VariacaoDePrazoEmMeses, 4));
        Assert.Equal(2.2745m, decimal.Round(prazo.DuracaoEstimadaEmMeses!.Value, 4));
    }

    [Fact]
    public void Prazo_agregado_interpola_dentro_do_mes_seguinte()
    {
        // Em novembro, 24.800 cobre outubro (15.466,67) e 52,63 % de novembro:
        // 1 + 9.333,33 / 17.733,33 = 1,5263 mês.
        var novembro = Em(Novembro);

        Assert.Equal(1.5263m, decimal.Round(novembro.Prazo.PrazoAgregadoEmMeses, 4));
        Assert.Equal(0.7632m, decimal.Round(novembro.Prazo.IndiceDeDesempenhoDePrazo!.Value, 4));
        Assert.Equal(0.7470m, decimal.Round(novembro.Obra.IndiceDeDesempenhoDePrazo!.Value, 4));
    }

    [Fact]
    public void No_fim_o_IDP_classico_volta_a_1_e_o_prazo_agregado_nao()
    {
        var dezembro = Em(Dezembro);

        Assert.Equal(1m, dezembro.Obra.IndiceDeDesempenhoDePrazo);
        Assert.Equal(1m, dezembro.Obra.PercentualExecutado);
        Assert.Equal((2m, 3), (dezembro.Prazo.PrazoAgregadoEmMeses, dezembro.Prazo.PrazoDecorridoEmMeses));
        Assert.Equal(0.6667m, decimal.Round(dezembro.Prazo.IndiceDeDesempenhoDePrazo!.Value, 4));
        Assert.Equal(3m, dezembro.Prazo.DuracaoEstimadaEmMeses);
    }

    [Fact]
    public void Indicadores_por_etapa_mostram_onde_esta_o_problema()
    {
        // Em outubro, os serviços iniciais estão em dia; as fundações, atrasadas e bem acima do custo.
        var outubro = Em(Outubro);
        var iniciais = outubro.Linha("1").Indicadores;
        var fundacoes = outubro.Linha("2.1").Indicadores;

        Assert.Equal((R(8_800m), R(8_800m), R(9_800m)), (iniciais.ValorPlanejado, iniciais.ValorAgregado, iniciais.CustoReal));
        Assert.Equal(1m, iniciais.IndiceDeDesempenhoDePrazo);
        Assert.Equal((R(6_666.67m), R(4_800m), R(6_200m)), (fundacoes.ValorPlanejado, fundacoes.ValorAgregado, fundacoes.CustoReal));
        Assert.Equal(0.7742m, decimal.Round(fundacoes.IndiceDeDesempenhoDeCusto!.Value, 4));
    }

    [Fact]
    public void Sem_mes_informado_a_analise_e_na_ultima_medicao()
    {
        Assert.Equal(Dezembro, CalculadoraDeValorAgregado.Calcular(TresMedicoes()).Mes);
    }

    [Fact]
    public void Curvas_S_ate_a_data_de_status()
    {
        var curva = Em(Outubro).Curva;

        Assert.Equal([Outubro, Novembro], curva.Select(ponto => ponto.Mes));
        Assert.Equal(new PontoDaCurva(Outubro, R(15_466.67m), R(13_600m), R(16_000m)), curva[0]);
        Assert.Equal(new PontoDaCurva(Novembro, R(33_200m), null, null), curva[1]);
    }

    [Fact]
    public void Curva_passa_do_fim_da_linha_de_base_quando_a_obra_atrasa()
    {
        var curva = Em(Dezembro).Curva;

        Assert.Equal([Outubro, Novembro, Dezembro], curva.Select(ponto => ponto.Mes));
        Assert.Equal(new PontoDaCurva(Dezembro, R(33_200m), R(33_200m), R(39_950m)), curva[2]);
    }

    [Fact]
    public void Antes_de_a_linha_de_base_comecar_nao_ha_indice_de_prazo()
    {
        // A obra começou em setembro, antes do planejado: nada planejado, nada decorrido.
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());
        acompanhamento.RegistrarMedicao(new DateOnly(2026, 9, 1), new LancamentoDeMedicao("1.1", 0.5m, 4_000m));

        var setembro = CalculadoraDeValorAgregado.Calcular(acompanhamento);

        Assert.Equal(R(0m), setembro.Obra.ValorPlanejado);
        Assert.Equal(R(4_000m), setembro.Obra.ValorAgregado);
        Assert.Null(setembro.Obra.IndiceDeDesempenhoDePrazo);
        Assert.Equal(0, setembro.Prazo.PrazoDecorridoEmMeses);
        Assert.Null(setembro.Prazo.IndiceDeDesempenhoDePrazo);
        Assert.Equal(new DateOnly(2026, 9, 1), setembro.Curva[0].Mes);
    }

    [Fact]
    public void Sem_medicao_nao_ha_analise()
    {
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());

        Assert.Throws<ExcecaoDeDominio>(() => CalculadoraDeValorAgregado.Calcular(acompanhamento));
    }

    [Fact]
    public void Data_de_status_precisa_ter_medicao()
    {
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());
        acompanhamento.RegistrarMedicao(Outubro);
        acompanhamento.RegistrarMedicao(Dezembro);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => CalculadoraDeValorAgregado.Calcular(acompanhamento, Novembro));

        Assert.Equal("Não há medição registrada em 11/2026.", erro.Message);
    }
}
