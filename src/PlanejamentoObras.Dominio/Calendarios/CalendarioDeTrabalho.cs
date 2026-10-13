using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Calendarios;

/// <summary>
/// Quais dias a obra trabalha.
/// </summary>
/// <remarks>
/// <para>
/// O cronograma conta tempo em dias úteis, não em dias corridos: uma
/// alvenaria de 10 dias leva duas semanas de calendário, não dez dias. O
/// calendário é quem traduz "dia útil número 37" para uma data.
/// </para>
/// <para>
/// Ele é separado do cálculo do caminho crítico de propósito. O algoritmo
/// trabalha só com números inteiros — o que o torna testável com exemplos de
/// livro, sem data nenhuma — e o calendário entra depois, na hora de mostrar.
/// Trocar o calendário (sábado passa a ser útil, entra um feriado municipal)
/// não muda a rede; muda só a tradução.
/// </para>
/// </remarks>
public sealed class CalendarioDeTrabalho
{
    private readonly HashSet<DayOfWeek> _diasDaSemanaUteis;
    private readonly HashSet<DateOnly> _feriados;

    public CalendarioDeTrabalho(IEnumerable<DayOfWeek> diasDaSemanaUteis, IEnumerable<DateOnly>? feriados = null)
    {
        ArgumentNullException.ThrowIfNull(diasDaSemanaUteis);

        _diasDaSemanaUteis = [.. diasDaSemanaUteis];

        /*
         * Sem nenhum dia útil na semana, procurar "o próximo dia útil" seria
         * um laço infinito. A regra existe para impedir isso aqui, na
         * construção, e não descobrir travando a aplicação.
         */
        if (_diasDaSemanaUteis.Count == 0)
        {
            throw new ExcecaoDeDominio("O calendário precisa de ao menos um dia útil na semana.");
        }

        _feriados = feriados is null ? [] : [.. feriados];
    }

    /// <summary>Segunda a sexta — o regime mais comum em obra.</summary>
    public static CalendarioDeTrabalho DeSegundaASexta(IEnumerable<DateOnly>? feriados = null) =>
        new(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            feriados);

    /// <summary>Segunda a sábado — comum em obra com prazo apertado.</summary>
    public static CalendarioDeTrabalho DeSegundaASabado(IEnumerable<DateOnly>? feriados = null) =>
        new(
            [
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
            ],
            feriados);

    public IReadOnlySet<DayOfWeek> DiasDaSemanaUteis => _diasDaSemanaUteis;

    public IReadOnlySet<DateOnly> Feriados => _feriados;

    public bool EhDiaUtil(DateOnly data) =>
        _diasDaSemanaUteis.Contains(data.DayOfWeek) && !_feriados.Contains(data);

    /// <summary>A própria data, se for útil; senão, o próximo dia útil.</summary>
    public DateOnly PrimeiroDiaUtilAPartirDe(DateOnly data)
    {
        // Termina sempre: há ao menos um dia útil por semana, e a lista de
        // feriados é finita.
        var dia = data;

        while (!EhDiaUtil(dia))
        {
            dia = dia.AddDays(1);
        }

        return dia;
    }

    /// <summary>
    /// A data do dia útil de número <paramref name="numero"/>, contando a partir
    /// de <paramref name="inicio"/>.
    /// </summary>
    /// <remarks>
    /// O dia útil 0 é o primeiro dia útil em ou após o início. Se a obra começa
    /// num sábado, o dia 0 é a segunda-feira seguinte.
    /// </remarks>
    public DateOnly DiaUtil(DateOnly inicio, int numero)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(numero);

        return DiasUteisAPartirDe(inicio, numero + 1)[numero];
    }

    /// <summary>
    /// Os <paramref name="quantidade"/> primeiros dias úteis a partir de
    /// <paramref name="inicio"/>, em ordem.
    /// </summary>
    /// <remarks>
    /// Existe para quem precisa traduzir muitos números de dia de uma vez — a
    /// programação de um cronograma inteiro. Gerar a lista uma vez e indexar é
    /// linear; chamar <see cref="DiaUtil"/> para cada atividade seria
    /// quadrático.
    /// </remarks>
    public IReadOnlyList<DateOnly> DiasUteisAPartirDe(DateOnly inicio, int quantidade)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantidade);

        var dias = new List<DateOnly>(quantidade);

        if (quantidade == 0)
        {
            return dias;
        }

        var dia = PrimeiroDiaUtilAPartirDe(inicio);
        dias.Add(dia);

        while (dias.Count < quantidade)
        {
            dia = PrimeiroDiaUtilAPartirDe(dia.AddDays(1));
            dias.Add(dia);
        }

        return dias;
    }

    /// <summary>Quantos dias úteis há no intervalo [de, ate).</summary>
    public int DiasUteisEntre(DateOnly de, DateOnly ate)
    {
        if (ate <= de)
        {
            return 0;
        }

        var total = 0;

        for (var dia = de; dia < ate; dia = dia.AddDays(1))
        {
            if (EhDiaUtil(dia))
            {
                total++;
            }
        }

        return total;
    }
}
