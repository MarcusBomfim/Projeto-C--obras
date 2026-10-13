using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;

namespace PlanejamentoObras.Dominio.Testes.LinhasDeBase;

// A obra e os números esperados estão abertos no comentário de CasaTerrea.
public sealed class LinhaDeBaseTests
{
    private static readonly DateOnly Setembro = new(2026, 9, 1);
    private static readonly DateOnly Outubro = new(2026, 10, 1);
    private static readonly DateOnly Novembro = new(2026, 11, 1);

    private static Dinheiro R(decimal valor) => Dinheiro.Reais(valor);

    private static LinhaDeBase Estabelecer(DatasDoCronograma datas = DatasDoCronograma.Cedo) =>
        LinhaDeBase.Estabelecer("  Linha de base 1 — contrato  ", new DateOnly(2026, 10, 15), CasaTerrea.Planilha(), CasaTerrea.Obra(), datas);

    [Fact]
    public void Congela_o_orcamento_e_os_meses_do_cronograma()
    {
        var linhaDeBase = Estabelecer();

        Assert.Equal("Linha de base 1 — contrato", linhaDeBase.Nome);
        Assert.Equal(new DateOnly(2026, 10, 15), linhaDeBase.AprovadaEm);
        Assert.Equal(DatasDoCronograma.Cedo, linhaDeBase.Datas);
        Assert.Equal((new DateOnly(2026, 10, 19), new DateOnly(2026, 11, 17)), (linhaDeBase.InicioPlanejado, linhaDeBase.TerminoPlanejado));
        Assert.Equal([Outubro, Novembro], linhaDeBase.Meses);
        Assert.Equal(R(41_500m), linhaDeBase.Total);
        Assert.Equal(R(33_200m), linhaDeBase.CustoTotal);
    }

    [Fact]
    public void Guarda_de_cada_item_o_contratado_e_o_planejado_de_cada_mes()
    {
        var fundacoes = Estabelecer().Item("2.1");

        Assert.Equal("Fundações", fundacoes.Descricao);
        Assert.Equal("vb", fundacoes.Unidade.Valor);
        Assert.Equal(1m, fundacoes.Quantidade);
        Assert.Equal(R(10_000m), fundacoes.PrecoUnitario);
        Assert.Equal(R(10_000m), fundacoes.Total);
        Assert.Equal(R(8_000m), fundacoes.CustoUnitario);
        Assert.Equal(R(8_000m), fundacoes.CustoTotal);
        Assert.Equal("FUN", fundacoes.Atividade.Valor);
        Assert.Equal([R(8_333.33m), R(1_666.67m)], fundacoes.ValoresPlanejados);
        Assert.Equal([R(6_666.67m), R(1_333.33m)], fundacoes.CustosPlanejados);
    }

    [Fact]
    public void Guarda_tambem_as_etapas()
    {
        var linhaDeBase = Estabelecer();

        Assert.IsType<EtapaPlanejada>(linhaDeBase.Linha("1"));
        Assert.Equal([R(11_000m), R(0m)], linhaDeBase.Linha("1").ValoresPlanejados);
        Assert.Equal(["1", "1.1", "1.2", "2", "2.1", "2.2", "2.3"], linhaDeBase.Linhas.Select(linha => linha.Numero.Valor));
        Assert.Equal(5, linhaDeBase.Itens.Count);
    }

