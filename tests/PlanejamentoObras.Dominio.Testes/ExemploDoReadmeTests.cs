using PlanejamentoObras.Dominio.Calendarios;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Dominio.ValorAgregado;

namespace PlanejamentoObras.Dominio.Testes;

/*
 * O exemplo do README, inteiro e conferido. Se o código mudar e os números do
 * README deixarem de ser verdade, este teste quebra antes de alguém ler o
 * README desatualizado.
 */
public sealed class ExemploDoReadmeTests
{
    private static Dinheiro R(decimal valor) => Dinheiro.Reais(valor);

    private static CronogramaProgramado Obra()
    {
        var cronograma = new Cronograma("Residencial Vista Serra");

        cronograma.AdicionarAtividade("MOB", "Mobilização do canteiro", 5);
        cronograma.AdicionarAtividade("FUN", "Fundações", 10);
        cronograma.AdicionarAtividade("EST", "Estrutura", 20);
        cronograma.AdicionarAtividade("ALV", "Alvenaria", 15);
        cronograma.AdicionarAtividade("ELE", "Instalações elétricas", 12);
        cronograma.AdicionarAtividade("REB", "Reboco", 8);
        cronograma.AdicionarAtividade("PIN", "Pintura", 6);
        cronograma.AdicionarAtividade("ENT", "Entrega das chaves", 0);

        cronograma.AdicionarDependencia("MOB", "FUN");
        cronograma.AdicionarDependencia("FUN", "EST");
        cronograma.AdicionarDependencia("EST", "ALV");
        cronograma.AdicionarDependencia("EST", "ELE");
        cronograma.AdicionarDependencia("ALV", "REB");
        cronograma.AdicionarDependencia("ELE", "REB");
        cronograma.AdicionarDependencia("REB", "PIN");
        cronograma.AdicionarDependencia("PIN", "ENT");

        var calendario = CalendarioDeTrabalho.DeSegundaASexta(
        [
            new DateOnly(2026, 10, 12), new DateOnly(2026, 11, 2),
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 25),
            new DateOnly(2027, 1, 1),
        ]);

