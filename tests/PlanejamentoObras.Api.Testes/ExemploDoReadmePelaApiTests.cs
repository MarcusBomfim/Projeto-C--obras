using PlanejamentoObras.Api.Recursos.Acompanhamentos;
using PlanejamentoObras.Api.Recursos.Custos;
using PlanejamentoObras.Api.Recursos.LinhasDeBase;
using PlanejamentoObras.Api.Recursos.Orcamentos;
using PlanejamentoObras.Api.Recursos.Painel;
using PlanejamentoObras.Api.Seguranca;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Dominio.ValorAgregado;

namespace PlanejamentoObras.Api.Testes;

/*
 * O exemplo do README, de ponta a ponta, pela API — cada passo por quem tem
 * o papel para ele. O domínio já confere esses números; aqui se confere que
 * eles atravessam o HTTP, o JSON e o banco sem mudar um centavo — e chegam
 * ao painel das obras pela caixa de saída e pelo processador.
 */
public sealed class ExemploDoReadmePelaApiTests(ApiDeTestes api)
{
    [Fact]
    public async Task Da_base_de_custos_ao_painel_das_obras_com_os_numeros_do_README()
    {
        var empresa = Guid.CreateVersion7();
        var orcamentista = api.Cliente(empresa, Papeis.Orcamentista);
        var planejador = api.Cliente(empresa, Papeis.Planejador);
        var gestor = api.Cliente(empresa, Papeis.Gestor);
        var engenheiro = api.Cliente(empresa, Papeis.Engenheiro);

        // A base, o orçamento e o cronograma, cada um por quem cuida dele.
        var baseDeCustos = await orcamentista.Criar("/api/bases-de-custos", ObraDoReadme.BaseDeCustos()).IdCriado();
        var orcamento = await orcamentista.Criar("/api/orcamentos", ObraDoReadme.Orcamento()).IdCriado();
        var cronograma = await planejador.Criar("/api/cronogramas", RedeDoReadme.Cronograma()).IdCriado();

        var alvenaria = await orcamentista.LerComSucesso<ComposicaoAnaliticaResposta>($"/api/bases-de-custos/{baseDeCustos}/composicoes/ALV");
        Assert.Equal(57.14m, alvenaria.CustoUnitario);

        // A planilha e a curva ABC.
        var calculo = $"/api/orcamentos/{orcamento}";
        var planilha = await orcamentista.LerComSucesso<PlanilhaResposta>($"{calculo}/planilha?baseDeCustos={baseDeCustos}");

        Assert.Equal((22.88m, 1_388_822.24m, 1_667_428.66m), (planilha.Bdi.Percentual, planilha.CustoDireto, planilha.Total));
        Assert.Equal(
            [4_950.00m, 627_302.40m, 447_041.86m, 184_934.40m, 403_200.00m],
            planilha.Linhas.OfType<EtapaDaPlanilhaResposta>().Select(etapa => etapa.Total));
        var alvenariaNaPlanilha = planilha.Linhas.OfType<ItemDaPlanilhaResposta>().Single(item => item.Numero == "3.1");
        Assert.Equal((70.21m, 170_645.40m), (alvenariaNaPlanilha.PrecoUnitario, alvenariaNaPlanilha.Total));

        var curva = await orcamentista.LerComSucesso<List<ItemDaCurvaAbcResposta>>($"{calculo}/curva-abc?baseDeCustos={baseDeCustos}");

        Assert.Equal(["2.2", "5.1", "3.2", "3.1", "2.1", "4.1", "4.2", "1.1"], curva.Select(item => item.Numero));
        Assert.Equal(
            [ClasseAbc.A, ClasseAbc.A, ClasseAbc.A, ClasseAbc.A, ClasseAbc.B, ClasseAbc.B, ClasseAbc.B, ClasseAbc.C],
            curva.Select(item => item.Classe));

        // O físico-financeiro, pelas datas cedo e tarde.
        var noCalendario = $"{calculo}/fisico-financeiro?baseDeCustos={baseDeCustos}&cronograma={cronograma}&inicio={RedeDoReadme.Inicio}&{RedeDoReadme.Feriados}";
        var cedo = await orcamentista.LerComSucesso<FisicoFinanceiroResposta>(noCalendario);
        var tarde = await orcamentista.LerComSucesso<FisicoFinanceiroResposta>($"{noCalendario}&datas=Tarde");

        Assert.Equal([227_239.92m, 465_652.92m, 518_743.18m, 455_792.64m], cedo.Periodos.Select(periodo => periodo.Valor));
        Assert.Equal([227_239.92m, 439_141.56m, 545_254.54m, 455_792.64m], tarde.Periodos.Select(periodo => periodo.Valor));

        // O gestor aprova a linha de base; o engenheiro mede a obra contra ela.
        var linhaDeBase = await gestor.Criar("/api/linhas-de-base", ObraDoReadme.LinhaDeBase(orcamento, baseDeCustos, cronograma)).IdCriado();
        var plano = await gestor.LerComSucesso<LinhaDeBaseResposta>($"/api/linhas-de-base/{linhaDeBase}");

        Assert.Equal((1_667_428.66m, 1_388_822.24m), (plano.Total, plano.CustoTotal));
        Assert.Equal((new DateOnly(2026, 10, 5), new DateOnly(2027, 1, 7)), (plano.InicioPlanejado, plano.TerminoPlanejado));

        var acompanhamento = await engenheiro.Criar("/api/acompanhamentos", new { linhaDeBase }).IdCriado();
        var medicoes = $"/api/acompanhamentos/{acompanhamento}/medicoes";
        await engenheiro.Enviar(HttpMethod.Post, medicoes, ObraDoReadme.MedicaoDeOutubro, versao: 1).ComSucesso();
        await engenheiro.Enviar(HttpMethod.Post, medicoes, ObraDoReadme.MedicaoDeNovembro, versao: 2).ComSucesso();

        // O boletim de novembro, a preço de contrato.
        var boletim = await engenheiro.LerComSucesso<BoletimResposta>($"/api/acompanhamentos/{acompanhamento}/boletins/2");

        Assert.Equal(
            [278_446.08m, 21_063.00m, 10_604.54m],
            boletim.Linhas.OfType<ItemDoBoletimResposta>().Where(item => item.QuantidadeNoMes != 0).Select(item => item.ValorNoMes));
        Assert.Equal((310_113.62m, 512_040.26m), (boletim.ValorNoMes, boletim.ValorAcumulado));

        // E o valor agregado de novembro, a custo.
        var analise = await gestor.LerComSucesso<ValorAgregadoResposta>($"/api/acompanhamentos/{acompanhamento}/valor-agregado");
        var obra = analise.Obra;

        Assert.Equal(
            (1_388_822.24m, 563_875.75m, 416_697.00m, 432_700.00m),
            (obra.OrcamentoNoTermino, obra.ValorPlanejado, obra.ValorAgregado, obra.CustoReal));
        Assert.Equal(
            (0.74m, 0.96m),
            (decimal.Round(obra.IndiceDeDesempenhoDePrazo!.Value, 2), decimal.Round(obra.IndiceDeDesempenhoDeCusto!.Value, 2)));
        Assert.Equal((1_442_159.13m, -53_336.89m), (obra.EstimativaNoTermino, obra.VariacaoNoTermino));
        Assert.Equal(
            (1.61m, 4.96m),
            (decimal.Round(analise.Prazo.PrazoAgregadoEmMeses, 2), decimal.Round(analise.Prazo.DuracaoEstimadaEmMeses!.Value, 2)));
        Assert.Equal(0.40m, decimal.Round(analise.Linhas.Single(linha => linha.Numero == "4").Indicadores.IndiceDeDesempenhoDePrazo!.Value, 2));

        // E o painel das obras, quando o processador trata as mensagens das
        // medições: a obra está crítica — o prazo decide.
        ObraNoPainelResposta? noPainel = null;
        await api.ProcessarAte(
            async () => (noPainel = await gestor.LerDoPainel(acompanhamento))?.VersaoDoAcompanhamento == 3,
            "a medição de novembro chegar ao painel");

        Assert.Equal(
            (SituacaoDaObra.Critica, "Linha de base 1 — contrato", (int?)2, (DateOnly?)new DateOnly(2026, 11, 1)),
            (noPainel!.Situacao, noPainel.Obra, noPainel.Medicao, noPainel.Mes));
        Assert.Equal(
            (1_388_822.24m, (decimal?)563_875.75m, (decimal?)416_697.00m, (decimal?)432_700.00m, (decimal?)1_442_159.13m, (decimal?)-53_336.89m),
            (noPainel.OrcamentoNoTermino, noPainel.ValorPlanejado, noPainel.ValorAgregado, noPainel.CustoReal, noPainel.EstimativaNoTermino, noPainel.VariacaoNoTermino));
        Assert.Equal(
            (0.74m, 0.96m),
            (decimal.Round(noPainel.IndiceDeDesempenhoDePrazo!.Value, 2), decimal.Round(noPainel.IndiceDeDesempenhoDeCusto!.Value, 2)));
        Assert.Equal(noPainel, Assert.Single(await gestor.LerComSucesso<List<ObraNoPainelResposta>>("/api/painel")));
    }
}
