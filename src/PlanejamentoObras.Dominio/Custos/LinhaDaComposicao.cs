using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Custos;

/// <summary>
/// Uma linha da composição analítica: o item, quanto entra dele e quanto custa.
/// </summary>
/// <param name="Tipo">O tipo do insumo; nulo quando a linha é uma composição auxiliar.</param>
/// <param name="CustoUnitario">O preço do insumo, ou o custo unitário da composição auxiliar.</param>
/// <param name="CustoParcial">Coeficiente vezes custo unitário, arredondado ao centavo.</param>
public sealed record LinhaDaComposicao(
    CodigoDeCusto Codigo,
    string Descricao,
    UnidadeDeMedida Unidade,
    TipoDeInsumo? Tipo,
    decimal Coeficiente,
    Dinheiro CustoUnitario,
    Dinheiro CustoParcial)
{
    public bool EhComposicaoAuxiliar => Tipo is null;
}
