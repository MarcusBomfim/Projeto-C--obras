using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;

namespace PlanejamentoObras.Dominio.Testes.Cronogramas;

/*
 * O cálculo em dias úteis, sem calendário. Cada número esperado aqui foi
 * calculado à mão, com ida e volta pela rede — o teste confere o algoritmo
 * contra a conta de papel, e não contra ele mesmo.
 */
public sealed class CaminhoCriticoTests
{
    private static AnaliseDoCaminhoCritico Analisar(Cronograma cronograma) =>
        CalculadoraDeCaminhoCritico.Analisar(cronograma);

    /// <summary>
    /// Uma obra pequena, do canteiro à entrega das chaves:
    ///
    ///   MOB(5) → FUN(10) → EST(20) ┬→ ALV(15) ┬→ REB(8) → PIN(6) → ENT(0)
    ///                              └→ ELE(12) ┘
    ///
    /// Alvenaria e elétrica correm em paralelo depois da estrutura; o reboco
    /// espera as duas. A elétrica é 3 dias mais curta — é a única com folga.
    /// </summary>
    private static Cronograma ObraDeExemplo()
    {
        var cronograma = new Cronograma("Residencial Vista Serra");
        cronograma.AdicionarAtividade("MOB", "Mobilização do canteiro", 5);
        cronograma.AdicionarAtividade("FUN", "Fundações", 10);
        cronograma.AdicionarAtividade("EST", "Estrutura", 20);
        cronograma.AdicionarAtividade("ALV", "Alvenaria", 15);
        cronograma.AdicionarAtividade("ELE", "Instalações elétricas", 12);
        cronograma.AdicionarAtividade("REB", "Reboco", 8);
        cronograma.AdicionarAtividade("PIN", "Pintura", 6);
        cronograma.AdicionarAtividade("ENT", "Entrega das chaves", 0);

        cronograma.AdicionarDependencia("MOB", "FUN");
        cronograma.AdicionarDependencia("FUN", "EST");
        cronograma.AdicionarDependencia("EST", "ALV");
        cronograma.AdicionarDependencia("EST", "ELE");
        cronograma.AdicionarDependencia("ALV", "REB");
        cronograma.AdicionarDependencia("ELE", "REB");
        cronograma.AdicionarDependencia("REB", "PIN");
        cronograma.AdicionarDependencia("PIN", "ENT");

        return cronograma;
    }

    // ---------- casos de borda ----------

    [Fact]
    public void Cronograma_vazio_tem_duracao_zero()
    {
        var analise = Analisar(new Cronograma("Vazio"));

        Assert.Equal(0, analise.DuracaoEmDiasUteis);
        Assert.Empty(analise.Atividades);
        Assert.Empty(analise.DependenciasCriticas);
    }

    [Fact]
    public void Atividade_sozinha_e_critica_por_definicao()
    {
        var cronograma = new Cronograma("Uma só");
        cronograma.AdicionarAtividade("A", "Única", 7);

        var tempos = Analisar(cronograma).Da("A");

        Assert.Equal((0, 7, 0, 7, 0, 0), (tempos.InicioCedo, tempos.TerminoCedo, tempos.InicioTarde, tempos.TerminoTarde, tempos.FolgaTotal, tempos.FolgaLivre));
        Assert.True(tempos.EhCritica);
    }

    // ---------- a obra de exemplo ----------

    [Theory]
    //          código  IC  TC  IT  TT  folga total  folga livre
    [InlineData("MOB", 0, 5, 0, 5, 0, 0)]
    [InlineData("FUN", 5, 15, 5, 15, 0, 0)]
    [InlineData("EST", 15, 35, 15, 35, 0, 0)]
    [InlineData("ALV", 35, 50, 35, 50, 0, 0)]
    [InlineData("ELE", 35, 47, 38, 50, 3, 3)]
    [InlineData("REB", 50, 58, 50, 58, 0, 0)]
    [InlineData("PIN", 58, 64, 58, 64, 0, 0)]
    [InlineData("ENT", 64, 64, 64, 64, 0, 0)]
    public void Calcula_ida_volta_e_folgas_de_cada_atividade(
        string codigo, int inicioCedo, int terminoCedo, int inicioTarde, int terminoTarde, int folgaTotal, int folgaLivre)
    {
        var tempos = Analisar(ObraDeExemplo()).Da(codigo);

        Assert.Equal(inicioCedo, tempos.InicioCedo);
        Assert.Equal(terminoCedo, tempos.TerminoCedo);
        Assert.Equal(inicioTarde, tempos.InicioTarde);
        Assert.Equal(terminoTarde, tempos.TerminoTarde);
        Assert.Equal(folgaTotal, tempos.FolgaTotal);
        Assert.Equal(folgaLivre, tempos.FolgaLivre);
    }

