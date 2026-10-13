using PlanejamentoObras.Dominio.Calendarios;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;

/*
 * Obra a partir da segunda 19/10/2026, de segunda a sexta, com os feriados de
 * 02/11 (Finados) e 20/11 (Consciência Negra).
 *
 *   SER  serviços preliminares, 5 dias   19/10 a 23/10          outubro: 5
 *   PLA  placa da obra, 2 dias, solta    cedo 19/10 a 20/10     outubro: 2
 *                                        tarde 16/11 a 17/11    novembro: 2
 *   FUN  fundações, 6 dias, após SER     26/10 a 30/10 e 03/11  outubro: 5, novembro: 1
 *   ALV  alvenaria, 10 dias, após FUN    04/11 a 17/11          novembro: 10
 *   ENT  entrega, marco, após ALV        17/11                  novembro
 *
 * Orçamento com BDI de 25 %, um item por atividade:
 *
 *   1.1  SER  10.000,00   outubro
 *   1.2  PLA   1.000,00   outubro (cedo) ou novembro (tarde)
 *   2.1  FUN  10.000,00   5/6 e 1/6: 8.333,333… e 1.666,666… → 8.333,33 e 1.666,67
 *   2.2  ALV  20.000,00   novembro
 *   2.3  ENT     500,00   novembro, no dia do marco
 *
 *   cedo:   outubro 19.333,33   novembro 22.166,67   total 41.500,00
 *   tarde:  outubro 18.333,33   novembro 23.166,67
 */
