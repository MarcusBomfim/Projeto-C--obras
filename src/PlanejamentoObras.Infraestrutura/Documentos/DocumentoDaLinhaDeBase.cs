using System.Text.Json.Serialization;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Infraestrutura.Documentos;

/// <summary>
/// Como uma linha de base é gravada: todos os valores, prontos. Ao contrário
/// dos outros agregados, aqui não há o que derivar — a linha de base É a
/// fotografia —, e nada é recalculado na leitura (ADR-0010).
/// </summary>
internal sealed record DocumentoDaLinhaDeBase
{
    public const short Formato = 1;

    public required string Nome { get; init; }

    public required DateOnly AprovadaEm { get; init; }

    public required DatasDoCronograma Datas { get; init; }

    public required DateOnly InicioPlanejado { get; init; }

    public required DateOnly TerminoPlanejado { get; init; }

    public required IReadOnlyList<DateOnly> Meses { get; init; }

    public required decimal Total { get; init; }

    public required decimal CustoTotal { get; init; }

    public required IReadOnlyList<LinhaPlanejadaGravada> Linhas { get; init; }

    public static DocumentoDaLinhaDeBase De(LinhaDeBase linhaDeBase) => new()
    {
        Nome = linhaDeBase.Nome,
        AprovadaEm = linhaDeBase.AprovadaEm,
        Datas = linhaDeBase.Datas,
        InicioPlanejado = linhaDeBase.InicioPlanejado,
        TerminoPlanejado = linhaDeBase.TerminoPlanejado,
        Meses = [.. linhaDeBase.Meses],
        Total = linhaDeBase.Total.Valor,
        CustoTotal = linhaDeBase.CustoTotal.Valor,
        Linhas = [.. linhaDeBase.Linhas.Select(LinhaPlanejadaGravada.De)],
    };

    public LinhaDeBase ParaLinhaDeBase(LinhaDeBaseId id) =>
        LinhaDeBase.Reconstituir(
            id,
            Nome,
            AprovadaEm,
            Datas,
            InicioPlanejado,
            TerminoPlanejado,
            Meses,
            [.. Linhas.Select(linha => linha.ParaLinhaPlanejada())],
            new Dinheiro(Total),
            new Dinheiro(CustoTotal));
}

/// <summary>Uma linha da linha de base: etapa ou item, identificada no JSON por <c>"tipo"</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(EtapaPlanejadaGravada), "etapa")]
[JsonDerivedType(typeof(ItemPlanejadoGravado), "item")]
internal abstract record LinhaPlanejadaGravada
{
    public required string Numero { get; init; }

    public required string Descricao { get; init; }

    public required decimal Total { get; init; }

    public required decimal CustoTotal { get; init; }

    public required IReadOnlyList<decimal> ValoresPlanejados { get; init; }

    public required IReadOnlyList<decimal> CustosPlanejados { get; init; }

    public static LinhaPlanejadaGravada De(LinhaPlanejada linha) => linha switch
    {
        ItemPlanejado item => new ItemPlanejadoGravado
        {
            Numero = item.Numero.Valor,
            Descricao = item.Descricao,
            Total = item.Total.Valor,
            CustoTotal = item.CustoTotal.Valor,
            ValoresPlanejados = [.. item.ValoresPlanejados.Select(valor => valor.Valor)],
            CustosPlanejados = [.. item.CustosPlanejados.Select(custo => custo.Valor)],
            Unidade = item.Unidade.Valor,
            Quantidade = item.Quantidade,
            PrecoUnitario = item.PrecoUnitario.Valor,
            CustoUnitario = item.CustoUnitario.Valor,
            Atividade = item.Atividade.Valor,
        },
        _ => new EtapaPlanejadaGravada
        {
            Numero = linha.Numero.Valor,
            Descricao = linha.Descricao,
            Total = linha.Total.Valor,
            CustoTotal = linha.CustoTotal.Valor,
            ValoresPlanejados = [.. linha.ValoresPlanejados.Select(valor => valor.Valor)],
            CustosPlanejados = [.. linha.CustosPlanejados.Select(custo => custo.Valor)],
        },
    };

    public abstract LinhaPlanejada ParaLinhaPlanejada();

    protected IEnumerable<Dinheiro> Valores() => ValoresPlanejados.Select(valor => new Dinheiro(valor));

    protected IEnumerable<Dinheiro> Custos() => CustosPlanejados.Select(custo => new Dinheiro(custo));
}

internal sealed record EtapaPlanejadaGravada : LinhaPlanejadaGravada
{
    public override LinhaPlanejada ParaLinhaPlanejada() =>
        new EtapaPlanejada(NumeroDaEap.Ler(Numero), Descricao, new Dinheiro(Total), Valores(), new Dinheiro(CustoTotal), Custos());
}

internal sealed record ItemPlanejadoGravado : LinhaPlanejadaGravada
{
    public required string Unidade { get; init; }

    public required decimal Quantidade { get; init; }

    public required decimal PrecoUnitario { get; init; }

    public required decimal CustoUnitario { get; init; }

    public required string Atividade { get; init; }

    public override LinhaPlanejada ParaLinhaPlanejada() =>
        new ItemPlanejado(
            NumeroDaEap.Ler(Numero),
            Descricao,
            new Dinheiro(Total),
            Valores(),
            new Dinheiro(CustoTotal),
            Custos(),
            new UnidadeDeMedida(Unidade),
            Quantidade,
            new Dinheiro(PrecoUnitario),
            new Dinheiro(CustoUnitario),
            new CodigoDaAtividade(Atividade));
}