    [Fact]
    public void A_obra_leva_o_tamanho_do_caminho_mais_longo()
    {
        Assert.Equal(64, Analisar(ObraDeExemplo()).DuracaoEmDiasUteis);
    }

    [Fact]
    public void So_a_eletrica_fica_fora_do_caminho_critico()
    {
        var criticas = Analisar(ObraDeExemplo()).AtividadesCriticas.Select(tempos => tempos.Codigo.Valor);

        Assert.Equal(["MOB", "FUN", "EST", "ALV", "REB", "PIN", "ENT"], criticas);
    }

    [Fact]
    public void Os_vinculos_criticos_sao_os_que_ligam_criticas_sem_folga()
    {
        var vinculos = Analisar(ObraDeExemplo()).DependenciasCriticas.Select(vinculo => vinculo.ToString());

        // Os dois vínculos da elétrica ficam de fora: ela tem folga.
        Assert.Equal(
            ["MOB → FUN (TI)", "FUN → EST (TI)", "EST → ALV (TI)", "ALV → REB (TI)", "REB → PIN (TI)", "PIN → ENT (TI)"],
            vinculos);
    }

    [Fact]
    public void As_atividades_saem_na_ordem_do_gantt()
    {
        // Pelo início cedo; ALV e ELE começam juntas no dia 35 e ficam na
        // ordem de cadastro.
        var ordem = Analisar(ObraDeExemplo()).Atividades.Select(tempos => tempos.Codigo.Valor);

        Assert.Equal(["MOB", "FUN", "EST", "ALV", "ELE", "REB", "PIN", "ENT"], ordem);
    }

    // ---------- folga livre x folga total ----------

    [Fact]
    public void Folga_livre_pode_ser_menor_que_a_total()
    {
        /*
         * A compra das telhas (3) e o transporte (2) correm em paralelo com a
         * estrutura (10); a cobertura espera os dois ramos.
         *
         *   EST(10) ─────────────┐
         *   COM(3) → TRA(2) ─────┴→ COB(5)
         *
         * A compra tem 5 dias de folga TOTAL: pode atrasar 5 sem atrasar a
         * obra. Mas folga LIVRE zero: qualquer atraso dela já empurra o
         * transporte. É a diferença que o planejador precisa ver antes de
         * dizer ao comprador que "tem folga".
         */
        var cronograma = new Cronograma("Cobertura");
        cronograma.AdicionarAtividade("EST", "Estrutura", 10);
        cronograma.AdicionarAtividade("COM", "Compra das telhas", 3);
        cronograma.AdicionarAtividade("TRA", "Transporte das telhas", 2);
        cronograma.AdicionarAtividade("COB", "Cobertura", 5);
        cronograma.AdicionarDependencia("EST", "COB");
        cronograma.AdicionarDependencia("COM", "TRA");
        cronograma.AdicionarDependencia("TRA", "COB");

        var analise = Analisar(cronograma);

        Assert.Equal((5, 0), (analise.Da("COM").FolgaTotal, analise.Da("COM").FolgaLivre));
        Assert.Equal((5, 5), (analise.Da("TRA").FolgaTotal, analise.Da("TRA").FolgaLivre));
        Assert.True(analise.Da("EST").EhCritica);
    }

    // ---------- os quatro tipos de vínculo ----------

    private static Cronograma DuasAtividades(int duracaoDaPredecessora, int duracaoDaSucessora, TipoDeDependencia tipo, int defasagem = 0)
    {
        var cronograma = new Cronograma("Par");
        cronograma.AdicionarAtividade("P", "Predecessora", duracaoDaPredecessora);
        cronograma.AdicionarAtividade("S", "Sucessora", duracaoDaSucessora);
        cronograma.AdicionarDependencia("P", "S", tipo, defasagem);

        return cronograma;
    }

    [Fact]
    public void Termino_inicio_com_defasagem_espera_a_cura()
    {
        // Desforma 3 dias úteis depois da concretagem terminar.
        var analise = Analisar(DuasAtividades(10, 5, TipoDeDependencia.TerminoInicio, 3));

        Assert.Equal((13, 18), (analise.Da("S").InicioCedo, analise.Da("S").TerminoCedo));
        Assert.Equal(18, analise.DuracaoEmDiasUteis);
    }

