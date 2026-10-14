using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.LinhasDeBase;

/// <summary>
/// O plano aprovado, congelado: o que se contratou, por quanto e quando —
/// a régua contra a qual a execução é medida.
/// </summary>
/// <remarks>
/// <para>
/// Orçamento e cronograma continuam mudando depois da aprovação: a base de
/// custos ganha o mês seguinte, a rede ganha uma atividade, uma quantidade é
/// revista. A linha de base não muda com eles. É uma cópia, feita uma vez, de
/// cada item — quantidade, preço e custo unitários, totais — e do planejado
/// dele em cada mês, a preço e a custo, tirado do cronograma
/// físico-financeiro (ADR-0007).
/// </para>
/// <para>
/// Não tem método que a altere. Plano novo — depois de um aditivo, por
/// exemplo — é outra linha de base.
/// </para>
/// </remarks>
public sealed class LinhaDeBase
{
    public const int TamanhoMaximoDoNome = 160;

    private readonly Dictionary<NumeroDaEap, LinhaPlanejada> _porNumero;

    private readonly List<LinhaPlanejada> _primeiroNivel;

    private LinhaDeBase(
        LinhaDeBaseId id,
        string nome,
        DateOnly aprovadaEm,
        DatasDoCronograma datas,
        DateOnly inicioPlanejado,
        DateOnly terminoPlanejado,
        IEnumerable<DateOnly> meses,
        IEnumerable<LinhaPlanejada> linhas,
        Dinheiro total,
        Dinheiro custoTotal)
    {
        Id = id;
        Nome = nome;
        AprovadaEm = aprovadaEm;
        Datas = datas;
        InicioPlanejado = inicioPlanejado;
        TerminoPlanejado = terminoPlanejado;
        Meses = Array.AsReadOnly<DateOnly>([.. meses]);
        Linhas = Array.AsReadOnly<LinhaPlanejada>([.. linhas]);
        Itens = Array.AsReadOnly<ItemPlanejado>([.. Linhas.OfType<ItemPlanejado>()]);
        Total = total;
        CustoTotal = custoTotal;
        _porNumero = Linhas.ToDictionary(linha => linha.Numero);
        _primeiroNivel = [.. Linhas.Where(linha => linha.Nivel == 1)];
    }

    public LinhaDeBaseId Id { get; }

    /// <summary>"Linha de base 1 — contrato assinado".</summary>
    public string Nome { get; }

    public DateOnly AprovadaEm { get; }

    /// <summary>Se o planejado veio das datas cedo ou das datas tarde do cronograma.</summary>
    public DatasDoCronograma Datas { get; }

    public DateOnly InicioPlanejado { get; }

    public DateOnly TerminoPlanejado { get; }

    /// <summary>Os meses planejados, do início ao término, como o primeiro dia de cada um.</summary>
    public IReadOnlyList<DateOnly> Meses { get; }

    /// <summary>Etapas e itens, na ordem da planilha.</summary>
    public IReadOnlyList<LinhaPlanejada> Linhas { get; }

    public IReadOnlyList<ItemPlanejado> Itens { get; }

    /// <summary>O valor da obra a preço de venda: o contratado.</summary>
    public Dinheiro Total { get; }

    /// <summary>O custo direto da obra: o orçamento no término do valor agregado.</summary>
    public Dinheiro CustoTotal { get; }

    /// <summary>
    /// Congela o orçamento e o cronograma como estão agora.
    /// </summary>
    /// <remarks>
    /// Todo item precisa estar vinculado a uma atividade: o planejado de cada
    /// mês vem do cronograma físico-financeiro, que exige isso.
    /// </remarks>
    public static LinhaDeBase Estabelecer(
        string nome,
        DateOnly aprovadaEm,
        OrcamentoCalculado orcamento,
        CronogramaProgramado cronograma,
        DatasDoCronograma datas = DatasDoCronograma.Cedo)
    {
        ArgumentNullException.ThrowIfNull(orcamento);
        ArgumentNullException.ThrowIfNull(cronograma);

        var nomeValido = ValidarNome(nome);

        if (orcamento.Itens.Count == 0)
        {
            throw new ExcecaoDeDominio("Não há linha de base para orçamento sem itens: não haveria o que medir.");
        }

        var fisicoFinanceiro = CalculadoraDoFisicoFinanceiro.Calcular(orcamento, cronograma, datas);

        var linhas = orcamento.Linhas.Select(linha =>
        {
            var distribuida = fisicoFinanceiro.Linha(linha.Numero.Valor);

            return linha is LinhaDeItem item
                ? (LinhaPlanejada)new ItemPlanejado(item, distribuida.Valores, distribuida.Custos)
                : new EtapaPlanejada(linha.Numero, linha.Descricao, linha.Total, distribuida.Valores, linha.CustoTotal, distribuida.Custos);
        });

        return new LinhaDeBase(
            LinhaDeBaseId.Novo(),
            nomeValido,
            aprovadaEm,
            datas,
            cronograma.DataDeInicio,
            cronograma.DataDeTermino,
            fisicoFinanceiro.Periodos.Select(periodo => periodo.Mes),
            linhas,
            orcamento.Total,
            orcamento.CustoDireto);
    }

