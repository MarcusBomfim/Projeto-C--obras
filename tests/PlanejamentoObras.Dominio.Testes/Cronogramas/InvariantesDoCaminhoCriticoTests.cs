using PlanejamentoObras.Dominio.Cronogramas;

namespace PlanejamentoObras.Dominio.Testes.Cronogramas;

/*
 * Testes por propriedade, sem biblioteca: centenas de redes aleatórias, e em
 * cada uma, as propriedades que QUALQUER cálculo de caminho crítico correto
 * tem que satisfazer.
 *
 * Os testes com exemplo conferem casos que alguém pensou em escrever. Estes
 * conferem os que ninguém pensou: rede com marco no meio, TT encadeado com IT,
 * defasagem negativa em vínculo que já estava folgado. Se o algoritmo tiver um
 * caso esquecido, é aqui que ele aparece.
 */
public sealed class InvariantesDoCaminhoCriticoTests
{
    [Theory]
    [MemberData(nameof(RedesAleatorias.Lotes), MemberType = typeof(RedesAleatorias))]
    public void Toda_rede_aleatoria_satisfaz_as_propriedades_do_cpm(int lote)
    {
        foreach (var semente in RedesAleatorias.SementesDoLote(lote))
        {
            var cronograma = RedesAleatorias.Gerar(semente);
            var analise = CalculadoraDeCaminhoCritico.Analisar(cronograma);

            ConferirPropriedades(cronograma, analise, semente);
        }
    }

