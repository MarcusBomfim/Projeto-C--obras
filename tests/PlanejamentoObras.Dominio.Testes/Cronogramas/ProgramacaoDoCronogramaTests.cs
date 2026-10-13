using PlanejamentoObras.Dominio.Calendarios;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;

namespace PlanejamentoObras.Dominio.Testes.Cronogramas;

/*
 * O cronograma posto no calendário. A obra começa na segunda 05/10/2026, de
 * segunda a sexta, e a segunda 12/10 é feriado — Nossa Senhora Aparecida.
 *
 * Os dias úteis ficam assim:
 *
 *   dia útil   0    1    2    3    4    5    6    7
 *   data      05   06   07   08   09   13   14   15
 *             seg  ter  qua  qui  sex  ter  qua  qui
 *
 * Do 4 para o 5, o fim de semana e o feriado somem: três dias de calendário
 * que não contam. A obra do canteiro leva 8 dias úteis — do 0 ao 7.
 */
public sealed class ProgramacaoDoCronogramaTests
{
    private static readonly DateOnly Segunda05 = new(2026, 10, 5);
    private static readonly DateOnly Terca06 = new(2026, 10, 6);
    private static readonly DateOnly Sexta09 = new(2026, 10, 9);
    private static readonly DateOnly Sabado10 = new(2026, 10, 10);
    private static readonly DateOnly Terca13 = new(2026, 10, 13);
    private static readonly DateOnly Quarta14 = new(2026, 10, 14);
    private static readonly DateOnly Quinta15 = new(2026, 10, 15);

    private static readonly CalendarioDeTrabalho Calendario =
        CalendarioDeTrabalho.DeSegundaASexta([new DateOnly(2026, 10, 12)]);

    /// <summary>
    /// INI(0) → MOB(5) → LOC(3) → CON(0), com dois desvios:
    /// ILO(0) é o "início da locação" (II com LOC) e PLA(2), a placa da obra,
    /// solta na rede.
    /// </summary>
    private static Cronograma Canteiro()
    {
        var cronograma = new Cronograma("Canteiro");
        cronograma.AdicionarAtividade("INI", "Início da obra", 0);
        cronograma.AdicionarAtividade("MOB", "Mobilização do canteiro", 5);
        cronograma.AdicionarAtividade("LOC", "Locação da obra", 3);
        cronograma.AdicionarAtividade("CON", "Conclusão da locação", 0);
        cronograma.AdicionarAtividade("ILO", "Início da locação", 0);
        cronograma.AdicionarAtividade("PLA", "Placa da obra", 2);

        cronograma.AdicionarDependencia("INI", "MOB");
        cronograma.AdicionarDependencia("MOB", "LOC");
        cronograma.AdicionarDependencia("LOC", "CON");
        cronograma.AdicionarDependencia("LOC", "ILO", TipoDeDependencia.InicioInicio);

        return cronograma;
    }

    private static CronogramaProgramado Programar(Cronograma cronograma, DateOnly? inicio = null) =>
        ProgramacaoDoCronograma.Programar(cronograma, inicio ?? Segunda05, Calendario);

    [Fact]
    public void Atividade_de_cinco_dias_vai_de_segunda_a_sexta()
    {
        var mobilizacao = Programar(Canteiro()).Da("MOB");

        Assert.Equal((Segunda05, Sexta09), (mobilizacao.InicioCedo, mobilizacao.TerminoCedo));
    }

    [Fact]
    public void A_sucessora_pula_o_fim_de_semana_e_o_feriado()
    {
        // A mobilização acaba na sexta 09; sábado, domingo e o feriado de
        // segunda não contam. A locação começa na terça 13.
        var locacao = Programar(Canteiro()).Da("LOC");

        Assert.Equal((Terca13, Quinta15), (locacao.InicioCedo, locacao.TerminoCedo));
    }

    [Fact]
    public void Marco_de_conclusao_cai_no_ultimo_dia_do_que_ele_conclui()
    {
        /*
         * A locação termina na quinta 15. A "conclusão da locação" é na
         * quinta 15 — não na sexta 16, que seria o começo do dia seguinte.
         * É o caso que um cálculo ingênuo erra: em dias úteis inteiros, o fim
         * da quinta e o começo da sexta são o mesmo número.
         */
        var conclusao = Programar(Canteiro()).Da("CON");

        Assert.True(conclusao.EhMarco);
        Assert.Equal(Quinta15, conclusao.InicioCedo);
        Assert.Equal(Quinta15, conclusao.TerminoCedo);
    }

    [Fact]
    public void Marco_de_inicio_cai_no_primeiro_dia_do_que_ele_inicia()
    {
        // O "início da locação" é amarrado ao começo da locação: terça 13.
        Assert.Equal(Terca13, Programar(Canteiro()).Da("ILO").InicioCedo);
    }

    [Fact]
    public void Marco_sem_predecessora_cai_no_primeiro_dia_da_obra()
    {
        Assert.Equal(Segunda05, Programar(Canteiro()).Da("INI").InicioCedo);
    }

    [Fact]
    public void Datas_tarde_mostram_ate_quando_da_para_escorregar()
    {
        /*
         * A placa da obra (2 dias) não segura nada. Pode começar na segunda
         * 05 ou, no limite, na quarta 14 — terminando na quinta 15, junto
         * com a obra. São 6 dias úteis de folga.
         */
        var placa = Programar(Canteiro()).Da("PLA");

        Assert.Equal((Segunda05, Terca06), (placa.InicioCedo, placa.TerminoCedo));
        Assert.Equal((Quarta14, Quinta15), (placa.InicioTarde, placa.TerminoTarde));
        Assert.Equal(6, placa.FolgaTotal);
        Assert.False(placa.EhCritica);
    }

