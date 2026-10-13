using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Custos;

namespace PlanejamentoObras.Dominio.Testes.Custos;

public sealed class BaseDeCustosTests
{
    private static BaseDeCustos Vazia() => new("Tabela própria", new DateOnly(2026, 9, 1));

    // ---------- a própria base ----------

    [Fact]
    public void Nasce_vazia_com_nome_e_data_base()
    {
        var baseDeCustos = new BaseDeCustos("  SINAPI SP  ", new DateOnly(2026, 9, 1));

        Assert.Equal("SINAPI SP", baseDeCustos.Nome);
        Assert.Equal(new DateOnly(2026, 9, 1), baseDeCustos.DataBase);
        Assert.Empty(baseDeCustos.Insumos);
        Assert.Empty(baseDeCustos.Composicoes);
    }

    [Fact]
    public void Recusa_base_sem_nome()
    {
        Assert.Throws<ExcecaoDeDominio>(() => new BaseDeCustos(" ", new DateOnly(2026, 9, 1)));
    }

    [Fact]
    public void Muda_nome_e_data_base_ao_atualizar_os_precos()
    {
        var baseDeCustos = Vazia();

        baseDeCustos.Renomear("Tabela própria — outubro");
        baseDeCustos.AlterarDataBase(new DateOnly(2026, 10, 1));

        Assert.Equal("Tabela própria — outubro", baseDeCustos.Nome);
        Assert.Equal(new DateOnly(2026, 10, 1), baseDeCustos.DataBase);
    }

    // ---------- insumos ----------