    private static void ConferirPropriedades(Cronograma cronograma, AnaliseDoCaminhoCritico analise, int semente)
    {
        var duracaoDaObra = analise.DuracaoEmDiasUteis;
        var porCodigo = analise.Atividades.ToDictionary(tempos => tempos.Codigo);

        Assert.True(cronograma.Atividades.Count == analise.Atividades.Count, $"semente {semente}: atividade perdida na análise");

        foreach (var tempos in analise.Atividades)
        {
            var onde = $"semente {semente}, atividade {tempos.Codigo}";

            // Nenhuma atividade começa antes da obra nem termina depois dela.
            Assert.True(tempos.InicioCedo >= 0, $"{onde}: início cedo negativo");
            Assert.True(tempos.TerminoTarde <= duracaoDaObra, $"{onde}: término tarde depois do fim da obra");

            // A duração se mantém nos dois cronogramas.
            Assert.Equal(tempos.DuracaoEmDiasUteis, tempos.TerminoCedo - tempos.InicioCedo);
            Assert.Equal(tempos.DuracaoEmDiasUteis, tempos.TerminoTarde - tempos.InicioTarde);

            // Tarde nunca vem antes de cedo; e a folga livre cabe na total.
            Assert.True(tempos.FolgaTotal >= 0, $"{onde}: folga total negativa ({tempos.FolgaTotal})");
            Assert.Equal(tempos.InicioTarde - tempos.InicioCedo, tempos.FolgaTotal);
            Assert.True(tempos.FolgaLivre >= 0 && tempos.FolgaLivre <= tempos.FolgaTotal, $"{onde}: folga livre {tempos.FolgaLivre} fora de [0, {tempos.FolgaTotal}]");
        }

        if (analise.Atividades.Count > 0)
        {
            // A duração da obra é a do caminho mais longo...
            Assert.Equal(duracaoDaObra, analise.Atividades.Max(tempos => tempos.TerminoCedo));

            // ...e quem termina por último é crítico. Toda rede não vazia tem
            // ao menos uma atividade crítica.
            Assert.All(
                analise.Atividades.Where(tempos => tempos.TerminoCedo == duracaoDaObra),
                tempos => Assert.True(tempos.EhCritica, $"semente {semente}: {tempos.Codigo} termina por último e não é crítica"));
        }

        foreach (var vinculo in cronograma.Dependencias)
        {
            var onde = $"semente {semente}, vínculo {vinculo}";
            var predecessora = porCodigo[vinculo.Predecessora];
            var sucessora = porCodigo[vinculo.Sucessora];

            // Os dois cronogramas — cedo e tarde — respeitam todo vínculo.
            Assert.True(
                Sobra(vinculo, predecessora.InicioCedo, predecessora.TerminoCedo, sucessora.InicioCedo, sucessora.TerminoCedo) >= 0,
                $"{onde}: violado no cronograma cedo");

            Assert.True(
                Sobra(vinculo, predecessora.InicioTarde, predecessora.TerminoTarde, sucessora.InicioTarde, sucessora.TerminoTarde) >= 0,
                $"{onde}: violado no cronograma tarde");
        }

        foreach (var tempos in analise.Atividades)
        {
            var onde = $"semente {semente}, atividade {tempos.Codigo}";

            /*
             * O cedo é o MAIS cedo possível: quem não começa no dia 0 está
             * sendo segurado por algum vínculo esticado. Se nenhum vínculo
             * a segurasse, ela poderia começar antes — e o cálculo teria
             * deixado folga sem motivo.
             */
            var seguradaPorAlgum = cronograma.Dependencias
                .Where(vinculo => vinculo.Sucessora == tempos.Codigo)
                .Any(vinculo =>
                {
                    var predecessora = porCodigo[vinculo.Predecessora];
                    return Sobra(vinculo, predecessora.InicioCedo, predecessora.TerminoCedo, tempos.InicioCedo, tempos.TerminoCedo) == 0;
                });

            Assert.True(tempos.InicioCedo == 0 || seguradaPorAlgum, $"{onde}: começa no dia {tempos.InicioCedo} sem nada que a segure");

            // O tarde é o MAIS tarde possível: quem não termina no fim da obra
            // está segurando algum vínculo esticado.
            var segurandoAlgum = cronograma.Dependencias
                .Where(vinculo => vinculo.Predecessora == tempos.Codigo)
                .Any(vinculo =>
                {
                    var sucessora = porCodigo[vinculo.Sucessora];
                    return Sobra(vinculo, tempos.InicioTarde, tempos.TerminoTarde, sucessora.InicioTarde, sucessora.TerminoTarde) == 0;
                });

            Assert.True(tempos.TerminoTarde == duracaoDaObra || segurandoAlgum, $"{onde}: termina tarde no dia {tempos.TerminoTarde} sem segurar nada");
        }

        // Vínculo crítico liga duas críticas, sem folga entre elas.
        foreach (var vinculo in analise.DependenciasCriticas)
        {
            var predecessora = porCodigo[vinculo.Predecessora];
            var sucessora = porCodigo[vinculo.Sucessora];

            Assert.True(predecessora.EhCritica && sucessora.EhCritica, $"semente {semente}: vínculo crítico {vinculo} com ponta não crítica");
            Assert.Equal(0, Sobra(vinculo, predecessora.InicioCedo, predecessora.TerminoCedo, sucessora.InicioCedo, sucessora.TerminoCedo));
        }
    }

    /// <summary>
    /// Quanto o vínculo tem de sobra num cronograma: o ponto amarrado da
    /// sucessora menos (a âncora da predecessora mais a defasagem). Negativo
    /// é vínculo violado; zero é vínculo esticado.
    /// </summary>
    /// <remarks>
    /// Escrita aqui de novo, de propósito, em vez de reaproveitar a do
    /// algoritmo: um teste que usa o código testado para conferir o código
    /// testado confere nada.
    /// </remarks>
    private static int Sobra(Dependencia vinculo, int inicioP, int terminoP, int inicioS, int terminoS)
    {
        var ancora = vinculo.Tipo switch
        {
            TipoDeDependencia.TerminoInicio or TipoDeDependencia.TerminoTermino => terminoP,
            _ => inicioP,
        };

        var pontoDaSucessora = vinculo.Tipo switch
        {
            TipoDeDependencia.TerminoTermino or TipoDeDependencia.InicioTermino => terminoS,
            _ => inicioS,
        };

        return pontoDaSucessora - (ancora + vinculo.DefasagemEmDiasUteis);
    }
}
