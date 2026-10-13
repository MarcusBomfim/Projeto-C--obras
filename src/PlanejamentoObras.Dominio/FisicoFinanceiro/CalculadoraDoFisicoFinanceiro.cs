using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.FisicoFinanceiro;

/// <summary>
/// Junta orçamento e cronograma: distribui o valor de cada item pelos dias de
/// trabalho da atividade a que ele está vinculado, e soma por mês.
/// </summary>
/// <remarks>
/// <para>
/// O valor de um item se espalha por igual nos dias úteis da atividade — a
/// alvenaria de 10 dias executa um décimo por dia. Com os dias de cada mês
/// contados, a parte do mês é proporcional a eles, rateada ao centavo (ver
/// <see cref="Rateio"/>). Um marco concentra o valor no dia dele.
/// </para>
/// <para>
/// Não há cronograma físico-financeiro de orçamento pela metade: todo item
/// precisa estar vinculado a uma atividade que exista no cronograma, senão a
/// soma dos meses não daria o total da obra. Os que faltam vêm todos numa
/// mensagem só. Ver ADR-0006.
/// </para>
/// </remarks>
public static class CalculadoraDoFisicoFinanceiro
{
    public static CronogramaFisicoFinanceiro Calcular(
        OrcamentoCalculado orcamento,
        CronogramaProgramado cronograma,
        DatasDoCronograma datas = DatasDoCronograma.Cedo)
    {
        ArgumentNullException.ThrowIfNull(orcamento);
        ArgumentNullException.ThrowIfNull(cronograma);

        if (!Enum.IsDefined(datas))
        {
            throw new ArgumentOutOfRangeException(nameof(datas), datas, "Datas do cronograma desconhecidas.");
        }

        ConferirVinculos(orcamento, cronograma);

        var meses = Meses(cronograma.DataDeInicio, cronograma.DataDeTermino);
        var posicaoDoMes = meses.Select((mes, posicao) => (mes, posicao)).ToDictionary(par => par.mes, par => par.posicao);

        // Itens primeiro; cada item soma o que é dele em cada etapa acima dele.
        var valores = orcamento.Linhas.ToDictionary(linha => linha.Numero, _ => new Dinheiro[meses.Count]);

        foreach (var item in orcamento.Itens)
        {
            var distribuidos = Distribuir(item, cronograma.Da(item.Atividade!.Valor), datas, cronograma, posicaoDoMes);

            foreach (var numero in Ancestrais(item.Numero).Append(item.Numero))
            {
                var linha = valores[numero];

                for (var mes = 0; mes < meses.Count; mes++)
                {
                    linha[mes] += distribuidos[mes];
                }
            }
        }

        var linhas = orcamento.Linhas
            .Select(linha => new LinhaDoFisicoFinanceiro(linha.Numero, linha.Descricao, linha.Total, valores[linha.Numero]))
            .ToList();

        return new CronogramaFisicoFinanceiro(datas, Periodos(meses, linhas, orcamento.Total), linhas, orcamento.Total);
    }

    private static void ConferirVinculos(OrcamentoCalculado orcamento, CronogramaProgramado cronograma)
    {
        var semAtividade = orcamento.Itens
            .Where(item => item.Atividade is null)
            .Select(item => item.Numero.Valor)
            .ToList();

        var programadas = cronograma.Atividades.Select(atividade => atividade.Codigo).ToHashSet();

        var atividadeInexistente = orcamento.Itens
            .Where(item => item.Atividade is not null && !programadas.Contains(item.Atividade))
            .Select(item => $"{item.Numero} ({item.Atividade})")
            .ToList();

        var problemas = new List<string>();

        if (semAtividade.Count > 0)
        {
            problemas.Add($"itens sem atividade vinculada: {string.Join(", ", semAtividade)}");
        }

        if (atividadeInexistente.Count > 0)
        {
            problemas.Add($"itens vinculados a atividade que não está no cronograma: {string.Join(", ", atividadeInexistente)}");
        }

        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio(
                $"Para distribuir o orçamento no tempo, todo item precisa de uma atividade do cronograma. Há {string.Join("; e ", problemas)}.");
        }
    }

    private static Dinheiro[] Distribuir(
        LinhaDeItem item,
        AtividadeProgramada atividade,
        DatasDoCronograma datas,
        CronogramaProgramado cronograma,
        Dictionary<DateOnly, int> posicaoDoMes)
    {
        var inicio = datas == DatasDoCronograma.Cedo ? atividade.InicioCedo : atividade.InicioTarde;

        IReadOnlyList<DateOnly> dias = atividade.EhMarco
            ? [inicio]
            : cronograma.Calendario.DiasUteisAPartirDe(inicio, atividade.DuracaoEmDiasUteis);

        var diasPorMes = new int[posicaoDoMes.Count];

        foreach (var dia in dias)
        {
            diasPorMes[posicaoDoMes[PrimeiroDoMes(dia)]]++;
        }

        return Rateio.Distribuir(item.Total, diasPorMes);
    }

    private static List<PeriodoDoFisicoFinanceiro> Periodos(
        List<DateOnly> meses,
        List<LinhaDoFisicoFinanceiro> linhas,
        Dinheiro total)
    {
        var itens = linhas.Where(linha => linha.Nivel == 1).ToList();
        var periodos = new List<PeriodoDoFisicoFinanceiro>(meses.Count);
        var acumulado = Dinheiro.Zero;

        for (var mes = 0; mes < meses.Count; mes++)
        {
            var valor = itens.Aggregate(Dinheiro.Zero, (soma, linha) => soma + linha.Valores[mes]);
            acumulado += valor;

            periodos.Add(new PeriodoDoFisicoFinanceiro(
                mes + 1,
                meses[mes],
                valor,
                Fracao(valor, total),
                acumulado,
                Fracao(acumulado, total)));
        }

        return periodos;
    }

    /// <summary>Do mês do início ao mês do término da obra, inclusive.</summary>
    private static List<DateOnly> Meses(DateOnly inicio, DateOnly termino)
    {
        var meses = new List<DateOnly>();

        for (var mes = PrimeiroDoMes(inicio); mes <= termino; mes = mes.AddMonths(1))
        {
            meses.Add(mes);
        }

        return meses;
    }

    /// <summary>As etapas acima de uma linha: de "2.2.1", as etapas 2 e 2.2.</summary>
    private static IEnumerable<NumeroDaEap> Ancestrais(NumeroDaEap numero)
    {
        var posicoes = numero.Posicoes();

        for (var nivel = 1; nivel < posicoes.Count; nivel++)
        {
            yield return NumeroDaEap.Ler(string.Join('.', posicoes.Take(nivel)));
        }
    }

    private static DateOnly PrimeiroDoMes(DateOnly dia) => new(dia.Year, dia.Month, 1);

    private static decimal Fracao(Dinheiro parte, Dinheiro todo) => todo == Dinheiro.Zero ? 0m : parte / todo;
}
