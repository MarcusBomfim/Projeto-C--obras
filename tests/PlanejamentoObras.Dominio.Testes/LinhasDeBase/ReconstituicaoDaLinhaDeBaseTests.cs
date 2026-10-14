using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;

namespace PlanejamentoObras.Dominio.Testes.LinhasDeBase;

/*
 * A linha de base nasce pronta e não tem regra de escrita a reaplicar: o que
 * a reconstituição confere é a coerência do que veio gravado.
 */
public sealed class ReconstituicaoDaLinhaDeBaseTests
{
    private static readonly LinhaDeBase Original = CasaTerrea.LinhaDeBaseAprovada();

    private static Dinheiro R(decimal valor) => Dinheiro.Reais(valor);

    /// <summary>Uma cópia da linha, com os meses trocados se pedido.</summary>
    private static LinhaPlanejada Copiar(LinhaPlanejada linha, IEnumerable<Dinheiro>? valores = null) =>
        linha is ItemPlanejado item
            ? new ItemPlanejado(
                item.Numero, item.Descricao, item.Total, valores ?? item.ValoresPlanejados, item.CustoTotal, item.CustosPlanejados,
                item.Unidade, item.Quantidade, item.PrecoUnitario, item.CustoUnitario, item.Atividade)
            : new EtapaPlanejada(linha.Numero, linha.Descricao, linha.Total, valores ?? linha.ValoresPlanejados, linha.CustoTotal, linha.CustosPlanejados);

    private static LinhaDeBase Reconstituir(IReadOnlyList<DateOnly>? meses = null, IReadOnlyList<LinhaPlanejada>? linhas = null) =>
        LinhaDeBase.Reconstituir(
            Original.Id,
            Original.Nome,
            Original.AprovadaEm,
            Original.Datas,
            Original.InicioPlanejado,
            Original.TerminoPlanejado,
            meses ?? Original.Meses,
            linhas ?? [.. Original.Linhas.Select(linha => Copiar(linha))],
            Original.Total,
            Original.CustoTotal);

    private static string Erro(Action acao) => Assert.Throws<ExcecaoDeDominio>(acao).Message;

    [Fact]
    public void Devolve_a_mesma_linha_de_base()
    {
        var copia = Reconstituir();

        Assert.Equal(Original.Id, copia.Id);
        Assert.Equal((Original.Nome, Original.AprovadaEm, Original.Datas), (copia.Nome, copia.AprovadaEm, copia.Datas));
        Assert.Equal(Original.Meses, copia.Meses);
        Assert.Equal((Original.Total, Original.CustoTotal), (copia.Total, copia.CustoTotal));
        Assert.Equal(R(8_000m), copia.Item("2.1").CustoUnitario);
        Assert.Equal([R(8_333.33m), R(1_666.67m)], copia.Item("2.1").ValoresPlanejados);
        Assert.Equal(R(15_466.67m), copia.CustoPlanejadoAcumulado(new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void Recusa_linha_cujos_meses_nao_somam_o_total()
    {
        var linhas = Original.Linhas
            .Select(linha => linha.Numero.Valor == "2.1" ? Copiar(linha, [R(8_333.33m), R(1_666.66m)]) : Copiar(linha))
            .ToList();

        Assert.Contains("os meses da linha 2.1 não somam o total dela", Erro(() => Reconstituir(linhas: linhas)), StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_linha_sem_um_valor_por_mes()
    {
        var linhas = Original.Linhas
            .Select(linha => linha.Numero.Valor == "1.1" ? Copiar(linha, [R(10_000m)]) : Copiar(linha))
            .ToList();

        Assert.Contains("a linha 1.1 não tem um valor para cada um dos 2 meses", Erro(() => Reconstituir(linhas: linhas)), StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_meses_fora_de_sequencia()
    {
        DateOnly[] pulandoUmMes = [new(2026, 10, 1), new(2026, 12, 1)];

        Assert.Contains("os meses não são uma sequência", Erro(() => Reconstituir(meses: pulandoUmMes)), StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_linha_repetida_e_linha_de_base_sem_item()
    {
        var repetida = Original.Linhas.Select(linha => Copiar(linha)).Append(Copiar(Original.Linha("2.3"))).ToList();
        var soEtapas = Original.Linhas.OfType<EtapaPlanejada>().Select(linha => Copiar(linha)).ToList();

        Assert.Contains("a linha 2.3 aparece mais de uma vez", Erro(() => Reconstituir(linhas: repetida)), StringComparison.Ordinal);
        Assert.Contains("não há item", Erro(() => Reconstituir(linhas: soEtapas)), StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_obra_cujas_etapas_nao_somam_o_total()
    {
        var semAEtapa2 = Original.Linhas
            .Where(linha => linha.Numero != NumeroDaEap.Ler("2") && !linha.Numero.Valor.StartsWith("2.", StringComparison.Ordinal))
            .Select(linha => Copiar(linha))
            .ToList();

        Assert.Contains("as etapas de primeiro nível não somam o total da obra", Erro(() => Reconstituir(linhas: semAEtapa2)), StringComparison.Ordinal);
    }
}