    [Fact]
    public void Marco_nao_aparece_depois_do_fim_da_obra()
    {
        /*
         * O início da locação não segura nada depois dele: pode escorregar
         * até o instante 8, o fim da obra. Lido como começo de dia, o
         * instante 8 seria a sexta 16 — que já não faz parte da obra. O fim
         * da obra é o fim da quinta 15, e é ali que ele aparece.
         */
        var inicioDaLocacao = Programar(Canteiro()).Da("ILO");

        Assert.Equal(Quinta15, inicioDaLocacao.InicioTarde);
        Assert.Equal(3, inicioDaLocacao.FolgaTotal);
    }

    [Fact]
    public void Marco_empurrado_por_um_termino_e_um_inicio_aparece_no_dia_do_inicio()
    {
        /*
         * A primeira medição exige a mobilização concluída (TI) e a locação
         * começada (II). Os dois vínculos caem no mesmo instante: o fim da
         * sexta 09 e o começo da terça 13. Na sexta, a medição viria antes de
         * a locação começar — ela aparece na terça.
         */
        var cronograma = Canteiro();
        cronograma.AdicionarAtividade("MED", "Primeira medição", 0);
        cronograma.AdicionarDependencia("MOB", "MED");
        cronograma.AdicionarDependencia("LOC", "MED", TipoDeDependencia.InicioInicio);

        Assert.Equal(Terca13, Programar(cronograma).Da("MED").InicioCedo);
    }

    [Fact]
    public void Marco_puxado_por_outro_marco_aparece_no_mesmo_dia_que_ele()
    {
        /*
         * O aviso à fiscalização sai junto com o início da locação (TI). Pelo
         * tipo do vínculo, seria a "conclusão" de algo e cairia na sexta 09 —
         * antes do marco que o puxa. Mas um marco é um instante só: o aviso
         * fica do mesmo lado do início da locação, na terça 13.
         */
        var cronograma = Canteiro();
        cronograma.AdicionarAtividade("AVI", "Aviso à fiscalização", 0);
        cronograma.AdicionarDependencia("ILO", "AVI");

        Assert.Equal(Terca13, Programar(cronograma).Da("AVI").InicioCedo);
    }

    [Fact]
    public void A_obra_termina_no_ultimo_dia_de_trabalho()
    {
        var obra = Programar(Canteiro());

        Assert.Equal(Segunda05, obra.DataDeInicio);
        Assert.Equal(Quinta15, obra.DataDeTermino);
        Assert.Equal(8, obra.DuracaoEmDiasUteis);
    }

    [Fact]
    public void O_caminho_critico_aparece_com_as_datas()
    {
        var obra = Programar(Canteiro());

        Assert.Equal(["INI", "MOB", "LOC", "CON"], obra.AtividadesCriticas.Select(atividade => atividade.Codigo.Valor));
        Assert.Equal(3, obra.DependenciasCriticas.Count);
    }

    [Fact]
    public void Obra_que_comeca_no_sabado_comeca_de_fato_no_proximo_dia_util()
    {
        // Sábado 10: domingo e o feriado de segunda ficam para trás.
        var obra = Programar(Canteiro(), Sabado10);

        Assert.Equal(Terca13, obra.DataDeInicio);
        Assert.Equal(Terca13, obra.Da("MOB").InicioCedo);
    }

    [Fact]
    public void A_descricao_vem_do_cronograma()
    {
        Assert.Equal("Mobilização do canteiro", Programar(Canteiro()).Da("MOB").Descricao);
    }

    [Fact]
    public void Cronograma_vazio_comeca_e_termina_no_primeiro_dia_util()
    {
        var obra = Programar(new Cronograma("Vazio"), Sabado10);

        Assert.Equal((Terca13, Terca13, 0), (obra.DataDeInicio, obra.DataDeTermino, obra.DuracaoEmDiasUteis));
        Assert.Empty(obra.Atividades);
    }

    [Fact]
    public void Trocar_o_calendario_muda_as_datas_mas_nao_a_rede()
    {
        // De segunda a sábado, sem o feriado: a mobilização continua
        // acabando na sexta, mas a locação começa no sábado 10 e acaba na
        // terça 13 — dois dias antes. A rede é a mesma; a duração em dias
        // úteis também.
        var obra = ProgramacaoDoCronograma.Programar(Canteiro(), Segunda05, CalendarioDeTrabalho.DeSegundaASabado());

        Assert.Equal(Sexta09, obra.Da("MOB").TerminoCedo);
        Assert.Equal((Sabado10, Terca13), (obra.Da("LOC").InicioCedo, obra.Da("LOC").TerminoCedo));
        Assert.Equal(8, obra.DuracaoEmDiasUteis);
    }

    [Fact]
    public void Guarda_o_calendario_em_que_foi_programado()
    {
        // Quem precisar dos dias de trabalho de uma atividade usa este, e não
        // outro que discordaria das datas.
        Assert.Same(Calendario, Programar(Canteiro()).Calendario);
    }

    [Fact]
    public void Acusa_atividade_que_nao_esta_no_cronograma_programado()
    {
        Assert.Throws<ExcecaoDeDominio>(() => Programar(Canteiro()).Da("XYZ"));
    }
}