    [Fact]
    public void Cadastra_insumo_e_o_encontra_em_qualquer_caixa()
    {
        var baseDeCustos = Vazia();
        baseDeCustos.AdicionarInsumo(" cim ", "  Cimento Portland CP II-32 ", " kg ", TipoDeInsumo.Material, 0.78m);

        var cimento = baseDeCustos.Insumo("CIM");

        Assert.Equal("Cimento Portland CP II-32", cimento.Descricao);
        Assert.Equal("kg", cimento.Unidade.Valor);
        Assert.Equal(TipoDeInsumo.Material, cimento.Tipo);
        Assert.Equal(Dinheiro.Reais(0.78m), cimento.Preco);
        Assert.True(baseDeCustos.Contem("Cim"));
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.325")]
    public void Recusa_preco_negativo_ou_alem_do_centavo(string preco)
    {
        var valor = decimal.Parse(preco, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ExcecaoDeDominio>(() =>
            Vazia().AdicionarInsumo("CIM", "Cimento", "kg", TipoDeInsumo.Material, valor));
    }

    [Fact]
    public void Aceita_insumo_de_preco_zero()
    {
        // Material fornecido pelo cliente entra na composição, mas não custa.
        var baseDeCustos = Vazia();

        baseDeCustos.AdicionarInsumo("BLO", "Bloco fornecido pelo cliente", "un", TipoDeInsumo.Material, 0m);

        Assert.Equal(Dinheiro.Zero, baseDeCustos.Insumo("BLO").Preco);
    }

    [Fact]
    public void Recusa_tipo_de_insumo_que_nao_existe()
    {
        Assert.Throws<ExcecaoDeDominio>(() =>
            Vazia().AdicionarInsumo("CIM", "Cimento", "kg", (TipoDeInsumo)99, 0.78m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("metro quadrado")]
    public void Recusa_unidade_vazia_ou_longa_demais(string unidade)
    {
        Assert.Throws<ExcecaoDeDominio>(() =>
            Vazia().AdicionarInsumo("CIM", "Cimento", unidade, TipoDeInsumo.Material, 0.78m));
    }

    [Fact]
    public void Altera_o_preco_com_as_mesmas_regras_do_cadastro()
    {
        var baseDeCustos = BaseDeExemplo.Criar();

        baseDeCustos.AlterarPrecoDoInsumo("CIM", 0.80m);

        Assert.Equal(Dinheiro.Reais(0.80m), baseDeCustos.Insumo("CIM").Preco);
        Assert.Throws<ExcecaoDeDominio>(() => baseDeCustos.AlterarPrecoDoInsumo("CIM", 0.805m));
    }

    [Fact]
    public void Insumos_e_composicoes_dividem_o_mesmo_codigo()
    {
        var baseDeCustos = BaseDeExemplo.Criar();

        var comoComposicao = Assert.Throws<ExcecaoDeDominio>(() =>
            baseDeCustos.AdicionarComposicao("cim", "Outra coisa", "m²"));
        var comoInsumo = Assert.Throws<ExcecaoDeDominio>(() =>
            baseDeCustos.AdicionarInsumo("ALV", "Outra coisa", "un", TipoDeInsumo.Material, 1m));

        Assert.Equal("Já existe o insumo CIM nesta base.", comoComposicao.Message);
        Assert.Equal("Já existe a composição ALV nesta base.", comoInsumo.Message);
    }

    // ---------- itens das composições ----------

    [Fact]
    public void Mantem_os_itens_na_ordem_em_que_entraram()
    {
        var itens = BaseDeExemplo.Criar().Composicao("ALV").Itens;

        Assert.Equal(["BLO", "ARG", "PED", "SER"], itens.Select(item => item.Codigo.Valor));
        Assert.Equal(0.012m, itens[1].Coeficiente);
    }

    [Fact]
    public void Recusa_item_que_nao_esta_na_base()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => BaseDeExemplo.Criar().AdicionarItem("ALV", "VER", 1m));

        Assert.Contains("Não existe VER nesta base", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_o_mesmo_item_duas_vezes()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => BaseDeExemplo.Criar().AdicionarItem("ALV", "PED", 0.1m));

        Assert.Contains("altere o coeficiente", erro.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Recusa_coeficiente_que_nao_e_positivo(int coeficiente)
    {
        var baseDeCustos = BaseDeExemplo.Criar();
        baseDeCustos.AdicionarComposicao("CHA", "Chapisco", "m²");

        Assert.Throws<ExcecaoDeDominio>(() => baseDeCustos.AdicionarItem("CHA", "CIM", coeficiente));
    }

    [Fact]
    public void Recusa_composicao_que_usa_a_si_mesma()
    {
        Assert.Throws<ExcecaoDeDominio>(() => BaseDeExemplo.Criar().AdicionarItem("ARG", "ARG", 1m));
    }

    [Fact]
    public void Recusa_o_item_que_fecharia_um_ciclo_e_mostra_qual()
    {
        // A alvenaria usa a argamassa; a argamassa não pode passar a usar a alvenaria.
        var erro = Assert.Throws<ExcecaoDeDominio>(() => BaseDeExemplo.Criar().AdicionarItem("ARG", "ALV", 1m));

        Assert.Contains("fecharia um ciclo: ARG → ALV → ARG", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_tambem_o_ciclo_indireto()
    {
        var baseDeCustos = BaseDeExemplo.Criar();
        baseDeCustos.AdicionarComposicao("PAR", "Parede pronta", "m²");
        baseDeCustos.AdicionarItem("PAR", "ALV", 1m);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => baseDeCustos.AdicionarItem("ARG", "PAR", 1m));

        Assert.Contains("ARG → PAR → ALV → ARG", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_mesma_auxiliar_em_duas_composicoes_nao_e_ciclo()
    {
        // Losango, não laço: alvenaria e reboco usam a mesma argamassa.
        var baseDeCustos = BaseDeExemplo.Criar();
        baseDeCustos.AdicionarComposicao("REB", "Reboco", "m²");
        baseDeCustos.AdicionarItem("REB", "ARG", 0.02m);
        baseDeCustos.AdicionarComposicao("PAR", "Parede pronta", "m²");
        baseDeCustos.AdicionarItem("PAR", "ALV", 1m);

        baseDeCustos.AdicionarItem("PAR", "REB", 2m);

        Assert.Equal(2, baseDeCustos.Composicao("PAR").Itens.Count);
    }

    [Fact]
    public void Altera_o_coeficiente_e_remove_o_item()
    {
        var baseDeCustos = BaseDeExemplo.Criar();

        baseDeCustos.AlterarCoeficiente("ALV", "PED", 0.75m);
        baseDeCustos.RemoverItem("ALV", "SER");

        var itens = baseDeCustos.Composicao("ALV").Itens;
        Assert.Equal(0.75m, itens.Single(item => item.Codigo.Valor == "PED").Coeficiente);
        Assert.DoesNotContain(itens, item => item.Codigo.Valor == "SER");
        Assert.Throws<ExcecaoDeDominio>(() => baseDeCustos.RemoverItem("ALV", "SER"));
    }

    [Fact]
    public void Os_itens_nao_mudam_por_fora_da_base()
    {
        var itens = BaseDeExemplo.Criar().Composicao("ALV").Itens;

        Assert.Throws<NotSupportedException>(() => ((ICollection<ItemDaComposicao>)itens).Clear());
    }

    // ---------- remoção ----------

    [Fact]
    public void Recusa_remover_insumo_em_uso_e_diz_onde()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => BaseDeExemplo.Criar().RemoverInsumo("SER"));

        Assert.Equal(
            "O insumo SER é usado em ARG e ALV. Tire-o dessas composições antes de removê-lo.",
            erro.Message);
    }

    [Fact]
    public void Remove_insumo_que_ninguem_usa()
    {
        var baseDeCustos = BaseDeExemplo.Criar();
        baseDeCustos.AdicionarInsumo("VER", "Verniz", "l", TipoDeInsumo.Material, 45m);

        baseDeCustos.RemoverInsumo("VER");

        Assert.False(baseDeCustos.Contem("VER"));
    }

    [Fact]
    public void Recusa_remover_composicao_auxiliar_em_uso()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => BaseDeExemplo.Criar().RemoverComposicao("ARG"));

        Assert.Contains("é usada em ALV", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Remove_composicao_que_ninguem_usa()
    {
        var baseDeCustos = BaseDeExemplo.Criar();

        baseDeCustos.RemoverComposicao("ALV");

        Assert.False(baseDeCustos.Contem("ALV"));
        Assert.True(baseDeCustos.Contem("ARG"));
    }
}
