using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Medicoes;

/// <summary>
/// A execução de uma obra medida contra a sua linha de base: as medições, mês
/// a mês.
/// </summary>
/// <remarks>
/// <para>
/// Raiz do agregado. As regras são do histórico inteiro, não de uma medição
/// sozinha: as medições entram em ordem, uma por mês; nenhum item passa da
/// quantidade contratada nem fica com quantidade medida negativa; e o que foi
/// registrado não muda — só a última medição pode ser desfeita (ADR-0008).
/// </para>
/// <para>
/// Guarda só o que foi lançado: quantidades e custos de cada mês. Acumulados,
/// valores medidos e indicadores são derivados — ver
/// <see cref="EmissaoDoBoletim"/>.
/// </para>
/// </remarks>
public sealed class Acompanhamento
{
    private readonly List<Medicao> _medicoes = [];

    public Acompanhamento(LinhaDeBase linhaDeBase)
        : this(AcompanhamentoId.Novo(), linhaDeBase)
    {
    }

    /// <summary>
    /// Um acompanhamento sem medições com uma identidade já existente — o
    /// começo da reconstituição de um acompanhamento gravado. A persistência
    /// registra as medições de novo, uma a uma, e cada uma passa pelas regras
    /// de sempre (ADR-0010).
    /// </summary>
    internal Acompanhamento(AcompanhamentoId id, LinhaDeBase linhaDeBase)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(linhaDeBase);