    [Theory]
    [InlineData("2026-09-30", "0")] // antes de começar
    [InlineData("2026-10-01", "19333.33")] // qualquer dia de outubro vale o mês inteiro
    [InlineData("2026-10-31", "19333.33")]
    [InlineData("2026-11-17", "41500.00")]
    [InlineData("2027-01-15", "41500.00")] // depois de terminar, o orçamento inteiro
    public void Valor_planejado_acumulado_ate_o_fim_do_mes(string mes, string esperado)
    {
        var valor = Estabelecer().ValorPlanejadoAcumulado(DateOnly.Parse(mes, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(R(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture)), valor);
    }

    [Fact]
    public void Valor_planejado_acumulado_de_uma_linha()
    {
        var linhaDeBase = Estabelecer();

        Assert.Equal(R(8_333.33m), linhaDeBase.ValorPlanejadoAcumulado("2.1", Outubro));
        Assert.Equal(R(10_000m), linhaDeBase.ValorPlanejadoAcumulado("2.1", Novembro));
        Assert.Equal(R(0m), linhaDeBase.ValorPlanejadoAcumulado("2.1", Setembro));
    }

    [Fact]
    public void Custo_planejado_acumulado_da_obra_e_de_uma_linha()
    {
        // A custo, sem o BDI de 25 %: 8.000 + 800 + 6.666,67 em outubro.
        var linhaDeBase = Estabelecer();

        Assert.Equal(R(0m), linhaDeBase.CustoPlanejadoAcumulado(Setembro));
        Assert.Equal(R(15_466.67m), linhaDeBase.CustoPlanejadoAcumulado(Outubro));
        Assert.Equal(R(33_200m), linhaDeBase.CustoPlanejadoAcumulado(Novembro));
        Assert.Equal(R(6_666.67m), linhaDeBase.CustoPlanejadoAcumulado("2.1", Outubro));
    }

    [Fact]
    public void Pelas_datas_tarde_o_planejado_vai_para_depois()
    {
        var tarde = Estabelecer(DatasDoCronograma.Tarde);

        Assert.Equal(DatasDoCronograma.Tarde, tarde.Datas);
        Assert.Equal([R(0m), R(1_000m)], tarde.Item("1.2").ValoresPlanejados);
        Assert.Equal(R(18_333.33m), tarde.ValorPlanejadoAcumulado(Outubro));
    }

    [Fact]
    public void Nada_na_linha_de_base_muda_depois_de_estabelecida()
    {
        // Nem convertendo de volta: as coleções são só de leitura de verdade.
        var linhaDeBase = Estabelecer();
        var item = linhaDeBase.Item("2.1");

        Assert.Throws<NotSupportedException>(() => { ((IList<Dinheiro>)item.ValoresPlanejados)[0] = R(0m); });
        Assert.Throws<NotSupportedException>(() => ((ICollection<LinhaPlanejada>)linhaDeBase.Linhas).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<DateOnly>)linhaDeBase.Meses).Clear());
        Assert.IsNotType<Dinheiro[]>(item.ValoresPlanejados);
    }

    [Fact]
    public void Mudar_o_orcamento_depois_nao_muda_a_linha_de_base()
    {
        // A quantidade da alvenaria dobra depois da aprovação: o orçamento
        // vivo muda, a linha de base não.
        var linhaDeBase = Estabelecer();
        var revisto = CasaTerrea.Planilha(orcamento => orcamento.AlterarQuantidade("2.2", 2m));

        Assert.Equal(R(61_500m), revisto.Total);
        Assert.Equal(R(41_500m), linhaDeBase.Total);
        Assert.Equal(1m, linhaDeBase.Item("2.2").Quantidade);
    }

    [Fact]
    public void Recusa_linha_de_base_sem_nome()
    {
        Assert.Throws<ExcecaoDeDominio>(() =>
            LinhaDeBase.Estabelecer(" ", Outubro, CasaTerrea.Planilha(), CasaTerrea.Obra()));
    }

    [Fact]
    public void Recusa_orcamento_sem_itens()
    {
        var vazio = CasaTerrea.Planilha(orcamento =>
        {
            orcamento.Remover("2");
            orcamento.Remover("1");
            orcamento.AdicionarEtapa("Nada ainda");
        });

        var erro = Assert.Throws<ExcecaoDeDominio>(() => LinhaDeBase.Estabelecer("LB", Outubro, vazio, CasaTerrea.Obra()));

        Assert.Contains("sem itens", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_orcamento_com_item_sem_atividade()
    {
        var semVinculo = CasaTerrea.Planilha(orcamento => orcamento.DesvincularAtividade("1.1"));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => LinhaDeBase.Estabelecer("LB", Outubro, semVinculo, CasaTerrea.Obra()));

        Assert.Contains("itens sem atividade vinculada: 1.1", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Acusa_linha_que_nao_existe_e_etapa_pedida_como_item()
    {
        var linhaDeBase = Estabelecer();

        Assert.Throws<ExcecaoDeDominio>(() => linhaDeBase.Linha("9"));
        Assert.Throws<ExcecaoDeDominio>(() => linhaDeBase.Item("1"));
    }
}
