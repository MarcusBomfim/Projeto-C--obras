using PlanejamentoObras.Dominio.Calendarios;
using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Testes.Calendarios;

/*
 * As datas são de outubro de 2026. Segunda-feira 5 a domingo 11, e a
 * segunda-feira 12 é feriado nacional — Nossa Senhora Aparecida. Um feriado
 * de verdade caindo numa segunda é exatamente o caso que derruba conta de
 * prazo feita de cabeça.
 */
public sealed class CalendarioDeTrabalhoTests
{
    private static readonly DateOnly Segunda05 = new(2026, 10, 5);
    private static readonly DateOnly Sexta09 = new(2026, 10, 9);
    private static readonly DateOnly Sabado10 = new(2026, 10, 10);
    private static readonly DateOnly Domingo11 = new(2026, 10, 11);
    private static readonly DateOnly Aparecida12 = new(2026, 10, 12);
    private static readonly DateOnly Terca13 = new(2026, 10, 13);

    private static CalendarioDeTrabalho ComAparecida() => CalendarioDeTrabalho.DeSegundaASexta([Aparecida12]);

    [Fact]
    public void Dia_de_semana_e_util_e_fim_de_semana_nao()
    {
        var calendario = CalendarioDeTrabalho.DeSegundaASexta();

        Assert.True(calendario.EhDiaUtil(Segunda05));
        Assert.True(calendario.EhDiaUtil(Sexta09));
        Assert.False(calendario.EhDiaUtil(Sabado10));
        Assert.False(calendario.EhDiaUtil(Domingo11));
    }

    [Fact]
    public void Feriado_nao_e_dia_util_mesmo_caindo_em_dia_de_semana()
    {
        Assert.False(ComAparecida().EhDiaUtil(Aparecida12));
    }

    [Fact]
    public void Obra_que_comeca_no_sabado_tem_o_dia_zero_no_proximo_dia_util()
    {
        // Sábado, domingo e o feriado de segunda ficam para trás.
        Assert.Equal(Terca13, ComAparecida().PrimeiroDiaUtilAPartirDe(Sabado10));
    }

    [Fact]
    public void Conta_dias_uteis_pulando_o_fim_de_semana()
    {
        var calendario = CalendarioDeTrabalho.DeSegundaASexta();

        Assert.Equal(Segunda05, calendario.DiaUtil(Segunda05, 0));
        Assert.Equal(Sexta09, calendario.DiaUtil(Segunda05, 4));
        Assert.Equal(Aparecida12, calendario.DiaUtil(Segunda05, 5));
    }

    [Fact]
    public void O_feriado_empurra_a_contagem_um_dia_para_frente()
    {
        // Sem o feriado, o 6º dia útil é a segunda 12; com ele, a terça 13.
        Assert.Equal(Terca13, ComAparecida().DiaUtil(Segunda05, 5));
    }

    [Fact]
    public void Lista_os_dias_uteis_em_ordem()
    {
        var dias = ComAparecida().DiasUteisAPartirDe(Sabado10, 3);

        Assert.Equal([Terca13, new(2026, 10, 14), new(2026, 10, 15)], dias);
    }

    [Fact]
    public void Lista_vazia_quando_a_quantidade_e_zero()
    {
        Assert.Empty(ComAparecida().DiasUteisAPartirDe(Segunda05, 0));
    }

    [Fact]
    public void Conta_os_dias_uteis_de_um_intervalo_semiaberto()
    {
        // De 05 a 18: cinco dias na primeira semana e quatro na segunda,
        // porque a segunda-feira 12 é feriado. O dia 19 não entra.
        Assert.Equal(9, ComAparecida().DiasUteisEntre(Segunda05, new DateOnly(2026, 10, 19)));
        Assert.Equal(0, ComAparecida().DiasUteisEntre(Sexta09, Sexta09));
        Assert.Equal(0, ComAparecida().DiasUteisEntre(Sexta09, Segunda05));
    }

    [Fact]
    public void Calendario_de_segunda_a_sabado_trabalha_no_sabado()
    {
        var calendario = CalendarioDeTrabalho.DeSegundaASabado();

        Assert.True(calendario.EhDiaUtil(Sabado10));
        Assert.False(calendario.EhDiaUtil(Domingo11));
    }

    [Fact]
    public void Recusa_calendario_sem_nenhum_dia_util()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new CalendarioDeTrabalho([]));

        Assert.Contains("ao menos um dia útil", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_numero_de_dia_negativo()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ComAparecida().DiaUtil(Segunda05, -1));
    }
}