        Id = id;
        LinhaDeBase = linhaDeBase;
    }

    public AcompanhamentoId Id { get; }

    public LinhaDeBase LinhaDeBase { get; }

    /// <summary>As medições registradas, da primeira à última.</summary>
    public IReadOnlyList<Medicao> Medicoes => _medicoes.AsReadOnly();

    /// <summary>
    /// Registra a medição de um mês.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Qualquer dia vale pelo mês inteiro: 31/10/2026 é a medição de outubro.
    /// Item que não aparece não executou nem custou nada no mês; medição sem
    /// lançamento nenhum é a de um mês parado — e conta.
    /// </para>
    /// <para>
    /// Os problemas vêm todos numa mensagem. Se houver algum, nada é
    /// registrado.
    /// </para>
    /// </remarks>
    public Medicao RegistrarMedicao(DateOnly mes, params IEnumerable<LancamentoDeMedicao> lancamentos)
    {
        ArgumentNullException.ThrowIfNull(lancamentos);

        var competencia = new DateOnly(mes.Year, mes.Month, 1);

        if (_medicoes.Count > 0 && competencia <= _medicoes[^1].Mes)
        {
            throw new ExcecaoDeDominio(
                $"A medição de {FormatoBrasileiro.Mes(competencia)} não pode entrar depois da de {FormatoBrasileiro.Mes(_medicoes[^1].Mes)}: "
                + "as medições entram em ordem, uma por mês. Para corrigir a última, desfaça-a e registre de novo.");
        }

        var acumuladas = QuantidadesAcumuladas(_medicoes.Count);
        var vistos = new HashSet<NumeroDaEap>();
        var itens = new List<ItemDaMedicao>();
        var problemas = new List<string>();

        foreach (var lancamento in lancamentos)
        {
            ArgumentNullException.ThrowIfNull(lancamento);

            if (Conferir(lancamento, acumuladas, vistos, problemas) is { } item)
            {
                itens.Add(item);
            }
        }

        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio(
                $"A medição de {FormatoBrasileiro.Mes(competencia)} não foi registrada: {string.Join("; ", problemas)}.");
        }

        var medicao = new Medicao(_medicoes.Count + 1, competencia, itens);
        _medicoes.Add(medicao);

        return medicao;
    }

    /// <summary>
    /// Desfaz a última medição — para corrigir um lançamento antes de a medição
    /// seguinte existir.
    /// </summary>
    public void DesfazerUltimaMedicao()
    {
        if (_medicoes.Count == 0)
        {
            throw new ExcecaoDeDominio("Não há medição para desfazer.");
        }

        _medicoes.RemoveAt(_medicoes.Count - 1);
    }

    /// <summary>A medição pelo número: 1 é a primeira.</summary>
    public Medicao Medicao(int numero) =>
        numero >= 1 && numero <= _medicoes.Count
            ? _medicoes[numero - 1]
            : throw new ExcecaoDeDominio($"Não existe a medição {numero}: há {_medicoes.Count} registrada(s).");

    /// <summary>A medição de um mês; qualquer dia do mês serve.</summary>
    public Medicao MedicaoDoMes(DateOnly mes)
    {
        var competencia = new DateOnly(mes.Year, mes.Month, 1);

        return _medicoes.Find(medicao => medicao.Mes == competencia)
            ?? throw new ExcecaoDeDominio($"Não há medição registrada em {FormatoBrasileiro.Mes(competencia)}.");
    }

    /// <summary>A quantidade acumulada de cada item lançado, da primeira medição até a informada.</summary>
    internal Dictionary<NumeroDaEap, decimal> QuantidadesAcumuladas(int ateAMedicao)
    {
        var acumuladas = new Dictionary<NumeroDaEap, decimal>();

        foreach (var item in _medicoes.Take(ateAMedicao).SelectMany(medicao => medicao.Itens))
        {
            acumuladas[item.Item] = acumuladas.GetValueOrDefault(item.Item) + item.Quantidade;
        }

        return acumuladas;
    }

    /// <summary>O custo real acumulado de cada item lançado, da primeira medição até a informada.</summary>
    internal Dictionary<NumeroDaEap, Dinheiro> CustosAcumulados(int ateAMedicao)
    {
        var acumulados = new Dictionary<NumeroDaEap, Dinheiro>();

        foreach (var item in _medicoes.Take(ateAMedicao).SelectMany(medicao => medicao.Itens))
        {
            acumulados[item.Item] = acumulados.GetValueOrDefault(item.Item) + item.CustoReal;
        }

        return acumulados;
    }

    /// <summary>Confere um lançamento; devolve o item da medição, ou nulo e o motivo em <paramref name="problemas"/>.</summary>
    private ItemDaMedicao? Conferir(
        LancamentoDeMedicao lancamento,
        Dictionary<NumeroDaEap, decimal> acumuladas,
        HashSet<NumeroDaEap> vistos,
        List<string> problemas)
    {
        if (!NumeroDaEap.TentarLer(lancamento.Item, out var numero))
        {
            problemas.Add($"\"{lancamento.Item}\" não é um número de item");

            return null;
        }

        var linha = LinhaDeBase.Linhas.FirstOrDefault(linha => linha.Numero == numero);

        if (linha is not ItemPlanejado contratado)
        {
            problemas.Add(linha is null
                ? $"o item {numero} não existe na linha de base"
                : $"{numero} é uma etapa, e a medição é item por item");

            return null;
        }

        if (!vistos.Add(numero))
        {
            problemas.Add($"o item {numero} aparece mais de uma vez");

            return null;
        }

        var antes = problemas.Count;
        var unidade = contratado.Unidade;

        if (lancamento.Quantidade != decimal.Round(lancamento.Quantidade, 2))
        {
            problemas.Add($"a quantidade do item {numero} tem mais de duas casas decimais");
        }

        var custo = new Dinheiro(lancamento.CustoReal);

        if (custo < Dinheiro.Zero || !custo.EstaEmCentavos)
        {
            problemas.Add($"o custo real do item {numero} precisa ser positivo e ir até o centavo");
        }

        var acumulada = acumuladas.GetValueOrDefault(numero) + lancamento.Quantidade;

        if (acumulada < 0)
        {
            problemas.Add(
                $"a glosa do item {numero} deixaria negativa a quantidade medida: {FormatoBrasileiro.Numero(acumulada)} {unidade}");
        }

        if (acumulada > contratado.Quantidade)
        {
            problemas.Add(
                $"o item {numero} chegaria a {FormatoBrasileiro.Numero(acumulada)} {unidade} medidos, "
                + $"mais que os {FormatoBrasileiro.Numero(contratado.Quantidade)} {unidade} contratados — medir além do contrato exige aditivo");
        }

        return problemas.Count == antes ? new ItemDaMedicao(numero, lancamento.Quantidade, custo) : null;
    }
}
