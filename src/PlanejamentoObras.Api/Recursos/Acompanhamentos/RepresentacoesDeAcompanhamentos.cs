using System.Text.Json.Serialization;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.ValorAgregado;

namespace PlanejamentoObras.Api.Recursos.Acompanhamentos;

public sealed record NovoAcompanhamento(Guid LinhaDeBase);

/// <summary>
/// A medição de um mês: para cada item medido, a quantidade executada no mês
/// — negativa é glosa — e o custo real dela.
/// </summary>
public sealed record NovaMedicao(DateOnly Mes, IReadOnlyList<NovoLancamento> Lancamentos);

public sealed record NovoLancamento(string Item, decimal Quantidade, decimal CustoReal = 0m);

public sealed record AcompanhamentoResposta(Guid Id, Guid LinhaDeBase, IReadOnlyList<MedicaoResposta> Medicoes)
{
    public static AcompanhamentoResposta De(Acompanhamento acompanhamento) =>
        new(
            acompanhamento.Id.Valor,
            acompanhamento.LinhaDeBase.Id.Valor,
            [.. acompanhamento.Medicoes.Select(medicao => new MedicaoResposta(
                medicao.Numero,
                medicao.Mes,
                [.. medicao.Itens.Select(item => new LancamentoResposta(item.Item.Valor, item.Quantidade, item.CustoReal.Valor))]))]);
}

public sealed record MedicaoResposta(int Numero, DateOnly Mes, IReadOnlyList<LancamentoResposta> Itens);

public sealed record LancamentoResposta(string Item, decimal Quantidade, decimal CustoReal);

/// <summary>Um acompanhamento medido contra a linha de base, e a versão dele.</summary>
public sealed record AcompanhamentoDaLinhaDeBase(Guid Id, int Versao);

/// <summary>O boletim de uma medição, a preço de contrato: o valor do mês é o que se paga.</summary>
public sealed record BoletimResposta(
    int Numero,
    DateOnly Mes,
    decimal ValorContratado,
    decimal ValorAnterior,
    decimal ValorNoMes,
    decimal ValorAcumulado,
    decimal Saldo,
    decimal PercentualExecutado,
    IReadOnlyList<LinhaDoBoletimResposta> Linhas)
{
    public static BoletimResposta De(BoletimDeMedicao boletim) =>
        new(
            boletim.Numero,
            boletim.Mes,
            boletim.ValorContratado.Valor,
            boletim.ValorAnterior.Valor,
            boletim.ValorNoMes.Valor,
            boletim.ValorAcumulado.Valor,
            boletim.Saldo.Valor,
            boletim.PercentualExecutado,
            [.. boletim.Linhas.Select(LinhaDoBoletimResposta.De)]);
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(EtapaDoBoletimResposta), "etapa")]
[JsonDerivedType(typeof(ItemDoBoletimResposta), "item")]
public abstract record LinhaDoBoletimResposta(
    string Numero,
    string Descricao,
    decimal ValorContratado,
    decimal ValorAnterior,
    decimal ValorNoMes,
    decimal ValorAcumulado,
    decimal Saldo,
    decimal PercentualExecutado)
{
    public static LinhaDoBoletimResposta De(LinhaDoBoletim linha) => linha switch
    {
        ItemDoBoletim item => new ItemDoBoletimResposta(
            item.Numero.Valor,
            item.Descricao,
            item.ValorContratado.Valor,
            item.ValorAnterior.Valor,
            item.ValorNoMes.Valor,
            item.ValorAcumulado.Valor,
            item.Saldo.Valor,
            item.PercentualExecutado,
            item.Unidade.Valor,
            item.PrecoUnitario.Valor,
            item.QuantidadeContratada,
            item.QuantidadeAnterior,
            item.QuantidadeNoMes,
            item.QuantidadeAcumulada),
        _ => new EtapaDoBoletimResposta(
            linha.Numero.Valor,
            linha.Descricao,
            linha.ValorContratado.Valor,
            linha.ValorAnterior.Valor,
            linha.ValorNoMes.Valor,
            linha.ValorAcumulado.Valor,
            linha.Saldo.Valor,
            linha.PercentualExecutado),
    };
}

public sealed record EtapaDoBoletimResposta(
    string Numero,
    string Descricao,
    decimal ValorContratado,
    decimal ValorAnterior,
    decimal ValorNoMes,
    decimal ValorAcumulado,
    decimal Saldo,
    decimal PercentualExecutado)
    : LinhaDoBoletimResposta(Numero, Descricao, ValorContratado, ValorAnterior, ValorNoMes, ValorAcumulado, Saldo, PercentualExecutado);

