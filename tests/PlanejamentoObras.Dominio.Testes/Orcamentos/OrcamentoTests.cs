using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Testes.Orcamentos;

public sealed class OrcamentoTests
{
    /// <summary>
    ///   1     Serviços preliminares
    ///   1.1     LIMP
    ///   2     Vedações
    ///   2.1     ALV
    ///   2.2     Revestimentos
    ///   2.2.1     REB
    /// </summary>
    private static Orcamento ComDuasEtapas()
    {
        var orcamento = new Orcamento("Residencial Vista Serra", Bdi.Informado(25m));
        orcamento.AdicionarEtapa("Serviços preliminares");
        orcamento.AdicionarItem("1", "LIMP", 1250m);
        orcamento.AdicionarEtapa("Vedações");
        orcamento.AdicionarItem("2", "ALV", 2430.50m);
        orcamento.AdicionarEtapa("Revestimentos", "2");
        orcamento.AdicionarItem("2.2", "REB", 4861m);

        return orcamento;
    }

    // ---------- o próprio orçamento ----------

    [Fact]
    public void Nasce_vazio_com_nome_e_BDI()
    {
        var orcamento = new Orcamento("  Torre A  ", Bdi.Informado(25m));

        Assert.Equal("Torre A", orcamento.Nome);
        Assert.Equal(25m, orcamento.Bdi.Percentual);
        Assert.Empty(orcamento.Etapas);
    }

    [Fact]
    public void Recusa_orcamento_sem_nome()
    {
        Assert.Throws<ExcecaoDeDominio>(() => new Orcamento(" ", Bdi.Zero));
    }

    [Fact]
    public void Troca_o_BDI_do_orcamento()
    {
        var orcamento = ComDuasEtapas();

        orcamento.AlterarBdi(Bdi.Informado(22.88m));

        Assert.Equal(22.88m, orcamento.Bdi.Percentual);
    }

    // ---------- estrutura e numeração ----------

    [Fact]
    public void Numera_cada_linha_pela_posicao()
    {
        var orcamento = new Orcamento("Obra", Bdi.Zero);

        var numeros = new[]
        {
            orcamento.AdicionarEtapa("Serviços preliminares"),
            orcamento.AdicionarItem("1", "LIMP", 1m),
            orcamento.AdicionarEtapa("Vedações"),
            orcamento.AdicionarItem("2", "ALV", 1m),
            orcamento.AdicionarEtapa("Revestimentos", "2"),
            orcamento.AdicionarItem("2.2", "REB", 1m),
        };

        Assert.Equal(["1", "1.1", "2", "2.1", "2.2", "2.2.1"], numeros.Select(numero => numero.Valor));
    }

    [Fact]
    public void Encontra_etapa_e_item_pelo_numero()
    {
        var orcamento = ComDuasEtapas();

        Assert.Equal("Revestimentos", orcamento.Etapa("2.2").Titulo);
        Assert.Equal("REB", orcamento.Item("2.2.1").Codigo.Valor);
        Assert.Equal(4861m, orcamento.Item("2.2.1").Quantidade);
    }

