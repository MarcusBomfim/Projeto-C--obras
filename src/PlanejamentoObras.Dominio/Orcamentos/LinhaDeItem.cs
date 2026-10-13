using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// Um item com preço: o que se lê numa linha de planilha orçamentária.
/// </summary>
/// <param name="CustoUnitario">O custo da composição, ou o preço do insumo, sem BDI.</param>
/// <param name="Bdi">O BDI aplicado: o diferenciado do item, ou o do orçamento.</param>
/// <param name="PrecoUnitario">Custo unitário com BDI, em centavos.</param>
/// <param name="Atividade">A atividade do cronograma em que o serviço é feito, se vinculada.</param>
public sealed record LinhaDeItem(
    NumeroDaEap Numero,
    string Descricao,
    Dinheiro CustoTotal,
    Dinheiro Total,
    decimal Peso,
    CodigoDeCusto Codigo,
    UnidadeDeMedida Unidade,
    decimal Quantidade,
    Dinheiro CustoUnitario,
    Bdi Bdi,
    Dinheiro PrecoUnitario,
    CodigoDaAtividade? Atividade)
    : LinhaDoOrcamento(Numero, Descricao, CustoTotal, Total, Peso);