public sealed record ItemDoBoletimResposta(
    string Numero,
    string Descricao,
    decimal ValorContratado,
    decimal ValorAnterior,
    decimal ValorNoMes,
    decimal ValorAcumulado,
    decimal Saldo,
    decimal PercentualExecutado,
    string Unidade,
    decimal PrecoUnitario,
    decimal QuantidadeContratada,
    decimal QuantidadeAnterior,
    decimal QuantidadeNoMes,
    decimal QuantidadeAcumulada)
    : LinhaDoBoletimResposta(Numero, Descricao, ValorContratado, ValorAnterior, ValorNoMes, ValorAcumulado, Saldo, PercentualExecutado);

/// <summary>
/// O valor agregado num mês: da obra, de cada etapa e item, o prazo agregado
/// e a curva S. Valores a custo (ADR-0009); índices com todas as casas.
/// </summary>
public sealed record ValorAgregadoResposta(
    int Medicao,
    DateOnly Mes,
    IndicadoresResposta Obra,
    PrazoAgregadoResposta Prazo,
    IReadOnlyList<LinhaDoValorAgregadoResposta> Linhas,
    IReadOnlyList<PontoDaCurvaResposta> Curva)
{
    public static ValorAgregadoResposta De(AnaliseDeValorAgregado analise) =>
        new(
            analise.Medicao,
            analise.Mes,
            IndicadoresResposta.De(analise.Obra),
            PrazoAgregadoResposta.De(analise.Prazo),
            [.. analise.Linhas.Select(linha => new LinhaDoValorAgregadoResposta(linha.Numero.Valor, linha.Descricao, IndicadoresResposta.De(linha.Indicadores)))],
            [.. analise.Curva.Select(ponto => new PontoDaCurvaResposta(ponto.Mes, ponto.ValorPlanejado.Valor, ponto.ValorAgregado?.Valor, ponto.CustoReal?.Valor))]);
}

/// <summary>Os indicadores de uma linha. Sem base para o cálculo — custo real zero, por exemplo —, o índice vem nulo.</summary>
public sealed record IndicadoresResposta(
    decimal OrcamentoNoTermino,
    decimal ValorPlanejado,
    decimal ValorAgregado,
    decimal CustoReal,
    decimal VariacaoDePrazo,
    decimal VariacaoDeCusto,
    decimal? IndiceDeDesempenhoDePrazo,
    decimal? IndiceDeDesempenhoDeCusto,
    decimal PercentualPlanejado,
    decimal PercentualExecutado,
    decimal? EstimativaNoTermino,
    decimal EstimativaNoTerminoComRestanteNoOrcamento,
    decimal? EstimativaNoTerminoComCustoEPrazo,
    decimal? EstimativaParaTerminar,
    decimal? VariacaoNoTermino,
    decimal? IndiceDeDesempenhoParaTerminar)
{
    public static IndicadoresResposta De(IndicadoresDeValorAgregado indicadores) =>
        new(
            indicadores.OrcamentoNoTermino.Valor,
            indicadores.ValorPlanejado.Valor,
            indicadores.ValorAgregado.Valor,
            indicadores.CustoReal.Valor,
            indicadores.VariacaoDePrazo.Valor,
            indicadores.VariacaoDeCusto.Valor,
            indicadores.IndiceDeDesempenhoDePrazo,
            indicadores.IndiceDeDesempenhoDeCusto,
            indicadores.PercentualPlanejado,
            indicadores.PercentualExecutado,
            indicadores.EstimativaNoTermino?.Valor,
            indicadores.EstimativaNoTerminoComRestanteNoOrcamento.Valor,
            indicadores.EstimativaNoTerminoComCustoEPrazo?.Valor,
            indicadores.EstimativaParaTerminar?.Valor,
            indicadores.VariacaoNoTermino?.Valor,
            indicadores.IndiceDeDesempenhoParaTerminar);
}

public sealed record PrazoAgregadoResposta(
    int DuracaoPlanejadaEmMeses,
    int PrazoDecorridoEmMeses,
    decimal PrazoAgregadoEmMeses,
    decimal VariacaoDePrazoEmMeses,
    decimal? IndiceDeDesempenhoDePrazo,
    decimal? DuracaoEstimadaEmMeses)
{
    public static PrazoAgregadoResposta De(PrazoAgregado prazo) =>
        new(
            prazo.DuracaoPlanejadaEmMeses,
            prazo.PrazoDecorridoEmMeses,
            prazo.PrazoAgregadoEmMeses,
            prazo.VariacaoDePrazoEmMeses,
            prazo.IndiceDeDesempenhoDePrazo,
            prazo.DuracaoEstimadaEmMeses);
}

public sealed record LinhaDoValorAgregadoResposta(string Numero, string Descricao, IndicadoresResposta Indicadores);

/// <summary>Um mês da curva S. Valor agregado e custo real só existem até o mês medido.</summary>
public sealed record PontoDaCurvaResposta(DateOnly Mes, decimal ValorPlanejado, decimal? ValorAgregado, decimal? CustoReal);
