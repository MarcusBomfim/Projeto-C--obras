using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Dominio.Testes.Custos;

namespace PlanejamentoObras.Dominio.Testes.Orcamentos;

/*
 * A planilha do exemplo, calculada à mão. BDI do orçamento 22,88 % (fórmula
 * do TCU, ver BdiTests); o elevador tem BDI diferenciado de 12,00 %.
 *
 *   item   qtd        custo un.  BDI      preço un.   custo total   total
 *   1.1    1.250,00        3,22  22,88 %       3,96      4.025,00      4.950,00
 *   2.1    2.430,50       57,14  22,88 %      70,21    138.878,77    170.645,40
 *   2.2.1  4.861,00       46,27  22,88 %      56,86    224.918,47    276.396,46
 *   3.1        2,00  180.000,00  12,00 % 201.600,00    360.000,00    403.200,00
 *
 *   etapa 1                                              4.025,00      4.950,00
 *   etapa 2.2                                          224.918,47    276.396,46
 *   etapa 2                                            363.797,24    447.041,86
 *   etapa 3                                            360.000,00    403.200,00
 *   obra                                               727.822,24    855.191,86
 *
 * De onde vêm os custos unitários novos:
 *   LIMP  0,15 h de servente × 21,46 = 3,219 → 3,22
 *   REB   0,025 × 646,06 = 16,1515 → 16,15; 0,60 × 28,74 = 17,244 → 17,24;
 *         0,60 × 21,46 = 12,876 → 12,88; soma 46,27
 */
public sealed class CalculadoraDoOrcamentoTests
{
    private static Dinheiro R(decimal valor) => Dinheiro.Reais(valor);

    private static BaseDeCustos Base()
    {
        var baseDeCustos = BaseDeExemplo.Criar();

        baseDeCustos.AdicionarComposicao("LIMP", "Limpeza manual do terreno", "m²");
        baseDeCustos.AdicionarItem("LIMP", "SER", 0.15m);

        baseDeCustos.AdicionarComposicao("REB", "Emboço de parede, argamassa 1:2:8, 2 cm", "m²");
        baseDeCustos.AdicionarItem("REB", "ARG", 0.025m);
        baseDeCustos.AdicionarItem("REB", "PED", 0.60m);
        baseDeCustos.AdicionarItem("REB", "SER", 0.60m);

        baseDeCustos.AdicionarInsumo(
            "ELV", "Elevador de passageiros, 8 pessoas, fornecido e instalado", "un", TipoDeInsumo.Equipamento, 180_000m);

        return baseDeCustos;
    }

    private static Orcamento Orcamento()
    {
        var bdi = Bdi.PelaFormulaDoTcu(new ParcelasDoBdi(4.00m, 0.40m, 1.27m, 0.40m, 1.23m, 7.40m, 6.15m));
        var orcamento = new Orcamento("Residencial Vista Serra", bdi);

        orcamento.AdicionarEtapa("Serviços preliminares");
        orcamento.AdicionarItem("1", "LIMP", 1250m);
        orcamento.AdicionarEtapa("Vedações e revestimentos");
        orcamento.AdicionarItem("2", "ALV", 2430.50m);
        orcamento.AdicionarEtapa("Revestimentos", "2");
        orcamento.AdicionarItem("2.2", "REB", 4861m);
        orcamento.AdicionarEtapa("Equipamentos");
        orcamento.AdicionarItem("3", "ELV", 2m);
        orcamento.DefinirBdiDiferenciado("3.1", Bdi.Informado(12m));

        return orcamento;
    }

    private static OrcamentoCalculado Planilha() => CalculadoraDoOrcamento.Calcular(Orcamento(), Base());

    [Theory]
    //          item     custo un.     preço un.     custo total    total
    [InlineData("1.1", "3.22", "3.96", "4025.00", "4950.00")]
    [InlineData("2.1", "57.14", "70.21", "138878.77", "170645.40")]
    [InlineData("2.2.1", "46.27", "56.86", "224918.47", "276396.46")]
    [InlineData("3.1", "180000.00", "201600.00", "360000.00", "403200.00")]
    public void Precifica_cada_item(string numero, string custoUnitario, string precoUnitario, string custoTotal, string total)
    {
        var item = Planilha().Item(numero);

        Assert.Equal(Valor(custoUnitario), item.CustoUnitario);
        Assert.Equal(Valor(precoUnitario), item.PrecoUnitario);
        Assert.Equal(Valor(custoTotal), item.CustoTotal);
        Assert.Equal(Valor(total), item.Total);
    }

