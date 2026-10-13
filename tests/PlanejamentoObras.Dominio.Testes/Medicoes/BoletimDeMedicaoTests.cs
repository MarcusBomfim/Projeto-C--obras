using PlanejamentoObras.Dominio.Calendarios;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;

namespace PlanejamentoObras.Dominio.Testes.Medicoes;

/*
 * A CasaTerrea medida em dois meses. Todo item tem quantidade 1, então a
 * quantidade é a fração executada.
 *
 *   medição 1, outubro:   1.1 inteiro, 1.2 inteiro, 2.1 em 60 %
 *   medição 2, novembro:  2.1 nos 40 % que faltam, 2.2 em 50 %
 *
 *              contratado   medição 1   medição 2   acumulado
 *   1.1          10.000,00   10.000,00           —   10.000,00
 *   1.2           1.000,00    1.000,00           —    1.000,00
 *   2.1          10.000,00    6.000,00    4.000,00   10.000,00
 *   2.2          20.000,00           —   10.000,00   10.000,00
 *   2.3             500,00           —           —           —
 *   obra         41.500,00   17.000,00   14.000,00   31.000,00
 */
public sealed class BoletimDeMedicaoTests
{
    private static Dinheiro R(decimal valor) => Dinheiro.Reais(valor);

    private static Acompanhamento DuasMedicoes()
    {
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());

        acompanhamento.RegistrarMedicao(
            new DateOnly(2026, 10, 1),
            new LancamentoDeMedicao("1.1", 1m, 9_000m),
            new LancamentoDeMedicao("1.2", 1m, 800m),
            new LancamentoDeMedicao("2.1", 0.6m, 6_200m));

        acompanhamento.RegistrarMedicao(
            new DateOnly(2026, 11, 1),
            new LancamentoDeMedicao("2.1", 0.4m, 3_900m),
            new LancamentoDeMedicao("2.2", 0.5m, 9_500m));

