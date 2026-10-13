using PlanejamentoObras.Dominio.Calendarios;
using PlanejamentoObras.Dominio.Cronogramas;

namespace PlanejamentoObras.Dominio.Testes.Cronogramas;

/*
 * As mesmas redes sorteadas, agora no calendário, cada uma começando num dia
 * diferente — fim de semana e feriado inclusive. A conta em dias úteis já foi
 * conferida; aqui se confere o que o engenheiro lê no Gantt: as datas.
 */
public sealed class InvariantesDaProgramacaoTests
{
    // Segunda a sexta, com os feriados nacionais de 2026 e 2027.
    private static readonly CalendarioDeTrabalho Calendario = CalendarioDeTrabalho.DeSegundaASexta(
    [
        new(2026, 10, 12), new(2026, 11, 2), new(2026, 11, 20), new(2026, 12, 25),
        new(2027, 1, 1), new(2027, 3, 26), new(2027, 4, 21), new(2027, 5, 1),
        new(2027, 9, 7), new(2027, 10, 12), new(2027, 11, 2), new(2027, 11, 15),
        new(2027, 11, 20), new(2027, 12, 25),
    ]);

    private static readonly DateOnly PrimeiroInicio = new(2026, 10, 1);

    [Theory]
    [MemberData(nameof(RedesAleatorias.Lotes), MemberType = typeof(RedesAleatorias))]
    public void Todo_cronograma_programado_e_coerente_com_a_conta_em_dias_uteis(int lote)
    {
        foreach (var semente in RedesAleatorias.SementesDoLote(lote))
        {
            var cronograma = RedesAleatorias.Gerar(semente);
            var obra = ProgramacaoDoCronograma.Programar(cronograma, PrimeiroInicio.AddDays(semente % 365), Calendario);

            ConferirPropriedades(cronograma, obra, semente);
        }
    }

    private static void ConferirPropriedades(Cronograma cronograma, CronogramaProgramado obra, int semente)
    {
        /*
         * Os dias úteis da obra, do primeiro ao último, e a posição de cada
         * um: é a régua para medir distância entre datas em dias úteis.
         * Montada pelo calendário, sem passar pela programação.
         */
        var dias = Calendario.DiasUteisAPartirDe(obra.DataDeInicio, Math.Max(obra.DuracaoEmDiasUteis, 1));
        var posicao = dias.Index().ToDictionary(dia => dia.Item, dia => dia.Index);

        // A obra ocupa a sua duração, nem um dia a mais.
        Assert.True(dias[^1] == obra.DataDeTermino, $"semente {semente}: a obra termina em {obra.DataDeTermino}, e não em {dias[^1]}");

        foreach (var atividade in obra.Atividades)
        {
            var onde = $"semente {semente}, atividade {atividade.Codigo}";

            // Toda data é dia útil e cai dentro da obra.
            foreach (var data in (DateOnly[])[atividade.InicioCedo, atividade.TerminoCedo, atividade.InicioTarde, atividade.TerminoTarde])
            {
                Assert.True(posicao.ContainsKey(data), $"{onde}: {data} não é um dia útil da obra ({obra.DataDeInicio} a {obra.DataDeTermino})");
            }

            // Início antes do término; cedo antes do tarde.
            Assert.True(atividade.InicioCedo <= atividade.TerminoCedo, $"{onde}: termina antes de começar");
            Assert.True(atividade.InicioCedo <= atividade.InicioTarde, $"{onde}: o início tarde vem antes do cedo");
            Assert.True(atividade.TerminoCedo <= atividade.TerminoTarde, $"{onde}: o término tarde vem antes do cedo");

            if (atividade.EhCritica)
            {
                Assert.Equal((atividade.InicioCedo, atividade.TerminoCedo), (atividade.InicioTarde, atividade.TerminoTarde));
            }

            if (atividade.EhMarco)
            {
                Assert.Equal(atividade.InicioCedo, atividade.TerminoCedo);
                Assert.Equal(atividade.InicioTarde, atividade.TerminoTarde);
                continue;
            }

            // Datas inclusivas: de segunda a sexta são cinco dias úteis.
            Assert.Equal(atividade.DuracaoEmDiasUteis, posicao[atividade.TerminoCedo] - posicao[atividade.InicioCedo] + 1);
            Assert.Equal(atividade.DuracaoEmDiasUteis, posicao[atividade.TerminoTarde] - posicao[atividade.InicioTarde] + 1);

            // A folga, em dias úteis, é a distância entre o início cedo e o tarde.
            Assert.Equal(atividade.FolgaTotal, posicao[atividade.InicioTarde] - posicao[atividade.InicioCedo]);
        }

        /*
         * Nenhum marco aparece antes do que o empurra. Se a predecessora
         * começa na terça e o vínculo é II+2, o marco não pode aparecer antes
         * de quinta — nem que a conta em dias úteis diga "mesmo instante",
         * que é onde o desempate entre fim de dia e começo de dia mora.
         */
        foreach (var vinculo in cronograma.Dependencias)
        {
            var marco = obra.Da(vinculo.Sucessora.Valor);

            if (!marco.EhMarco)
            {
                continue;
            }

            var predecessora = obra.Da(vinculo.Predecessora.Valor);
            var ancora = vinculo.Tipo is TipoDeDependencia.TerminoInicio or TipoDeDependencia.TerminoTermino
                ? predecessora.TerminoCedo
                : predecessora.InicioCedo;

            var limite = dias[Math.Clamp(posicao[ancora] + vinculo.DefasagemEmDiasUteis, 0, dias.Count - 1)];

            Assert.True(
                marco.InicioCedo >= limite,
                $"semente {semente}, vínculo {vinculo}: o marco aparece em {marco.InicioCedo}, antes de {limite}");
        }
    }
}