        return ProgramacaoDoCronograma.Programar(cronograma, new DateOnly(2026, 10, 5), calendario);
    }

    private static BaseDeCustos BaseDeCustos()
    {
        var baseDeCustos = new BaseDeCustos("Tabela própria — setembro de 2026", new DateOnly(2026, 9, 1));

        baseDeCustos.AdicionarInsumo("CIM", "Cimento Portland CP II-32", "kg", TipoDeInsumo.Material, 0.78m);
        baseDeCustos.AdicionarInsumo("CAL", "Cal hidratada CH-III", "kg", TipoDeInsumo.Material, 1.05m);
        baseDeCustos.AdicionarInsumo("ARE", "Areia média lavada", "m³", TipoDeInsumo.Material, 120.00m);
        baseDeCustos.AdicionarInsumo("BLO", "Bloco cerâmico de vedação 9x19x19 cm", "un", TipoDeInsumo.Material, 1.32m);
        baseDeCustos.AdicionarInsumo("PED", "Pedreiro, com encargos sociais", "h", TipoDeInsumo.MaoDeObra, 28.74m);
        baseDeCustos.AdicionarInsumo("SER", "Servente, com encargos sociais", "h", TipoDeInsumo.MaoDeObra, 21.46m);
        baseDeCustos.AdicionarInsumo("BET", "Betoneira 400 L, custo horário produtivo", "h", TipoDeInsumo.Equipamento, 1.90m);

        baseDeCustos.AdicionarComposicao("ARG", "Argamassa de assentamento, traço 1:2:8, preparo mecânico", "m³");
        baseDeCustos.AdicionarItem("ARG", "CIM", 180m);
        baseDeCustos.AdicionarItem("ARG", "CAL", 150m);
        baseDeCustos.AdicionarItem("ARG", "ARE", 1.10m);
        baseDeCustos.AdicionarItem("ARG", "SER", 10m);
        baseDeCustos.AdicionarItem("ARG", "BET", 0.82m);

        baseDeCustos.AdicionarComposicao("ALV", "Alvenaria de vedação com bloco cerâmico 9x19x19 cm", "m²");
        baseDeCustos.AdicionarItem("ALV", "BLO", 13.5m);
        baseDeCustos.AdicionarItem("ALV", "ARG", 0.012m);
        baseDeCustos.AdicionarItem("ALV", "PED", 0.80m);
        baseDeCustos.AdicionarItem("ALV", "SER", 0.40m);

        baseDeCustos.AdicionarComposicao("LIMP", "Limpeza manual do terreno", "m²");
        baseDeCustos.AdicionarItem("LIMP", "SER", 0.15m);

        baseDeCustos.AdicionarComposicao("REB", "Emboço de parede, argamassa 1:2:8, 2 cm", "m²");
        baseDeCustos.AdicionarItem("REB", "ARG", 0.025m);
        baseDeCustos.AdicionarItem("REB", "PED", 0.60m);
        baseDeCustos.AdicionarItem("REB", "SER", 0.60m);

        baseDeCustos.AdicionarInsumo("FUND", "Fundações: estacas, blocos e vigas baldrame", "vb", TipoDeInsumo.Material, 98_500m);
        baseDeCustos.AdicionarInsumo("ESTR", "Estrutura de concreto armado", "vb", TipoDeInsumo.Material, 412_000m);
        baseDeCustos.AdicionarInsumo("ELET", "Instalações elétricas", "vb", TipoDeInsumo.Material, 86_300m);
        baseDeCustos.AdicionarInsumo("PINT", "Pintura interna e externa", "vb", TipoDeInsumo.Material, 64_200m);
        baseDeCustos.AdicionarInsumo(
            "ELV", "Elevador de passageiros, 8 pessoas, fornecido e instalado", "un", TipoDeInsumo.Equipamento, 180_000m);

        return baseDeCustos;
    }

    private static OrcamentoCalculado Planilha()
    {
        var bdi = Bdi.PelaFormulaDoTcu(new ParcelasDoBdi(
            administracaoCentral: 4.00m,
            seguros: 0.40m,
            riscos: 1.27m,
            garantias: 0.40m,
            despesasFinanceiras: 1.23m,
            lucro: 7.40m,
            tributos: 6.15m));

        var orcamento = new Orcamento("Residencial Vista Serra", bdi);

        orcamento.AdicionarEtapa("Serviços preliminares");
        orcamento.AdicionarItem("1", "LIMP", 1250m);
        orcamento.AdicionarEtapa("Infraestrutura e estrutura");
        orcamento.AdicionarItem("2", "FUND", 1m);
        orcamento.AdicionarItem("2", "ESTR", 1m);
        orcamento.AdicionarEtapa("Vedações e revestimentos");
        orcamento.AdicionarItem("3", "ALV", 2430.50m);
        orcamento.AdicionarItem("3", "REB", 4861m);
        orcamento.AdicionarEtapa("Instalações e acabamentos");
        orcamento.AdicionarItem("4", "ELET", 1m);
        orcamento.AdicionarItem("4", "PINT", 1m);
        orcamento.AdicionarEtapa("Equipamentos");
        orcamento.AdicionarItem("5", "ELV", 2m);
        orcamento.DefinirBdiDiferenciado("5.1", Bdi.Informado(12m));

        orcamento.VincularAtividade("1.1", "MOB");
        orcamento.VincularAtividade("2.1", "FUN");
        orcamento.VincularAtividade("2.2", "EST");
        orcamento.VincularAtividade("3.1", "ALV");
        orcamento.VincularAtividade("3.2", "REB");
        orcamento.VincularAtividade("4.1", "ELE");
        orcamento.VincularAtividade("4.2", "PIN");
        orcamento.VincularAtividade("5.1", "ENT");

        return CalculadoraDoOrcamento.Calcular(orcamento, BaseDeCustos());
    }

    [Fact]
    public void A_obra_termina_em_7_de_janeiro()
    {
        var obra = Obra();

        Assert.Equal((new DateOnly(2026, 10, 5), new DateOnly(2027, 1, 7), 64), (obra.DataDeInicio, obra.DataDeTermino, obra.DuracaoEmDiasUteis));
        Assert.Equal(3, obra.Da("ELE").FolgaTotal);
    }

    [Fact]
    public void A_alvenaria_custa_57_14_o_metro_quadrado()
    {
        Assert.Equal(R(57.14m), CalculadoraDeCustos.Calcular(BaseDeCustos(), "ALV").CustoUnitario);
    }

    [Fact]
    public void A_planilha_da_os_totais_do_README()
    {
        var planilha = Planilha();

        Assert.Equal(22.88m, planilha.Bdi.Percentual);
        Assert.Equal(R(1_388_822.24m), planilha.CustoDireto);
        Assert.Equal(R(1_667_428.66m), planilha.Total);
        Assert.Equal(
            [R(4_950.00m), R(627_302.40m), R(447_041.86m), R(184_934.40m), R(403_200.00m)],
            planilha.Linhas.Where(linha => linha.Nivel == 1).Select(linha => linha.Total));
    }

    [Fact]
    public void A_curva_ABC_do_README()
    {
        var curva = Planilha().CurvaAbc();

        Assert.Equal(["2.2", "5.1", "3.2", "3.1", "2.1", "4.1", "4.2", "1.1"], curva.Select(item => item.Numero.Valor));
        Assert.Equal(
            [ClasseAbc.A, ClasseAbc.A, ClasseAbc.A, ClasseAbc.A, ClasseAbc.B, ClasseAbc.B, ClasseAbc.B, ClasseAbc.C],
            curva.Select(item => item.Classe));
    }

    [Fact]
    public void O_fisico_financeiro_do_README()
    {
        var cedo = CalculadoraDoFisicoFinanceiro.Calcular(Planilha(), Obra(), DatasDoCronograma.Cedo);
        var tarde = CalculadoraDoFisicoFinanceiro.Calcular(Planilha(), Obra(), DatasDoCronograma.Tarde);

        Assert.Equal(
            [R(227_239.92m), R(465_652.92m), R(518_743.18m), R(455_792.64m)],
            cedo.Periodos.Select(periodo => periodo.Valor));
        Assert.Equal(
            [R(227_239.92m), R(439_141.56m), R(545_254.54m), R(455_792.64m)],
            tarde.Periodos.Select(periodo => periodo.Valor));
    }

    // ---------- linha de base, medição e valor agregado ----------

    private static Acompanhamento DuasMedicoes()
    {
        var linhaDeBase = LinhaDeBase.Estabelecer("Linha de base 1 — contrato", new DateOnly(2026, 9, 28), Planilha(), Obra());
        var acompanhamento = new Acompanhamento(linhaDeBase);

        acompanhamento.RegistrarMedicao(
            new DateOnly(2026, 10, 30),
            new LancamentoDeMedicao("1.1", 1250m, CustoReal: 4_300m),
            new LancamentoDeMedicao("2.1", 1m, CustoReal: 104_900m),
            new LancamentoDeMedicao("2.2", 0.15m, CustoReal: 63_000m));

        acompanhamento.RegistrarMedicao(
            new DateOnly(2026, 11, 30),
            new LancamentoDeMedicao("2.2", 0.55m, CustoReal: 232_000m),
            new LancamentoDeMedicao("3.1", 300m, CustoReal: 18_900m),
            new LancamentoDeMedicao("4.1", 0.10m, CustoReal: 9_600m));

        return acompanhamento;
    }

    [Fact]
    public void O_boletim_de_novembro_do_README()
    {
        var boletim = EmissaoDoBoletim.Emitir(DuasMedicoes(), medicao: 2);

        Assert.Equal(
            [R(278_446.08m), R(21_063.00m), R(10_604.54m)],
            boletim.Linhas.OfType<ItemDoBoletim>().Where(item => item.QuantidadeNoMes != 0).Select(item => item.ValorNoMes));
        Assert.Equal((R(310_113.62m), R(512_040.26m)), (boletim.ValorNoMes, boletim.ValorAcumulado));
        Assert.Equal(0.307m, decimal.Round(boletim.PercentualExecutado, 3));
    }

    [Fact]
    public void O_valor_agregado_de_novembro_do_README()
    {
        var analise = CalculadoraDeValorAgregado.Calcular(DuasMedicoes());
        var obra = analise.Obra;

        Assert.Equal(
            (R(1_388_822.24m), R(563_875.75m), R(416_697.00m), R(432_700.00m)),
            (obra.OrcamentoNoTermino, obra.ValorPlanejado, obra.ValorAgregado, obra.CustoReal));
        Assert.Equal((0.74m, 0.96m), (decimal.Round(obra.IndiceDeDesempenhoDePrazo!.Value, 2), decimal.Round(obra.IndiceDeDesempenhoDeCusto!.Value, 2)));
        Assert.Equal((R(1_442_159.13m), R(-53_336.89m)), (obra.EstimativaNoTermino, obra.VariacaoNoTermino));
        Assert.Equal((1.61m, 4.96m), (decimal.Round(analise.Prazo.PrazoAgregadoEmMeses, 2), decimal.Round(analise.Prazo.DuracaoEstimadaEmMeses!.Value, 2)));
        Assert.Equal(0.40m, decimal.Round(analise.Linha("4").Indicadores.IndiceDeDesempenhoDePrazo!.Value, 2));
    }
}
