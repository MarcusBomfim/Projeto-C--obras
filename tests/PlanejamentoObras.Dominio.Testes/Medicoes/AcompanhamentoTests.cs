using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;

namespace PlanejamentoObras.Dominio.Testes.Medicoes;

// A linha de base é a da CasaTerrea: cinco itens de quantidade 1 — 1.1, 1.2,
// 2.1, 2.2 e 2.3 —, em outubro e novembro de 2026.
public sealed class AcompanhamentoTests
{
    private static readonly DateOnly Outubro = new(2026, 10, 1);
    private static readonly DateOnly Novembro = new(2026, 11, 1);
    private static readonly DateOnly Dezembro = new(2026, 12, 1);

    private static Acompanhamento Novo() => new(CasaTerrea.LinhaDeBaseAprovada());

    private static string Erro(Action acao) => Assert.Throws<ExcecaoDeDominio>(acao).Message;

    [Fact]
    public void Nasce_sem_medicoes_e_com_a_linha_de_base()
    {
        var acompanhamento = Novo();

        Assert.Empty(acompanhamento.Medicoes);
        Assert.Equal("Linha de base 1", acompanhamento.LinhaDeBase.Nome);
    }

    [Fact]
    public void Registra_a_medicao_do_mes()
    {
        var acompanhamento = Novo();

        // Qualquer dia vale pelo mês inteiro.
        var medicao = acompanhamento.RegistrarMedicao(
            new DateOnly(2026, 10, 31),
            new LancamentoDeMedicao("1.1", 1m, 9_000m),
            new LancamentoDeMedicao("2.1", 0.6m, 6_200m));

        Assert.Equal(1, medicao.Numero);
        Assert.Equal(Outubro, medicao.Mes);
        Assert.Equal(["1.1", "2.1"], medicao.Itens.Select(item => item.Item.Valor));
        Assert.Equal((0.6m, Dinheiro.Reais(6_200m)), (medicao.Itens[1].Quantidade, medicao.Itens[1].CustoReal));
    }

    [Fact]
    public void Medicoes_entram_em_ordem_uma_por_mes()
    {
        var acompanhamento = Novo();
        acompanhamento.RegistrarMedicao(Novembro);

        Assert.Contains("não pode entrar depois da de 11/2026", Erro(() => acompanhamento.RegistrarMedicao(Outubro)), StringComparison.Ordinal);
        Assert.Contains("uma por mês", Erro(() => acompanhamento.RegistrarMedicao(new DateOnly(2026, 11, 20))), StringComparison.Ordinal);
    }

    [Fact]
    public void Mes_sem_medicao_no_meio_e_permitido()
    {
        var acompanhamento = Novo();
        acompanhamento.RegistrarMedicao(Outubro);

        var dezembro = acompanhamento.RegistrarMedicao(Dezembro);

        Assert.Equal(2, dezembro.Numero);
    }

    [Fact]
    public void Mes_parado_tem_medicao_sem_lancamentos()
    {
        Assert.Empty(Novo().RegistrarMedicao(Outubro).Itens);
    }

    [Fact]
    public void Acusa_todos_os_problemas_de_uma_vez_e_nao_registra_nada()
    {
        var acompanhamento = Novo();

        var erro = Erro(() => acompanhamento.RegistrarMedicao(
            Outubro,
            new LancamentoDeMedicao("9", 1m),
            new LancamentoDeMedicao("1", 1m),
            new LancamentoDeMedicao("2.1", 0.5m),
            new LancamentoDeMedicao("2.1", 0.2m),
            new LancamentoDeMedicao("x", 1m),
            new LancamentoDeMedicao("2.2", 0.125m),
            new LancamentoDeMedicao("1.2", 1m, -5m),
            new LancamentoDeMedicao("1.1", 1m, 10.005m)));

        Assert.StartsWith("A medição de 10/2026 não foi registrada:", erro, StringComparison.Ordinal);
        Assert.Contains("o item 9 não existe na linha de base", erro, StringComparison.Ordinal);
        Assert.Contains("1 é uma etapa, e a medição é item por item", erro, StringComparison.Ordinal);
        Assert.Contains("o item 2.1 aparece mais de uma vez", erro, StringComparison.Ordinal);
        Assert.Contains("\"x\" não é um número de item", erro, StringComparison.Ordinal);
        Assert.Contains("a quantidade do item 2.2 tem mais de duas casas decimais", erro, StringComparison.Ordinal);
        Assert.Contains("o custo real do item 1.2 precisa ser positivo", erro, StringComparison.Ordinal);
        Assert.Contains("o custo real do item 1.1 precisa ser positivo e ir até o centavo", erro, StringComparison.Ordinal);
        Assert.Empty(acompanhamento.Medicoes);
    }