    [Theory]
    [InlineData("1", "4025.00", "4950.00")]
    [InlineData("2.2", "224918.47", "276396.46")]
    [InlineData("2", "363797.24", "447041.86")]
    [InlineData("3", "360000.00", "403200.00")]
    public void Soma_as_etapas_pelo_que_esta_logo_abaixo(string numero, string custoTotal, string total)
    {
        var etapa = Planilha().Linha(numero);

        Assert.IsType<LinhaDeEtapa>(etapa);
        Assert.Equal(Valor(custoTotal), etapa.CustoTotal);
        Assert.Equal(Valor(total), etapa.Total);
    }

    [Fact]
    public void Totaliza_a_obra()
    {
        var planilha = Planilha();

        Assert.Equal(R(727_822.24m), planilha.CustoDireto);
        Assert.Equal(R(855_191.86m), planilha.Total);
    }

    [Fact]
    public void Total_do_item_tambem_arredonda_pela_NBR_5891()
    {
        // 2.430,50 × 70,21 = 170.645,405: meio centavo exato, e o 0 é par.
        // Arredondando o meio para cima seria 170.645,41.
        Assert.Equal(R(170_645.40m), Planilha().Item("2.1").Total);
    }

    [Fact]
    public void Cada_item_usa_o_seu_BDI()
    {
        var planilha = Planilha();

        Assert.Equal(22.88m, planilha.Item("2.1").Bdi.Percentual);
        Assert.Equal(12.00m, planilha.Item("3.1").Bdi.Percentual);
    }

    [Fact]
    public void Descricao_e_unidade_vem_da_base()
    {
        var planilha = Planilha();

        Assert.Equal("Emboço de parede, argamassa 1:2:8, 2 cm", planilha.Item("2.2.1").Descricao);
        Assert.Equal("m²", planilha.Item("2.2.1").Unidade.Valor);

        // Insumo também pode ser item: o elevador entra pelo preço.
        Assert.Equal("un", planilha.Item("3.1").Unidade.Valor);
    }

    [Fact]
    public void Linhas_saem_na_ordem_da_planilha()
    {
        Assert.Equal(
            ["1", "1.1", "2", "2.1", "2.2", "2.2.1", "3", "3.1"],
            Planilha().Linhas.Select(linha => linha.Numero.Valor));
    }

    [Fact]
    public void Peso_e_quanto_a_linha_e_do_total()
    {
        var planilha = Planilha();

        // 447.041,86 / 855.191,86 = 0,52273868…
        Assert.Equal(0.5227m, decimal.Round(planilha.Linha("2").Peso, 4));
        Assert.Equal(1m, decimal.Round(planilha.Linhas.Where(linha => linha.Nivel == 1).Sum(linha => linha.Peso), 20));
    }

    [Fact]
    public void Leva_junto_a_base_e_o_vinculo_com_o_cronograma()
    {
        var orcamento = Orcamento();
        orcamento.VincularAtividade("2.1", "ALV");

        var planilha = CalculadoraDoOrcamento.Calcular(orcamento, Base());

        Assert.Equal("ALV", planilha.Item("2.1").Atividade?.Valor);
        Assert.Null(planilha.Item("1.1").Atividade);
        Assert.Equal(new DateOnly(2026, 9, 1), planilha.DataBase);
        Assert.Equal("Tabela própria — setembro de 2026", planilha.NomeDaBase);
    }

    [Fact]
    public void Acusa_de_uma_vez_todos_os_codigos_que_faltam_na_base()
    {
        var orcamento = Orcamento();
        orcamento.AdicionarItem("1", "XYZ", 1m);
        orcamento.AdicionarItem("2.2", "ABC", 1m);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => CalculadoraDoOrcamento.Calcular(orcamento, Base()));

