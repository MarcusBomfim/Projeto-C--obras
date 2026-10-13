using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Custos;

/// <summary>
/// Uma composição com os custos abertos linha a linha — a "composição
/// analítica" que vai para o anexo do orçamento.
/// </summary>
/// <remarks>
/// Fotografia dos preços no momento do cálculo: mudou um preço na base,
/// calcula-se outra.
/// </remarks>
public sealed class ComposicaoAnalitica
{
    internal ComposicaoAnalitica(Composicao composicao, IReadOnlyList<LinhaDaComposicao> linhas)
    {
        Codigo = composicao.Codigo;
        Descricao = composicao.Descricao;
        Unidade = composicao.Unidade;
        Linhas = linhas;
        CustoUnitario = linhas.Aggregate(Dinheiro.Zero, (soma, linha) => soma + linha.CustoParcial);
    }

    public CodigoDeCusto Codigo { get; }

    public string Descricao { get; }

    public UnidadeDeMedida Unidade { get; }

    public IReadOnlyList<LinhaDaComposicao> Linhas { get; }

    /// <summary>
    /// A soma dos custos parciais: o custo de uma unidade do serviço, em
    /// centavos, sem BDI.
    /// </summary>
    public Dinheiro CustoUnitario { get; }
}
