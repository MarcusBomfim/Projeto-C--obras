using PlanejamentoObras.Dominio.Calendarios;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Dominio.Testes.Cronogramas;
using PlanejamentoObras.Dominio.Testes.Orcamentos;

namespace PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;

/*
 * Uma rede sorteada e um orçamento sorteado, com cada item vinculado a uma
 * atividade qualquer — marcos e atividades com folga inclusive —, nas datas
 * cedo e nas tarde. Em todos, o físico-financeiro tem que fechar com o
 * orçamento ao centavo e pôr cada real no mês em que o serviço acontece.
 */
public sealed class InvariantesDoFisicoFinanceiroTests
{
    private static readonly CalendarioDeTrabalho Calendario = CalendarioDeTrabalho.DeSegundaASexta(
    [
        new(2026, 10, 12), new(2026, 11, 2), new(2026, 11, 20), new(2026, 12, 25),
        new(2027, 1, 1), new(2027, 3, 26), new(2027, 4, 21), new(2027, 9, 7),
    ]);

    [Theory]
    [MemberData(nameof(RedesAleatorias.Lotes), MemberType = typeof(RedesAleatorias))]
    public void Todo_fisico_financeiro_fecha_com_o_orcamento_e_respeita_o_cronograma(int lote)
    {
        foreach (var semente in RedesAleatorias.SementesDoLote(lote))
        {
            var cronograma = RedesAleatorias.Gerar(semente);
            var obra = ProgramacaoDoCronograma.Programar(cronograma, new DateOnly(2026, 10, 1).AddDays(semente % 365), Calendario);
            var planilha = OrcamentoVinculado(semente, cronograma);

            foreach (var datas in Enum.GetValues<DatasDoCronograma>())
            {
                Conferir(planilha, obra, CalculadoraDoFisicoFinanceiro.Calcular(planilha, obra, datas), $"semente {semente}, {datas}");
            }
        }
    }

    private static OrcamentoCalculado OrcamentoVinculado(int semente, Cronograma cronograma)
    {
        var (baseDeCustos, orcamento) = OrcamentosAleatorios.Gerar(semente);
        var codigos = cronograma.Atividades.Select(atividade => atividade.Codigo.Valor).ToList();
        var sorteio = new Random(semente);

        foreach (var item in CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos).Itens)
        {
#pragma warning disable CA5394 // Aleatório não criptográfico de propósito: o que se quer é reprodutibilidade.
            orcamento.VincularAtividade(item.Numero.Valor, codigos[sorteio.Next(codigos.Count)]);
#pragma warning restore CA5394
        }

        return CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos);
    }

    private static void Conferir(OrcamentoCalculado planilha, CronogramaProgramado obra, CronogramaFisicoFinanceiro fisicoFinanceiro, string onde)
    {
        var periodos = fisicoFinanceiro.Periodos;

        // Os meses da obra, todos, em sequência.
        Assert.Equal(new DateOnly(obra.DataDeInicio.Year, obra.DataDeInicio.Month, 1), periodos[0].Mes);
        Assert.Equal(new DateOnly(obra.DataDeTermino.Year, obra.DataDeTermino.Month, 1), periodos[^1].Mes);
        Assert.All(periodos.Skip(1).Zip(periodos), par => Assert.Equal(par.Second.Mes.AddMonths(1), par.First.Mes));

        // Fecha com o orçamento, e o acumulado só sobe.
        Assert.True(planilha.Total == fisicoFinanceiro.Total, $"{onde}: total diferente do orçamento");
        Assert.True(periodos.Sum(periodo => periodo.Valor.Valor) == planilha.Total.Valor, $"{onde}: os meses não somam o total");
        Assert.True(periodos[^1].Acumulado == planilha.Total, $"{onde}: o acumulado não chega ao total");
        Assert.All(periodos.Skip(1).Zip(periodos), par => Assert.True(par.First.Acumulado >= par.Second.Acumulado, $"{onde}: acumulado que desce"));

        if (planilha.Total > Dinheiro.Zero)
        {
            Assert.Equal(1m, periodos[^1].PercentualAcumulado);
        }

        foreach (var linha in fisicoFinanceiro.Linhas)
        {
            var aqui = $"{onde}, linha {linha.Numero}";

            Assert.Equal(periodos.Count, linha.Valores.Count);
            Assert.True(linha.Valores.Sum(valor => valor.Valor) == linha.Total.Valor, $"{aqui}: os meses não somam o total da linha");
            Assert.All(linha.Valores, valor => Assert.True(valor >= Dinheiro.Zero && valor.EstaEmCentavos, $"{aqui}: valor negativo ou fora do centavo"));

            // Etapa: mês a mês, a soma do que está logo abaixo dela.
            var filhos = fisicoFinanceiro.Linhas
                .Where(outra => outra.Nivel == linha.Nivel + 1 && outra.Numero.Valor.StartsWith(linha.Numero.Valor + ".", StringComparison.Ordinal))
                .ToList();

            if (filhos.Count > 0)
            {
                for (var mes = 0; mes < periodos.Count; mes++)
                {
                    Assert.True(filhos.Sum(filho => filho.Valores[mes].Valor) == linha.Valores[mes].Valor, $"{aqui}: o mês {mes + 1} não soma os filhos");
                }
            }
        }

        foreach (var item in planilha.Itens)
        {
            ConferirItem(item, obra, fisicoFinanceiro, onde);
        }
    }

    /// <summary>
    /// Cada mês recebe a parte do item proporcional aos dias de trabalho da
    /// atividade nele — a menos de um centavo — e mês sem dia de trabalho não
    /// recebe nada.
    /// </summary>
    /// <remarks>
    /// Os dias são contados aqui de novo, varrendo o calendário dia a dia
    /// entre o início e o término programados.
    /// </remarks>
    private static void ConferirItem(LinhaDeItem item, CronogramaProgramado obra, CronogramaFisicoFinanceiro fisicoFinanceiro, string onde)
    {
        var atividade = obra.Da(item.Atividade!.Valor);
        var (inicio, termino) = fisicoFinanceiro.Datas == DatasDoCronograma.Cedo
            ? (atividade.InicioCedo, atividade.TerminoCedo)
            : (atividade.InicioTarde, atividade.TerminoTarde);

        var diasPorMes = new Dictionary<DateOnly, int>();

        for (var dia = inicio; dia <= termino; dia = dia.AddDays(1))
        {
            if (atividade.EhMarco || Calendario.EhDiaUtil(dia))
            {
                var mes = new DateOnly(dia.Year, dia.Month, 1);
                diasPorMes[mes] = diasPorMes.GetValueOrDefault(mes) + 1;
            }
        }

        var totalDeDias = diasPorMes.Values.Sum();
        var valores = fisicoFinanceiro.Linha(item.Numero.Valor).Valores;

        for (var mes = 0; mes < fisicoFinanceiro.Periodos.Count; mes++)
        {
            var dias = diasPorMes.GetValueOrDefault(fisicoFinanceiro.Periodos[mes].Mes);
            var exato = item.Total.Valor * dias / totalDeDias;

            Assert.True(
                Math.Abs(valores[mes].Valor - exato) < 0.01m,
                $"{onde}, item {item.Numero}: no mês {mes + 1}, {valores[mes]} para {dias} de {totalDeDias} dias de {item.Total}");
        }
    }
}