    [Fact]
    public void Item_so_entra_dentro_de_etapa()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => ComDuasEtapas().AdicionarItem("2.1", "PED", 1m));

        Assert.Equal("A linha 2.1 é um item, e não uma etapa.", erro.Message);
    }

    [Fact]
    public void Nao_existe_nada_dentro_de_item()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => ComDuasEtapas().Item("2.1.1"));

        Assert.Contains("dentro de um item não há outras linhas", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Acusa_linha_que_nao_existe_e_linha_do_tipo_errado()
    {
        var orcamento = ComDuasEtapas();

        Assert.Equal("Não existe a linha 3 neste orçamento.", Assert.Throws<ExcecaoDeDominio>(() => orcamento.Etapa("3")).Message);
        Assert.Equal("A linha 2 é uma etapa, e não um item.", Assert.Throws<ExcecaoDeDominio>(() => orcamento.Item("2")).Message);
    }

    [Fact]
    public void Remover_etapa_leva_junto_o_que_esta_dentro_e_renumera_as_seguintes()
    {
        var orcamento = ComDuasEtapas();

        orcamento.Remover("1");

        Assert.Single(orcamento.Etapas);
        Assert.Equal("Vedações", orcamento.Etapa("1").Titulo);
        Assert.Equal("REB", orcamento.Item("1.2.1").Codigo.Valor);
    }

    [Fact]
    public void Remover_item_sobe_as_linhas_seguintes_da_mesma_etapa()
    {
        var orcamento = ComDuasEtapas();

        orcamento.Remover("2.1");

        Assert.Equal("Revestimentos", orcamento.Etapa("2.1").Titulo);
        Assert.Throws<ExcecaoDeDominio>(() => orcamento.Etapa("2.2"));
    }

    [Fact]
    public void Recusa_remover_o_que_nao_existe()
    {
        Assert.Throws<ExcecaoDeDominio>(() => ComDuasEtapas().Remover("2.3"));
    }

    [Fact]
    public void Renomeia_etapa_e_recusa_titulo_vazio()
    {
        var orcamento = ComDuasEtapas();

        orcamento.RenomearEtapa("2.2", "Revestimentos internos");

        Assert.Equal("Revestimentos internos", orcamento.Etapa("2.2").Titulo);
        Assert.Throws<ExcecaoDeDominio>(() => orcamento.RenomearEtapa("2.2", " "));
    }

    [Fact]
    public void A_estrutura_nao_muda_por_fora_do_orcamento()
    {
        var orcamento = ComDuasEtapas();

        Assert.Throws<NotSupportedException>(() => ((ICollection<Etapa>)orcamento.Etapas).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<NoDaEap>)orcamento.Etapa("2").Filhos).Clear());
    }

    // ---------- itens ----------

    [Fact]
    public void Aceita_codigo_que_a_base_ainda_nao_tem()
    {
        // O orçamento não conhece a base: quem acusa é o cálculo.
        var orcamento = ComDuasEtapas();

        var numero = orcamento.AdicionarItem("1", "XYZ-99", 10m);

        Assert.Equal("XYZ-99", orcamento.Item(numero.Valor).Codigo.Valor);
    }

    [Fact]
    public void Recusa_codigo_em_formato_invalido()
    {
        Assert.Throws<ExcecaoDeDominio>(() => ComDuasEtapas().AdicionarItem("1", "ALV 01", 10m));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("12.345")]
    public void Recusa_quantidade_que_nao_e_positiva_ou_tem_casas_demais(string quantidade)
    {
        var valor = decimal.Parse(quantidade, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ExcecaoDeDominio>(() => ComDuasEtapas().AdicionarItem("1", "LIMP", valor));
    }

    [Fact]
    public void Altera_a_quantidade_com_as_mesmas_regras()
    {
        var orcamento = ComDuasEtapas();

        orcamento.AlterarQuantidade("2.1", 2500m);

        Assert.Equal(2500m, orcamento.Item("2.1").Quantidade);
        Assert.Throws<ExcecaoDeDominio>(() => orcamento.AlterarQuantidade("2.1", 0m));
    }

    [Fact]
    public void Vincula_e_desvincula_a_atividade_do_cronograma()
    {
        var orcamento = ComDuasEtapas();

        orcamento.VincularAtividade("2.1", "alv");
        Assert.Equal("ALV", orcamento.Item("2.1").Atividade?.Valor);

        orcamento.DesvincularAtividade("2.1");
        Assert.Null(orcamento.Item("2.1").Atividade);
    }

    [Fact]
    public void Define_e_remove_BDI_diferenciado()
    {
        var orcamento = ComDuasEtapas();

        orcamento.DefinirBdiDiferenciado("2.1", Bdi.Informado(12m));
        Assert.Equal(12m, orcamento.Item("2.1").BdiDiferenciado?.Percentual);

        orcamento.RemoverBdiDiferenciado("2.1");
        Assert.Null(orcamento.Item("2.1").BdiDiferenciado);
    }
}