        return acompanhamento;
    }

    [Fact]
    public void Primeiro_boletim_mede_o_que_andou_no_mes()
    {
        var boletim = EmissaoDoBoletim.Emitir(DuasMedicoes(), 1);
        var fundacoes = boletim.Item("2.1");

        Assert.Equal((1, new DateOnly(2026, 10, 1)), (boletim.Numero, boletim.Mes));
        Assert.Equal((0m, 0.6m, 0.6m), (fundacoes.QuantidadeAnterior, fundacoes.QuantidadeNoMes, fundacoes.QuantidadeAcumulada));
        Assert.Equal((R(0m), R(6_000m), R(6_000m)), (fundacoes.ValorAnterior, fundacoes.ValorNoMes, fundacoes.ValorAcumulado));
        Assert.Equal(R(4_000m), fundacoes.Saldo);
        Assert.Equal(0.6m, fundacoes.PercentualExecutado);
    }

    [Fact]
    public void O_valor_da_medicao_e_o_que_se_paga_no_mes()
    {
        var boletim = EmissaoDoBoletim.Emitir(DuasMedicoes(), 1);

        Assert.Equal(R(17_000m), boletim.ValorNoMes);
        Assert.Equal(R(17_000m), boletim.ValorAcumulado);
        Assert.Equal(R(41_500m), boletim.ValorContratado);
        Assert.Equal(R(24_500m), boletim.Saldo);
    }

    [Fact]
    public void Segundo_boletim_parte_do_acumulado_do_primeiro()
    {
        var boletim = EmissaoDoBoletim.Emitir(DuasMedicoes(), 2);
        var fundacoes = boletim.Item("2.1");

        Assert.Equal((0.6m, 0.4m, 1m), (fundacoes.QuantidadeAnterior, fundacoes.QuantidadeNoMes, fundacoes.QuantidadeAcumulada));
        Assert.Equal((R(6_000m), R(4_000m), R(10_000m)), (fundacoes.ValorAnterior, fundacoes.ValorNoMes, fundacoes.ValorAcumulado));
        Assert.Equal(1m, fundacoes.PercentualExecutado);
        Assert.Equal((R(17_000m), R(14_000m), R(31_000m)), (boletim.ValorAnterior, boletim.ValorNoMes, boletim.ValorAcumulado));
        Assert.Equal(R(10_500m), boletim.Saldo);
    }

    [Fact]
    public void Etapa_soma_os_itens_dela()
    {
        var boletim = EmissaoDoBoletim.Emitir(DuasMedicoes(), 2);
        var obra = boletim.Linha("2");

        Assert.IsType<EtapaDoBoletim>(obra);
        Assert.Equal(R(30_500m), obra.ValorContratado);
        Assert.Equal((R(6_000m), R(14_000m), R(20_000m)), (obra.ValorAnterior, obra.ValorNoMes, obra.ValorAcumulado));
        Assert.Equal(R(10_500m), obra.Saldo);
    }

    [Fact]
    public void Traz_todas_as_linhas_inclusive_as_que_nao_andaram()
    {
        var boletim = EmissaoDoBoletim.Emitir(DuasMedicoes(), 2);

        Assert.Equal(["1", "1.1", "1.2", "2", "2.1", "2.2", "2.3"], boletim.Linhas.Select(linha => linha.Numero.Valor));
        Assert.Equal(R(0m), boletim.Item("2.3").ValorAcumulado);
    }

    [Fact]
    public void Item_todo_medido_vale_exatamente_o_contratado()
    {
        /*
         * Um item de R$ 0,15 medido em duas metades. Cada metade vale 0,075:
         * calculando o mês direto, 0,08 + 0,08 = 0,16 — um centavo a mais que
         * o contratado. Pela diferença dos acumulados: 0,08 no primeiro mês
         * (0,075 → 0,08, NBR 5891) e 0,15 − 0,08 = 0,07 no segundo.
         */
        var cronograma = new Cronograma("Tela");
        cronograma.AdicionarAtividade("A", "Tela", 2);
        var obra = ProgramacaoDoCronograma.Programar(cronograma, new DateOnly(2026, 10, 19), CalendarioDeTrabalho.DeSegundaASexta());

        var baseDeCustos = new BaseDeCustos("Tela", new DateOnly(2026, 9, 1));
        baseDeCustos.AdicionarInsumo("TEL", "Tela", "m²", TipoDeInsumo.Material, 0.15m);
        var orcamento = new Orcamento("Tela", Bdi.Zero);
        orcamento.AdicionarEtapa("Tudo");
        orcamento.AdicionarItem("1", "TEL", 1m);
        orcamento.VincularAtividade("1.1", "A");

        var linhaDeBase = LinhaDeBase.Estabelecer("LB", new DateOnly(2026, 10, 1), CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos), obra);
        var acompanhamento = new Acompanhamento(linhaDeBase);
        acompanhamento.RegistrarMedicao(new DateOnly(2026, 10, 1), new LancamentoDeMedicao("1.1", 0.5m));
        acompanhamento.RegistrarMedicao(new DateOnly(2026, 11, 1), new LancamentoDeMedicao("1.1", 0.5m));

        Assert.Equal(R(0.08m), EmissaoDoBoletim.Emitir(acompanhamento, 1).Item("1.1").ValorNoMes);
        Assert.Equal(R(0.07m), EmissaoDoBoletim.Emitir(acompanhamento, 2).Item("1.1").ValorNoMes);
        Assert.Equal(R(0.15m), EmissaoDoBoletim.Emitir(acompanhamento, 2).Item("1.1").ValorAcumulado);
    }

    [Fact]
    public void Glosa_aparece_como_valor_negativo_no_mes()
    {
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());
        acompanhamento.RegistrarMedicao(new DateOnly(2026, 10, 1), new LancamentoDeMedicao("2.1", 0.6m));
        acompanhamento.RegistrarMedicao(new DateOnly(2026, 11, 1), new LancamentoDeMedicao("2.1", -0.1m));

        var boletim = EmissaoDoBoletim.Emitir(acompanhamento, 2);

        Assert.Equal(R(-1_000m), boletim.Item("2.1").ValorNoMes);
        Assert.Equal(R(5_000m), boletim.Item("2.1").ValorAcumulado);
    }

    [Fact]
    public void Acusa_medicao_que_nao_existe_e_linha_que_nao_existe()
    {
        var acompanhamento = DuasMedicoes();

        Assert.Throws<ExcecaoDeDominio>(() => EmissaoDoBoletim.Emitir(acompanhamento, 3));
        Assert.Throws<ExcecaoDeDominio>(() => EmissaoDoBoletim.Emitir(acompanhamento, 2).Linha("9"));
        Assert.Throws<ExcecaoDeDominio>(() => EmissaoDoBoletim.Emitir(acompanhamento, 2).Item("1"));
    }
}
