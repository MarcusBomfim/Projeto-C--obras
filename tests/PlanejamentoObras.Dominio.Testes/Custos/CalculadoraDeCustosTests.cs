using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Custos;

namespace PlanejamentoObras.Dominio.Testes.Custos;

/*
 * Os valores esperados estão abertos, linha a linha, no comentário de
 * BaseDeExemplo — a conta de papel contra a qual o cálculo é conferido.
 */
public sealed class CalculadoraDeCustosTests
{
    private static Dinheiro R(decimal valor) => Dinheiro.Reais(valor);

    [Fact]
    public void Calcula_a_composicao_linha_a_linha()
    {
        var argamassa = CalculadoraDeCustos.Calcular(BaseDeExemplo.Criar(), "ARG");

        Assert.Equal(
            [R(140.40m), R(157.50m), R(132.00m), R(214.60m), R(1.56m)],
            argamassa.Linhas.Select(linha => linha.CustoParcial));
        Assert.Equal(R(646.06m), argamassa.CustoUnitario);
    }

    [Fact]
    public void Composicao_auxiliar_entra_pelo_custo_unitario_dela()
    {
        var alvenaria = CalculadoraDeCustos.Calcular(BaseDeExemplo.Criar(), "ALV");
        var argamassa = alvenaria.Linhas[1];

        Assert.True(argamassa.EhComposicaoAuxiliar);
        Assert.Equal(R(646.06m), argamassa.CustoUnitario);
        Assert.Equal(R(7.75m), argamassa.CustoParcial);
        Assert.Equal(R(57.14m), alvenaria.CustoUnitario);
    }

    [Fact]
    public void A_linha_traz_o_que_o_anexo_do_orcamento_mostra()
    {
        var pedreiro = CalculadoraDeCustos.Calcular(BaseDeExemplo.Criar(), "ALV").Linhas[2];

        Assert.Equal("PED", pedreiro.Codigo.Valor);
        Assert.Equal("Pedreiro, com encargos sociais", pedreiro.Descricao);
        Assert.Equal("h", pedreiro.Unidade.Valor);
        Assert.Equal(TipoDeInsumo.MaoDeObra, pedreiro.Tipo);
        Assert.Equal(0.80m, pedreiro.Coeficiente);
        Assert.Equal(R(28.74m), pedreiro.CustoUnitario);
        Assert.Equal(R(22.99m), pedreiro.CustoParcial);
    }

    [Fact]
    public void Cada_parcela_e_arredondada_pela_NBR_5891()
    {
        /*
         * 0,25 × 12,34 = 3,085 e 0,5 × 0,15 = 0,075: dois meios centavos
         * exatos. Para o par, 3,08 e 0,08 — total 3,16. Arredondando o meio
         * sempre para cima seriam 3,09 e 0,08, total 3,17.
         */
        var baseDeCustos = new BaseDeCustos("Telas", new DateOnly(2026, 9, 1));
        baseDeCustos.AdicionarInsumo("TEL", "Tela de aço soldada", "m²", TipoDeInsumo.Material, 12.34m);
        baseDeCustos.AdicionarInsumo("ESP", "Espaçador plástico", "un", TipoDeInsumo.Material, 0.15m);
        baseDeCustos.AdicionarComposicao("ARM", "Armação de piso com tela", "m²");
        baseDeCustos.AdicionarItem("ARM", "TEL", 0.25m);
        baseDeCustos.AdicionarItem("ARM", "ESP", 0.5m);

        var armacao = CalculadoraDeCustos.Calcular(baseDeCustos, "ARM");

        Assert.Equal([R(3.08m), R(0.08m)], armacao.Linhas.Select(linha => linha.CustoParcial));
        Assert.Equal(R(3.16m), armacao.CustoUnitario);
    }

    [Fact]
    public void Preco_novo_no_insumo_sobe_pelas_composicoes_que_o_usam()
    {
        /*
         * Cimento de 0,78 para 0,80: 180 × 0,80 = 144,00 e a argamassa vai a
         * 649,66. Na alvenaria, 0,012 × 649,66 = 7,79592 → 7,80, e o metro
         * quadrado vai a 57,19.
         */
        var baseDeCustos = BaseDeExemplo.Criar();
        baseDeCustos.AlterarPrecoDoInsumo("CIM", 0.80m);

        Assert.Equal(R(57.19m), CalculadoraDeCustos.Calcular(baseDeCustos, "ALV").CustoUnitario);
    }

    [Fact]
    public void Calcula_varias_de_uma_vez()
    {
        var custos = CalculadoraDeCustos.Calcular(BaseDeExemplo.Criar(), ["alv", "ARG"]);

        Assert.Equal(2, custos.Count);
        Assert.Equal(R(57.14m), custos[new CodigoDeCusto("ALV")].CustoUnitario);
        Assert.Equal(R(646.06m), custos[new CodigoDeCusto("ARG")].CustoUnitario);
    }

    [Fact]
    public void Recusa_calcular_composicao_sem_itens()
    {
        var baseDeCustos = BaseDeExemplo.Criar();
        baseDeCustos.AdicionarComposicao("REB", "Reboco", "m²");

        var erro = Assert.Throws<ExcecaoDeDominio>(() => CalculadoraDeCustos.Calcular(baseDeCustos, "REB"));

        Assert.Contains("não tem itens", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_tambem_quando_a_composicao_sem_itens_e_auxiliar()
    {
        var baseDeCustos = BaseDeExemplo.Criar();
        baseDeCustos.AdicionarComposicao("CHA", "Chapisco", "m²");
        baseDeCustos.AdicionarComposicao("REB", "Reboco", "m²");
        baseDeCustos.AdicionarItem("REB", "CHA", 1m);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => CalculadoraDeCustos.Calcular(baseDeCustos, "REB"));

        Assert.Contains("CHA não tem itens", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Insumo_nao_e_composicao()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => CalculadoraDeCustos.Calcular(BaseDeExemplo.Criar(), "CIM"));

        Assert.Contains("é um insumo", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Acusa_composicao_que_nao_existe()
    {
        Assert.Throws<ExcecaoDeDominio>(() => CalculadoraDeCustos.Calcular(BaseDeExemplo.Criar(), "XYZ"));
    }
}