    [Fact]
    public void Termino_inicio_com_defasagem_negativa_antecipa()
    {
        // A pintura começa 3 dias antes de o reboco acabar.
        var analise = Analisar(DuasAtividades(10, 5, TipoDeDependencia.TerminoInicio, -3));

        Assert.Equal(7, analise.Da("S").InicioCedo);
        Assert.Equal(12, analise.DuracaoEmDiasUteis);
        Assert.True(analise.Da("P").EhCritica);
        Assert.True(analise.Da("S").EhCritica);
    }

    [Fact]
    public void Inicio_inicio_amarra_os_dois_comecos()
    {
        var analise = Analisar(DuasAtividades(10, 4, TipoDeDependencia.InicioInicio, 2));

        // S começa 2 dias depois de P começar, e cabe inteira antes de P acabar.
        Assert.Equal((2, 6, 6, 10), (analise.Da("S").InicioCedo, analise.Da("S").TerminoCedo, analise.Da("S").InicioTarde, analise.Da("S").TerminoTarde));
        Assert.Equal(4, analise.Da("S").FolgaTotal);
        Assert.True(analise.Da("P").EhCritica);
    }

    [Fact]
    public void Termino_termino_faz_as_duas_acabarem_juntas()
    {
        // A limpeza (4) termina junto com a pintura (10).
        var analise = Analisar(DuasAtividades(10, 4, TipoDeDependencia.TerminoTermino));

        Assert.Equal((6, 10), (analise.Da("S").InicioCedo, analise.Da("S").TerminoCedo));
        Assert.True(analise.Da("P").EhCritica);
        Assert.True(analise.Da("S").EhCritica);
    }

    [Fact]
    public void Termino_termino_com_sucessora_mais_longa_nao_comeca_antes_da_obra()
    {
        /*
         * S (15) precisa terminar depois de P (10) terminar. A conta crua
         * mandaria S começar no dia -5; ninguém começa antes da obra. S
         * começa no 0, vira a atividade mais longa — e P ganha folga.
         */
        var analise = Analisar(DuasAtividades(10, 15, TipoDeDependencia.TerminoTermino));

        Assert.Equal((0, 15), (analise.Da("S").InicioCedo, analise.Da("S").TerminoCedo));
        Assert.Equal(15, analise.DuracaoEmDiasUteis);
        Assert.Equal(5, analise.Da("P").FolgaTotal);
        Assert.True(analise.Da("S").EhCritica);
    }

    [Fact]
    public void Inicio_termino_so_restringe_o_fim_da_sucessora()
    {
        // O andaime provisório (3) só pode sair depois que o definitivo (10)
        // começar — o que, aqui, não segura nada.
        var analise = Analisar(DuasAtividades(10, 3, TipoDeDependencia.InicioTermino));

        Assert.Equal((0, 3), (analise.Da("S").InicioCedo, analise.Da("S").TerminoCedo));
        Assert.Equal(7, analise.Da("S").FolgaTotal);
        Assert.True(analise.Da("P").EhCritica);
    }

    // ---------- caminhos e marcos ----------

    [Fact]
    public void Dois_caminhos_de_mesmo_tamanho_sao_ambos_criticos()
    {
        var cronograma = new Cronograma("Paralelo");
        cronograma.AdicionarAtividade("A", "Ramo A", 5);
        cronograma.AdicionarAtividade("B", "Ramo B", 5);
        cronograma.AdicionarAtividade("C", "Fecha os dois", 5);
        cronograma.AdicionarDependencia("A", "C");
        cronograma.AdicionarDependencia("B", "C");

        var analise = Analisar(cronograma);

        Assert.Equal(3, analise.AtividadesCriticas.Count);
        Assert.Equal(2, analise.DependenciasCriticas.Count);
    }

    [Fact]
    public void Marco_que_conclui_algo_vem_do_termino_da_predecessora()
    {
        var analise = Analisar(ObraDeExemplo());

        Assert.True(analise.Da("ENT").EhMarco);
        Assert.True(analise.Da("ENT").MarcoDeTermino);
    }