public sealed class CalculadoraDoFisicoFinanceiroTests
{
    private static readonly CalendarioDeTrabalho Calendario =
        CalendarioDeTrabalho.DeSegundaASexta([new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 20)]);

    private static readonly DateOnly Outubro = new(2026, 10, 1);
    private static readonly DateOnly Novembro = new(2026, 11, 1);

    private static Dinheiro R(decimal valor) => Dinheiro.Reais(valor);

    private static CronogramaProgramado Obra()
    {
        var cronograma = new Cronograma("Casa térrea");
        cronograma.AdicionarAtividade("SER", "Serviços preliminares", 5);
        cronograma.AdicionarAtividade("PLA", "Placa da obra", 2);
        cronograma.AdicionarAtividade("FUN", "Fundações", 6);
        cronograma.AdicionarAtividade("ALV", "Alvenaria", 10);
        cronograma.AdicionarAtividade("ENT", "Entrega", 0);
        cronograma.AdicionarDependencia("SER", "FUN");
        cronograma.AdicionarDependencia("FUN", "ALV");
        cronograma.AdicionarDependencia("ALV", "ENT");

        return ProgramacaoDoCronograma.Programar(cronograma, new DateOnly(2026, 10, 19), Calendario);
    }

    private static OrcamentoCalculado Orcamento(Action<Orcamento>? ajustar = null)
    {
        var baseDeCustos = new BaseDeCustos("Verbas", new DateOnly(2026, 9, 1));
        baseDeCustos.AdicionarInsumo("SERV", "Serviços preliminares", "vb", TipoDeInsumo.Material, 8_000m);
        baseDeCustos.AdicionarInsumo("PLAC", "Placa da obra", "un", TipoDeInsumo.Material, 800m);
        baseDeCustos.AdicionarInsumo("FUND", "Fundações", "vb", TipoDeInsumo.Material, 8_000m);
        baseDeCustos.AdicionarInsumo("ALVE", "Alvenaria", "vb", TipoDeInsumo.Material, 16_000m);
        baseDeCustos.AdicionarInsumo("LIMP", "Limpeza e entrega", "vb", TipoDeInsumo.Material, 400m);

        var orcamento = new Orcamento("Casa térrea", Bdi.Informado(25m));
        orcamento.AdicionarEtapa("Serviços iniciais");
        orcamento.AdicionarItem("1", "SERV", 1m);
        orcamento.AdicionarItem("1", "PLAC", 1m);
        orcamento.AdicionarEtapa("Obra");
        orcamento.AdicionarItem("2", "FUND", 1m);
        orcamento.AdicionarItem("2", "ALVE", 1m);
        orcamento.AdicionarItem("2", "LIMP", 1m);

        orcamento.VincularAtividade("1.1", "SER");
        orcamento.VincularAtividade("1.2", "PLA");
        orcamento.VincularAtividade("2.1", "FUN");
        orcamento.VincularAtividade("2.2", "ALV");
        orcamento.VincularAtividade("2.3", "ENT");

        ajustar?.Invoke(orcamento);

        return CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos);
    }

    private static CronogramaFisicoFinanceiro FisicoFinanceiro(DatasDoCronograma datas = DatasDoCronograma.Cedo) =>
        CalculadoraDoFisicoFinanceiro.Calcular(Orcamento(), Obra(), datas);

    [Fact]
    public void Um_periodo_por_mes_da_obra()
    {
        var periodos = FisicoFinanceiro().Periodos;

        Assert.Equal([1, 2], periodos.Select(periodo => periodo.Numero));
        Assert.Equal([Outubro, Novembro], periodos.Select(periodo => periodo.Mes));
    }

    [Fact]
    public void Soma_o_que_cada_mes_executa()
    {
        var periodos = FisicoFinanceiro().Periodos;

        Assert.Equal([R(19_333.33m), R(22_166.67m)], periodos.Select(periodo => periodo.Valor));
        Assert.Equal([R(19_333.33m), R(41_500m)], periodos.Select(periodo => periodo.Acumulado));
    }

    [Fact]
    public void O_acumulado_e_a_curva_S()
    {
        var periodos = FisicoFinanceiro().Periodos;

        // 19.333,33 / 41.500,00 = 0,465863…
        Assert.Equal(0.4659m, decimal.Round(periodos[0].Percentual, 4));
        Assert.Equal(0.4659m, decimal.Round(periodos[0].PercentualAcumulado, 4));
        Assert.Equal(1m, periodos[1].PercentualAcumulado);
    }

    [Fact]
    public void Atividade_que_atravessa_o_mes_divide_o_valor_pelos_dias_de_cada_um()
    {
        // Fundações: 5 dias em outubro e 1 em novembro (02/11 é feriado).
        Assert.Equal([R(8_333.33m), R(1_666.67m)], FisicoFinanceiro().Linha("2.1").Valores);
    }

    [Fact]
    public void Marco_concentra_o_valor_no_dia_dele()
    {
        Assert.Equal([R(0m), R(500m)], FisicoFinanceiro().Linha("2.3").Valores);
    }

    [Fact]
    public void Etapa_soma_os_itens_dela_mes_a_mes()
    {
        var fisicoFinanceiro = FisicoFinanceiro();

        Assert.Equal([R(11_000m), R(0m)], fisicoFinanceiro.Linha("1").Valores);
        Assert.Equal([R(8_333.33m), R(22_166.67m)], fisicoFinanceiro.Linha("2").Valores);
    }

    [Fact]
    public void Fecha_com_o_orcamento_ao_centavo()
    {
        var fisicoFinanceiro = FisicoFinanceiro();

        Assert.Equal(R(41_500m), fisicoFinanceiro.Total);
        Assert.Equal(fisicoFinanceiro.Total, fisicoFinanceiro.Periodos[^1].Acumulado);
    }

    [Fact]
    public void Pelas_datas_tarde_o_que_tem_folga_vai_para_depois()
    {
        // A placa tem folga até o fim da obra: no tarde, sai de outubro para novembro.
        var tarde = FisicoFinanceiro(DatasDoCronograma.Tarde);

        Assert.Equal(DatasDoCronograma.Tarde, tarde.Datas);
        Assert.Equal([R(0m), R(1_000m)], tarde.Linha("1.2").Valores);
        Assert.Equal([R(18_333.33m), R(23_166.67m)], tarde.Periodos.Select(periodo => periodo.Valor));
    }

    [Fact]
    public void A_curva_tarde_nunca_passa_a_frente_da_cedo()
    {
        var cedo = FisicoFinanceiro(DatasDoCronograma.Cedo).Periodos;
        var tarde = FisicoFinanceiro(DatasDoCronograma.Tarde).Periodos;

        Assert.All(cedo.Zip(tarde), par => Assert.True(par.Second.Acumulado <= par.First.Acumulado));
    }

    [Fact]
    public void Centavo_que_sobra_no_empate_vai_para_o_mes_mais_cedo()
    {
        /*
         * R$ 0,03 em dois dias: sexta 30/10 e terça 03/11. Cada mês tem 1,5
         * centavo exato; um centavo inteiro para cada, e o que sobra vai para
         * outubro.
         */
        var cronograma = new Cronograma("Empate");
        cronograma.AdicionarAtividade("X", "Dois dias", 2);
        var obra = ProgramacaoDoCronograma.Programar(cronograma, new DateOnly(2026, 10, 30), Calendario);

        var baseDeCustos = new BaseDeCustos("Centavos", new DateOnly(2026, 9, 1));
        baseDeCustos.AdicionarInsumo("TRES", "Três centavos", "vb", TipoDeInsumo.Material, 0.03m);
        var orcamento = new Orcamento("Empate", Bdi.Zero);
        orcamento.AdicionarEtapa("Tudo");
        orcamento.AdicionarItem("1", "TRES", 1m);
        orcamento.VincularAtividade("1.1", "X");

        var fisicoFinanceiro = CalculadoraDoFisicoFinanceiro.Calcular(CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos), obra);

        Assert.Equal([R(0.02m), R(0.01m)], fisicoFinanceiro.Linha("1.1").Valores);
    }

    [Fact]
    public void Mes_sem_execucao_aparece_com_zero()
    {
        // A espera de 40 dias úteis depois de A pula novembro inteiro.
        var cronograma = new Cronograma("Com espera");
        cronograma.AdicionarAtividade("A", "Antes", 2);
        cronograma.AdicionarAtividade("B", "Depois", 2);
        cronograma.AdicionarDependencia("A", "B", TipoDeDependencia.TerminoInicio, 40);
        var obra = ProgramacaoDoCronograma.Programar(cronograma, new DateOnly(2026, 10, 19), Calendario);

        var baseDeCustos = new BaseDeCustos("Verbas", new DateOnly(2026, 9, 1));
        baseDeCustos.AdicionarInsumo("V", "Verba", "vb", TipoDeInsumo.Material, 100m);
        var orcamento = new Orcamento("Com espera", Bdi.Zero);
        orcamento.AdicionarEtapa("Tudo");
        orcamento.AdicionarItem("1", "V", 1m);
        orcamento.AdicionarItem("1", "V", 1m);
        orcamento.VincularAtividade("1.1", "A");
        orcamento.VincularAtividade("1.2", "B");

        var periodos = CalculadoraDoFisicoFinanceiro.Calcular(CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos), obra).Periodos;

        Assert.Equal([R(100m), R(0m), R(100m)], periodos.Select(periodo => periodo.Valor));
    }

    [Fact]
    public void Acusa_de_uma_vez_os_itens_sem_atividade_e_com_atividade_que_nao_existe()
    {
        var orcamento = Orcamento(orcamento =>
        {
            orcamento.DesvincularAtividade("1.1");
            orcamento.DesvincularAtividade("1.2");
            orcamento.VincularAtividade("2.1", "XYZ");
        });

        var erro = Assert.Throws<ExcecaoDeDominio>(() => CalculadoraDoFisicoFinanceiro.Calcular(orcamento, Obra()));

        Assert.Contains("itens sem atividade vinculada: 1.1, 1.2", erro.Message, StringComparison.Ordinal);
        Assert.Contains("atividade que não está no cronograma: 2.1 (XYZ)", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Acusa_linha_que_nao_existe()
    {
        Assert.Throws<ExcecaoDeDominio>(() => FisicoFinanceiro().Linha("9"));
    }
}
