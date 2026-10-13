using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.ValorAgregado;

/// <summary>
/// Os indicadores de valor agregado de uma linha — ou da obra inteira — numa
/// data de status.
/// </summary>
/// <remarks>
/// <para>
/// Quatro valores dados, todos a custo — nunca a preço de venda —, e o resto
/// derivado. Os índices são razões entre os valores, sem arredondar: quem
/// mostra arredonda. As estimativas são em dinheiro, e saem dos valores —
/// nunca de um índice arredondado —, para se refazerem à mão a partir do que
/// a tela mostra (ADR-0009).
/// </para>
/// <para>
/// Índice que não tem como existir é nulo: sem valor planejado, não há
/// desempenho de prazo; sem custo real, não há desempenho de custo.
/// </para>
/// </remarks>
/// <param name="OrcamentoNoTermino">ONT (BAC): o custo orçado da linha toda executada.</param>
/// <param name="ValorPlanejado">VP (PV): o custo orçado do que se planejou executar até a data de status.</param>
/// <param name="ValorAgregado">VA (EV): o custo orçado do que se executou — quantidade medida × custo unitário da linha de base.</param>
/// <param name="CustoReal">CR (AC): quanto de fato custou o que se executou.</param>
public sealed record IndicadoresDeValorAgregado(
    Dinheiro OrcamentoNoTermino,
    Dinheiro ValorPlanejado,
    Dinheiro ValorAgregado,
    Dinheiro CustoReal)
{
    /// <summary>VPR (SV): positiva, adiantado; negativa, atrasado — em dinheiro.</summary>
    public Dinheiro VariacaoDePrazo => ValorAgregado - ValorPlanejado;

    /// <summary>VC (CV): positiva, abaixo do custo; negativa, acima.</summary>
    public Dinheiro VariacaoDeCusto => ValorAgregado - CustoReal;

    /// <summary>IDP (SPI): VA / VP. Abaixo de 1, atrasado.</summary>
    public decimal? IndiceDeDesempenhoDePrazo => ValorPlanejado == Dinheiro.Zero ? null : ValorAgregado / ValorPlanejado;

    /// <summary>IDC (CPI): VA / CR. Abaixo de 1, gastando mais que o orçado para o que executou.</summary>
    public decimal? IndiceDeDesempenhoDeCusto => CustoReal == Dinheiro.Zero ? null : ValorAgregado / CustoReal;

    /// <summary>VP / ONT: quanto da linha deveria estar pronto.</summary>
    public decimal PercentualPlanejado => Fracao(ValorPlanejado);

    /// <summary>VA / ONT: quanto da linha está pronto.</summary>
    public decimal PercentualExecutado => Fracao(ValorAgregado);

    /// <summary>
    /// ENT (EAC) supondo que o desempenho de custo de até aqui continua:
    /// ONT / IDC, calculado como ONT × CR / VA.
    /// </summary>
    public Dinheiro? EstimativaNoTermino =>
        ValorAgregado == Dinheiro.Zero || CustoReal == Dinheiro.Zero
            ? null
            : new Dinheiro(OrcamentoNoTermino.Valor * CustoReal.Valor / ValorAgregado.Valor).EmCentavos();

    /// <summary>
    /// ENT supondo que o desvio de até aqui não se repete: CR + (ONT − VA).
    /// </summary>
    public Dinheiro EstimativaNoTerminoComRestanteNoOrcamento => CustoReal + (OrcamentoNoTermino - ValorAgregado);

    /// <summary>
    /// ENT supondo que custo e prazo pesam juntos no que falta:
    /// CR + (ONT − VA) / (IDC × IDP), calculado como CR + (ONT − VA) × CR × VP / VA².
    /// </summary>
    public Dinheiro? EstimativaNoTerminoComCustoEPrazo =>
        ValorAgregado == Dinheiro.Zero || CustoReal == Dinheiro.Zero || ValorPlanejado == Dinheiro.Zero
            ? null
            : CustoReal + new Dinheiro(
                (OrcamentoNoTermino - ValorAgregado).Valor * CustoReal.Valor * ValorPlanejado.Valor
                / (ValorAgregado.Valor * ValorAgregado.Valor)).EmCentavos();

    /// <summary>EPT (ETC): quanto ainda se vai gastar, pela ENT.</summary>
    public Dinheiro? EstimativaParaTerminar => EstimativaNoTermino - CustoReal;

    /// <summary>VNT (VAC): ONT − ENT. Negativa, a obra vai custar mais que o orçado.</summary>
    public Dinheiro? VariacaoNoTermino => OrcamentoNoTermino - EstimativaNoTermino;

    /// <summary>
    /// IDPT (TCPI): o desempenho de custo necessário daqui em diante para
    /// terminar dentro do orçamento — (ONT − VA) / (ONT − CR). Nulo quando o
    /// orçamento já foi todo gasto.
    /// </summary>
    public decimal? IndiceDeDesempenhoParaTerminar =>
        OrcamentoNoTermino <= CustoReal ? null : (OrcamentoNoTermino - ValorAgregado) / (OrcamentoNoTermino - CustoReal);

    private decimal Fracao(Dinheiro parte) => OrcamentoNoTermino == Dinheiro.Zero ? 0m : parte / OrcamentoNoTermino;
}