    [Fact]
    public void Nao_mede_alem_do_contratado()
    {
        var acompanhamento = Novo();
        acompanhamento.RegistrarMedicao(Outubro, new LancamentoDeMedicao("2.1", 0.6m));

        var erro = Erro(() => acompanhamento.RegistrarMedicao(Novembro, new LancamentoDeMedicao("2.1", 0.5m)));

        Assert.Contains(
            "o item 2.1 chegaria a 1,10 vb medidos, mais que os 1,00 vb contratados — medir além do contrato exige aditivo",
            erro,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Glosa_corrige_o_que_foi_medido_a_mais()
    {
        var acompanhamento = Novo();
        acompanhamento.RegistrarMedicao(Outubro, new LancamentoDeMedicao("2.1", 0.6m));

        var glosa = acompanhamento.RegistrarMedicao(Novembro, new LancamentoDeMedicao("2.1", -0.1m));

        Assert.Equal(-0.1m, glosa.Itens[0].Quantidade);
    }

    [Fact]
    public void Glosa_nao_deixa_a_quantidade_medida_negativa()
    {
        var acompanhamento = Novo();
        acompanhamento.RegistrarMedicao(Outubro, new LancamentoDeMedicao("2.1", 0.6m));

        var erro = Erro(() => acompanhamento.RegistrarMedicao(Novembro, new LancamentoDeMedicao("2.1", -0.7m)));

        Assert.Contains("a glosa do item 2.1 deixaria negativa a quantidade medida: -0,10 vb", erro, StringComparison.Ordinal);
    }

    [Fact]
    public void Desfaz_a_ultima_medicao_para_corrigir_e_registrar_de_novo()
    {
        var acompanhamento = Novo();
        acompanhamento.RegistrarMedicao(Outubro, new LancamentoDeMedicao("2.1", 0.6m));
        acompanhamento.RegistrarMedicao(Novembro, new LancamentoDeMedicao("2.1", 0.3m));

        acompanhamento.DesfazerUltimaMedicao();
        var corrigida = acompanhamento.RegistrarMedicao(Novembro, new LancamentoDeMedicao("2.1", 0.4m));

        Assert.Equal(2, corrigida.Numero);
        Assert.Equal(2, acompanhamento.Medicoes.Count);
    }

    [Fact]
    public void Sem_medicao_nao_ha_o_que_desfazer()
    {
        Assert.Equal("Não há medição para desfazer.", Erro(() => Novo().DesfazerUltimaMedicao()));
    }

    [Fact]
    public void Encontra_a_medicao_pelo_numero_e_pelo_mes()
    {
        var acompanhamento = Novo();
        acompanhamento.RegistrarMedicao(Outubro);
        acompanhamento.RegistrarMedicao(Novembro);

        Assert.Equal(Novembro, acompanhamento.Medicao(2).Mes);
        Assert.Equal(1, acompanhamento.MedicaoDoMes(new DateOnly(2026, 10, 9)).Numero);
        Assert.Equal("Não existe a medição 3: há 2 registrada(s).", Erro(() => acompanhamento.Medicao(3)));
        Assert.Equal("Não há medição registrada em 12/2026.", Erro(() => acompanhamento.MedicaoDoMes(Dezembro)));
    }

    [Fact]
    public void O_historico_nao_muda_por_fora()
    {
        var acompanhamento = Novo();
        var medicao = acompanhamento.RegistrarMedicao(Outubro, new LancamentoDeMedicao("2.1", 0.6m));

        Assert.Throws<NotSupportedException>(() => ((ICollection<Medicao>)acompanhamento.Medicoes).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<ItemDaMedicao>)medicao.Itens).Clear());
    }
}