    /// <summary>
    /// Refaz uma linha de base gravada, com os valores exatamente como foram
    /// gravados — nada é recalculado.
    /// </summary>
    /// <remarks>
    /// Para a persistência, e só para ela (ADR-0010). Como a linha de base
    /// não tem regra de escrita a reaplicar — ela nasce pronta —, o que se
    /// confere é a coerência do que veio gravado: meses em sequência, um
    /// valor por mês em cada linha, os meses somando o total da linha e as
    /// linhas de primeiro nível somando o da obra. Gravação corrompida é
    /// recusada na leitura, e não descoberta num relatório errado.
    /// </remarks>
    internal static LinhaDeBase Reconstituir(
        LinhaDeBaseId id,
        string nome,
        DateOnly aprovadaEm,
        DatasDoCronograma datas,
        DateOnly inicioPlanejado,
        DateOnly terminoPlanejado,
        IReadOnlyList<DateOnly> meses,
        IReadOnlyList<LinhaPlanejada> linhas,
        Dinheiro total,
        Dinheiro custoTotal)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(meses);
        ArgumentNullException.ThrowIfNull(linhas);

        var nomeValido = ValidarNome(nome);
        var problemas = new List<string>();

        if (!Enum.IsDefined(datas))
        {
            problemas.Add("as datas do cronograma não são nem cedo nem tarde");
        }

        if (meses.Count == 0 || meses[0].Day != 1 || meses.Skip(1).Where((mes, i) => mes != meses[i].AddMonths(1)).Any())
        {
            problemas.Add("os meses não são uma sequência de primeiros dias de mês consecutivos");
        }

        if (!linhas.OfType<ItemPlanejado>().Any())
        {
            problemas.Add("não há item");
        }

        foreach (var repetido in linhas.GroupBy(linha => linha.Numero).Where(grupo => grupo.Count() > 1))
        {
            problemas.Add($"a linha {repetido.Key} aparece mais de uma vez");
        }

        foreach (var linha in linhas)
        {
            if (linha.ValoresPlanejados.Count != meses.Count || linha.CustosPlanejados.Count != meses.Count)
            {
                problemas.Add($"a linha {linha.Numero} não tem um valor para cada um dos {meses.Count} meses");
            }
            else if (Somar(linha.ValoresPlanejados) != linha.Total || Somar(linha.CustosPlanejados) != linha.CustoTotal)
            {
                problemas.Add($"os meses da linha {linha.Numero} não somam o total dela");
            }
        }

        var primeiroNivel = linhas.Where(linha => linha.Nivel == 1).ToList();

        if (Somar(primeiroNivel.Select(linha => linha.Total)) != total
            || Somar(primeiroNivel.Select(linha => linha.CustoTotal)) != custoTotal)
        {
            problemas.Add("as etapas de primeiro nível não somam o total da obra");
        }

        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio($"A linha de base {nomeValido} gravada não é coerente: {string.Join("; ", problemas)}.");
        }

        return new LinhaDeBase(id, nomeValido, aprovadaEm, datas, inicioPlanejado, terminoPlanejado, meses, linhas, total, custoTotal);
    }

    public LinhaPlanejada Linha(string numero)
    {
        var chave = NumeroDaEap.Ler(numero);

        return _porNumero.TryGetValue(chave, out var linha)
            ? linha
            : throw new ExcecaoDeDominio($"Não existe a linha {chave} nesta linha de base.");
    }

    public ItemPlanejado Item(string numero) =>
        Linha(numero) as ItemPlanejado
        ?? throw new ExcecaoDeDominio($"A linha {numero} é uma etapa, e não um item.");

    /// <summary>
    /// O valor planejado da obra, a preço de venda, até o fim do mês. Antes do
    /// primeiro mês, zero; depois do último, o total.
    /// </summary>
    public Dinheiro ValorPlanejadoAcumulado(DateOnly mes) =>
        _primeiroNivel.Aggregate(Dinheiro.Zero, (soma, linha) => soma + Acumulado(linha.ValoresPlanejados, mes));

    /// <summary>O custo planejado da obra até o fim do mês. Antes do primeiro mês, zero; depois do último, o custo total.</summary>
    public Dinheiro CustoPlanejadoAcumulado(DateOnly mes) =>
        _primeiroNivel.Aggregate(Dinheiro.Zero, (soma, linha) => soma + Acumulado(linha.CustosPlanejados, mes));

    /// <summary>O valor planejado de uma linha, a preço de venda, até o fim do mês.</summary>
    public Dinheiro ValorPlanejadoAcumulado(string numero, DateOnly mes) => Acumulado(Linha(numero).ValoresPlanejados, mes);

    /// <summary>O custo planejado de uma linha até o fim do mês.</summary>
    public Dinheiro CustoPlanejadoAcumulado(string numero, DateOnly mes) => Acumulado(Linha(numero).CustosPlanejados, mes);

    internal Dinheiro CustoPlanejadoAcumulado(LinhaPlanejada linha, DateOnly mes) => Acumulado(linha.CustosPlanejados, mes);

    /// <summary>A soma dos meses da linha de base que terminam até o fim do mês informado.</summary>
    private Dinheiro Acumulado(IReadOnlyList<Dinheiro> porMes, DateOnly mes)
    {
        var competencia = new DateOnly(mes.Year, mes.Month, 1);

        return porMes.Take(Meses.Count(planejado => planejado <= competencia)).Aggregate(Dinheiro.Zero, (soma, valor) => soma + valor);
    }

    private static Dinheiro Somar(IEnumerable<Dinheiro> valores) =>
        valores.Aggregate(Dinheiro.Zero, (soma, valor) => soma + valor);

    private static string ValidarNome(string nome)
    {
        var limpo = (nome ?? string.Empty).Trim();

        if (limpo.Length == 0)
        {
            throw new ExcecaoDeDominio("A linha de base precisa de um nome.");
        }

        if (limpo.Length > TamanhoMaximoDoNome)
        {
            throw new ExcecaoDeDominio($"O nome da linha de base tem no máximo {TamanhoMaximoDoNome} caracteres.");
        }

        return limpo;
    }
}