        Assert.Contains("1.2 (XYZ), 2.2.2 (ABC)", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Etapa_vazia_entra_com_total_zero()
    {
        var orcamento = Orcamento();
        orcamento.AdicionarEtapa("Paisagismo");

        var planilha = CalculadoraDoOrcamento.Calcular(orcamento, Base());

        Assert.Equal(Dinheiro.Zero, planilha.Linha("4").Total);
        Assert.Equal(R(855_191.86m), planilha.Total);
    }

    [Fact]
    public void Acusa_linha_que_nao_existe_na_planilha()
    {
        var planilha = Planilha();

        Assert.Throws<ExcecaoDeDominio>(() => planilha.Linha("9"));
        Assert.Throws<ExcecaoDeDominio>(() => planilha.Item("2"));
    }

    // ---------- curva ABC ----------

    /// <summary>Seis itens de preço redondo, sem BDI: 50, 25, 10, 8, 5 e 2 — total 100.</summary>
    private static OrcamentoCalculado SeisItens()
    {
        var baseDeCustos = new BaseDeCustos("Redonda", new DateOnly(2026, 9, 1));
        var orcamento = new Orcamento("Curva", Bdi.Zero);
        orcamento.AdicionarEtapa("Tudo");

        // Cadastrados fora de ordem, para a curva ter o que ordenar.
        foreach (var valor in new[] { 8m, 50m, 2m, 25m, 5m, 10m })
        {
            baseDeCustos.AdicionarInsumo($"V{valor}", $"Item de {valor}", "vb", TipoDeInsumo.Material, valor);
            orcamento.AdicionarItem("1", $"V{valor}", 1m);
        }

        return CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos);
    }

    [Fact]
    public void Curva_ABC_vai_do_mais_caro_ao_mais_barato()
    {
        var curva = SeisItens().CurvaAbc();

        Assert.Equal([50m, 25m, 10m, 8m, 5m, 2m], curva.Select(item => item.Total.Valor));
        Assert.Equal([0.50m, 0.75m, 0.85m, 0.93m, 0.98m, 1m], curva.Select(item => item.PesoAcumulado));
    }

    [Fact]
    public void Classifica_pelo_acumulado_antes_do_item()
    {
        /*
         * Acumulado antes de cada um: 0, 50, 75, 85, 93 e 98 %. A até 80, B
         * até 95, C dali em diante. O item de 10 é A: quando chega a vez dele,
         * os mais caros somam 75 %.
         */
        var classes = SeisItens().CurvaAbc().Select(item => item.Classe);

        Assert.Equal([ClasseAbc.A, ClasseAbc.A, ClasseAbc.A, ClasseAbc.B, ClasseAbc.B, ClasseAbc.C], classes);
    }

    [Fact]
    public void Item_que_sozinho_passa_de_80_por_cento_e_A()
    {
        var baseDeCustos = new BaseDeCustos("Concentrada", new DateOnly(2026, 9, 1));
        baseDeCustos.AdicionarInsumo("GRANDE", "Item grande", "vb", TipoDeInsumo.Material, 85m);
        baseDeCustos.AdicionarInsumo("PEQUENO", "Item pequeno", "vb", TipoDeInsumo.Material, 15m);
        var orcamento = new Orcamento("Concentrada", Bdi.Zero);
        orcamento.AdicionarEtapa("Tudo");
        orcamento.AdicionarItem("1", "PEQUENO", 1m);
        orcamento.AdicionarItem("1", "GRANDE", 1m);

        // Olhando o acumulado depois, o grande seria B (85 %). Antes dele não
        // há nada: é A. O pequeno chega com 85 % acumulados: B.
        var curva = CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos).CurvaAbc();

        Assert.Equal((ClasseAbc.A, ClasseAbc.B), (curva[0].Classe, curva[1].Classe));
    }

    [Fact]
    public void Empate_de_valor_fica_na_ordem_da_planilha()
    {
        var baseDeCustos = new BaseDeCustos("Empate", new DateOnly(2026, 9, 1));
        baseDeCustos.AdicionarInsumo("X", "Primeiro", "vb", TipoDeInsumo.Material, 10m);
        baseDeCustos.AdicionarInsumo("Y", "Segundo", "vb", TipoDeInsumo.Material, 10m);
        var orcamento = new Orcamento("Empate", Bdi.Zero);
        orcamento.AdicionarEtapa("Tudo");
        orcamento.AdicionarItem("1", "X", 1m);
        orcamento.AdicionarItem("1", "Y", 1m);

        var curva = CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos).CurvaAbc();

        Assert.Equal(["1.1", "1.2"], curva.Select(item => item.Numero.Valor));
    }

    private static Dinheiro Valor(string texto) =>
        Dinheiro.Reais(decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture));
}
