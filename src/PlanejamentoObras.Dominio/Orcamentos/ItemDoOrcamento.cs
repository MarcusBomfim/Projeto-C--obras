using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// Uma linha orçada: um serviço da base de custos, numa quantidade.
/// </summary>
/// <remarks>
/// <para>
/// O item guarda o código e a quantidade, não o preço. Descrição, unidade e
/// custo vêm da base no momento do cálculo (ADR-0005).
/// </para>
/// <para>
/// O vínculo com a atividade do cronograma é por código, e não por
/// referência: orçamento e cronograma são agregados diferentes, editados em
/// momentos diferentes. Quem confere que a atividade existe é o cronograma
/// físico-financeiro, quando junta os dois.
/// </para>
/// </remarks>
public sealed class ItemDoOrcamento : NoDaEap
{
    internal ItemDoOrcamento(CodigoDeCusto codigo, decimal quantidade)
    {
        Codigo = codigo;
        Quantidade = ValidarQuantidade(codigo, quantidade);
    }

    /// <summary>A composição — ou o insumo — da base de custos que este item orça.</summary>
    public CodigoDeCusto Codigo { get; }

    /// <summary>Na unidade da composição ou do insumo, com até duas casas.</summary>
    public decimal Quantidade { get; private set; }

    /// <summary>A atividade do cronograma em que o serviço é executado; nulo enquanto não vinculado.</summary>
    public CodigoDaAtividade? Atividade { get; private set; }

    /// <summary>
    /// Um BDI só deste item, no lugar do BDI do orçamento; nulo quando vale o
    /// do orçamento.
    /// </summary>
    /// <remarks>
    /// O caso típico é o fornecimento de equipamento — o elevador comprado
    /// pronto, em que a construtora só intermedeia. Cobrar sobre ele o mesmo
    /// BDI da alvenaria remunera um risco e uma administração que não existem.
    /// </remarks>
    public Bdi? BdiDiferenciado { get; private set; }

    internal void AlterarQuantidade(decimal quantidade) => Quantidade = ValidarQuantidade(Codigo, quantidade);

    internal void VincularAtividade(CodigoDaAtividade? atividade) => Atividade = atividade;

    internal void DefinirBdiDiferenciado(Bdi? bdi) => BdiDiferenciado = bdi;

    private static decimal ValidarQuantidade(CodigoDeCusto codigo, decimal quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ExcecaoDeDominio($"A quantidade de {codigo} precisa ser maior que zero.");
        }

        if (quantidade != decimal.Round(quantidade, 2))
        {
            throw new ExcecaoDeDominio(
                $"A quantidade de {codigo} tem no máximo duas casas decimais: é o número que vai para a planilha.");
        }

        return quantidade;
    }
}
