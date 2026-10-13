namespace PlanejamentoObras.Dominio.ValorAgregado;

/// <summary>
/// O prazo agregado (earned schedule): em que mês do plano a obra está, pelo
/// que já executou — o atraso medido em tempo, e não em dinheiro.
/// </summary>
/// <remarks>
/// <para>
/// O IDP clássico, VA / VP, tem um defeito conhecido: no fim da obra VA e VP
/// chegam os dois ao orçamento, e o IDP volta a 1 — mesmo que a obra tenha
/// terminado meses atrasada. O prazo agregado não volta: a obra que executou
/// em três meses o planejado para dois tem prazo agregado 2 e prazo decorrido
/// 3, e o índice fica em 0,67 (ADR-0009).
/// </para>
/// <para>
/// Prazo agregado = C + (VA − VP<sub>C</sub>) / (VP<sub>C+1</sub> − VP<sub>C</sub>),
/// em que C é o último mês cujo valor planejado acumulado não passa do valor
/// agregado: a parte inteira são os meses do plano já cumpridos; a fração, o
/// quanto do mês seguinte.
/// </para>
/// </remarks>
/// <param name="DuracaoPlanejadaEmMeses">Quantos meses a linha de base tem.</param>
/// <param name="PrazoDecorridoEmMeses">Do primeiro mês da linha de base até o mês de status, inclusive.</param>
/// <param name="PrazoAgregadoEmMeses">Até que ponto do plano o executado chega, em meses.</param>
public sealed record PrazoAgregado(
    int DuracaoPlanejadaEmMeses,
    int PrazoDecorridoEmMeses,
    decimal PrazoAgregadoEmMeses)
{
    /// <summary>Positiva, adiantado; negativa, atrasado — em meses.</summary>
    public decimal VariacaoDePrazoEmMeses => PrazoAgregadoEmMeses - PrazoDecorridoEmMeses;

    /// <summary>IDP(t): prazo agregado / prazo decorrido. Nulo antes de a linha de base começar.</summary>
    public decimal? IndiceDeDesempenhoDePrazo =>
        PrazoDecorridoEmMeses <= 0 ? null : PrazoAgregadoEmMeses / PrazoDecorridoEmMeses;

    /// <summary>
    /// Quantos meses a obra vai levar se o ritmo de até aqui continuar:
    /// duração planejada / IDP(t).
    /// </summary>
    /// <remarks>
    /// Calculada como duração planejada × prazo decorrido / prazo agregado,
    /// e não dividindo pelo índice: o índice de 2 por 3 já é uma dízima, e a
    /// obra que levou 3 meses sairia com 2,999… (ADR-0009).
    /// </remarks>
    public decimal? DuracaoEstimadaEmMeses =>
        PrazoDecorridoEmMeses > 0 && PrazoAgregadoEmMeses > 0
            ? DuracaoPlanejadaEmMeses * (decimal)PrazoDecorridoEmMeses / PrazoAgregadoEmMeses
            : null;
}