    [Fact]
    public void Marco_amarrado_ao_inicio_de_outra_atividade_e_de_inicio()
    {
        var cronograma = new Cronograma("Marcos");
        cronograma.AdicionarAtividade("ALV", "Alvenaria", 10);
        cronograma.AdicionarAtividade("IREB", "Início do reboco", 0);
        cronograma.AdicionarAtividade("OBRA", "Início da obra", 0);
        cronograma.AdicionarDependencia("ALV", "IREB", TipoDeDependencia.InicioInicio, 3);

        var analise = Analisar(cronograma);

        Assert.Equal(3, analise.Da("IREB").InicioCedo);
        Assert.False(analise.Da("IREB").MarcoDeTermino);
        Assert.False(analise.Da("OBRA").MarcoDeTermino);
    }

    [Fact]
    public void Marco_empurrado_por_termino_e_inicio_no_mesmo_instante_e_de_inicio()
    {
        /*
         * A medição exige a mobilização concluída (TI) e a locação começada
         * (II), e a locação começa quando a mobilização acaba: os dois
         * vínculos dão o instante 5. Para um é o fim do dia 4; para o outro,
         * o começo do dia 5. A medição tem que vir depois dos dois.
         *
         * O vínculo de término vem primeiro de propósito: o empate tem que
         * ser decidido pela regra, não pela ordem de cadastro.
         */
        var cronograma = new Cronograma("Medição");
        cronograma.AdicionarAtividade("MOB", "Mobilização", 5);
        cronograma.AdicionarAtividade("LOC", "Locação", 3);
        cronograma.AdicionarAtividade("MED", "Primeira medição", 0);
        cronograma.AdicionarDependencia("MOB", "LOC");
        cronograma.AdicionarDependencia("MOB", "MED");
        cronograma.AdicionarDependencia("LOC", "MED", TipoDeDependencia.InicioInicio);

        var medicao = Analisar(cronograma).Da("MED");

        Assert.Equal(5, medicao.InicioCedo);
        Assert.False(medicao.MarcoDeTermino);
    }

    [Fact]
    public void Marco_puxado_por_outro_marco_fica_do_lado_dele()
    {
        /*
         * Um marco é um instante só, sem início e término separados. Quem
         * vem dele fica do mesmo lado, qualquer que seja o tipo do vínculo:
         *
         *   ILO (início da locação, de início) ─TI→ AVI: de início
         *   CON (conclusão da locação, de término) ─II→ REL: de término
         */
        var cronograma = new Cronograma("Marcos em cadeia");
        cronograma.AdicionarAtividade("MOB", "Mobilização", 5);
        cronograma.AdicionarAtividade("LOC", "Locação", 3);
        cronograma.AdicionarAtividade("ILO", "Início da locação", 0);
        cronograma.AdicionarAtividade("AVI", "Aviso à fiscalização", 0);
        cronograma.AdicionarAtividade("CON", "Conclusão da locação", 0);
        cronograma.AdicionarAtividade("REL", "Relatório da locação", 0);
        cronograma.AdicionarDependencia("MOB", "LOC");
        cronograma.AdicionarDependencia("LOC", "ILO", TipoDeDependencia.InicioInicio);
        cronograma.AdicionarDependencia("ILO", "AVI");
        cronograma.AdicionarDependencia("LOC", "CON");
        cronograma.AdicionarDependencia("CON", "REL", TipoDeDependencia.InicioInicio);

        var analise = Analisar(cronograma);

        Assert.False(analise.Da("AVI").MarcoDeTermino);
        Assert.True(analise.Da("REL").MarcoDeTermino);
    }

    [Fact]
    public void Atividade_que_nao_e_marco_nunca_e_marco_de_termino()
    {
        Assert.All(
            Analisar(ObraDeExemplo()).Atividades.Where(tempos => !tempos.EhMarco),
            tempos => Assert.False(tempos.MarcoDeTermino));
    }

    [Fact]
    public void Acusa_atividade_que_nao_esta_na_analise()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Analisar(ObraDeExemplo()).Da("XYZ"));

        Assert.Contains("XYZ", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mudar_a_rede_e_recalcular_reflete_a_mudanca()
    {
        // A análise é fotografia: a rede muda, calcula-se outra.
        var cronograma = ObraDeExemplo();
        Assert.Equal(64, Analisar(cronograma).DuracaoEmDiasUteis);

        // A elétrica passa a ser 5 dias mais longa que a alvenaria: troca o
        // caminho crítico de ramo.
        cronograma.AlterarDuracaoDaAtividade("ELE", 20);
        var depois = Analisar(cronograma);

        Assert.Equal(69, depois.DuracaoEmDiasUteis);
        Assert.True(depois.Da("ELE").EhCritica);
        Assert.Equal(5, depois.Da("ALV").FolgaTotal);
    }
}
