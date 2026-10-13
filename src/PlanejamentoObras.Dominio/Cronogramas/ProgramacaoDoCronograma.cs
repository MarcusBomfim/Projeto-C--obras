using PlanejamentoObras.Dominio.Calendarios;

namespace PlanejamentoObras.Dominio.Cronogramas;

/// <summary>
/// Põe o cronograma no calendário: transforma "dia útil 37" em "quinta-feira,
/// 19 de novembro".
/// </summary>
/// <remarks>
/// Separado do cálculo de propósito. A análise do caminho crítico não muda se
/// o sábado passar a ser dia útil — muda só a tradução para datas. E o
/// algoritmo fica testável com exemplos de livro, em números inteiros, sem
/// depender de que dia da semana caiu cada data.
/// </remarks>
public static class ProgramacaoDoCronograma
{
    public static CronogramaProgramado Programar(
        Cronograma cronograma,
        DateOnly inicioDaObra,
        CalendarioDeTrabalho calendario)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(calendario);

        var analise = CalculadoraDeCaminhoCritico.Analisar(cronograma);

        /*
         * Os dias úteis da obra numa lista, do primeiro ao último: o "dia útil
         * n" vira dias[n] por índice, e a programação inteira fica linear.
         * Obra de duração zero — vazia, ou só de marcos — ainda tem um dia: o
         * do início.
         */
        var dias = calendario.DiasUteisAPartirDe(inicioDaObra, Math.Max(analise.DuracaoEmDiasUteis, 1));

        var programadas = analise.Atividades
            .Select(tempos => Programar(tempos, cronograma.Atividade(tempos.Codigo.Valor).Descricao, dias))
            .ToList();

        return new CronogramaProgramado(
            cronograma.Nome,
            calendario,
            dias[0],
            dias[^1],
            analise.DuracaoEmDiasUteis,
            programadas,
            analise.DependenciasCriticas);
    }

    private static AtividadeProgramada Programar(TemposDaAtividade tempos, string descricao, IReadOnlyList<DateOnly> dias)
    {
        if (tempos.EhMarco)
        {
            var cedo = DataDoMarco(tempos.InicioCedo, tempos.MarcoDeTermino, dias);
            var tarde = DataDoMarco(tempos.InicioTarde, tempos.MarcoDeTermino, dias);

            return new AtividadeProgramada(
                tempos.Codigo, descricao, 0, cedo, cedo, tarde, tarde,
                tempos.FolgaTotal, tempos.FolgaLivre, tempos.EhCritica, EhMarco: true);
        }

        /*
         * O intervalo semiaberto [início, término) vira datas inclusivas: o
         * término em dias úteis é o primeiro dia em que a atividade já acabou,
         * então a data de término é a do dia anterior a ele.
         */
        return new AtividadeProgramada(
            tempos.Codigo,
            descricao,
            tempos.DuracaoEmDiasUteis,
            dias[tempos.InicioCedo],
            dias[tempos.TerminoCedo - 1],
            dias[tempos.InicioTarde],
            dias[tempos.TerminoTarde - 1],
            tempos.FolgaTotal,
            tempos.FolgaLivre,
            tempos.EhCritica,
            EhMarco: false);
    }

    /// <summary>
    /// Em que dia um marco aparece.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Um marco é um instante, e o mesmo instante tem dois nomes: o fim do dia
    /// útil 63 é o começo do dia útil 64. Em dias úteis inteiros os dois são o
    /// número 64; na data, um é a sexta e o outro a segunda.
    /// </para>
    /// <para>
    /// Quem desempata é o que posicionou o marco. A "conclusão da pintura" vem
    /// do término da pintura: aparece no último dia da pintura. O "início da
    /// alvenaria" vem do início da alvenaria: aparece no primeiro dia dela. É
    /// o que o MS Project faz, e o que o engenheiro espera ler. A data tarde
    /// usa o mesmo lado, para cedo e tarde nunca se inverterem.
    /// </para>
    /// <para>
    /// Na ponta final, vale o lado que existe dentro da obra: o último instante
    /// é o fim do último dia, mesmo para um marco de início. Na ponta inicial
    /// não há o que ajustar — um marco de término nunca está no instante 0,
    /// porque ali quem o posiciona é o começo da obra. Ver ADR-0003.
    /// </para>
    /// </remarks>
    private static DateOnly DataDoMarco(int instante, bool deTermino, IReadOnlyList<DateOnly> dias) =>
        deTermino
            ? dias[instante - 1]
            : dias[Math.Min(instante, dias.Count - 1)];
}
