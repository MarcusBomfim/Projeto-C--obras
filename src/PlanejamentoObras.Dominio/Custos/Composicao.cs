using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Custos;

/// <summary>
/// Composição de custo unitário: o que entra em uma unidade de um serviço.
/// </summary>
/// <remarks>
/// <para>
/// Um metro quadrado de alvenaria de bloco cerâmico leva 13,5 blocos, 0,012 m³
/// de argamassa, 0,8 h de pedreiro e 0,4 h de servente. A argamassa, por sua
/// vez, é outra composição — uma composição auxiliar —, com cimento, cal,
/// areia, servente e betoneira.
/// </para>
/// <para>
/// Como a atividade no cronograma, só muda pela raiz do agregado: quem sabe
/// se um item novo fecha um ciclo é a base de custos inteira, não a
/// composição sozinha.
/// </para>
/// </remarks>
public sealed class Composicao
{
    public const int TamanhoMaximoDaDescricao = 200;

    private readonly List<ItemDaComposicao> _itens = [];

    internal Composicao(CodigoDeCusto codigo, string descricao, UnidadeDeMedida unidade)
    {
        Codigo = codigo;
        Descricao = ValidarDescricao(codigo, descricao);
        Unidade = unidade;
    }

    public CodigoDeCusto Codigo { get; }

    public string Descricao { get; }

    public UnidadeDeMedida Unidade { get; }

    /// <summary>Os itens, na ordem em que foram incluídos.</summary>
    public IReadOnlyList<ItemDaComposicao> Itens => _itens.AsReadOnly();

    internal bool Usa(CodigoDeCusto codigo) => _itens.Exists(item => item.Codigo == codigo);

    internal void AdicionarItem(ItemDaComposicao item) => _itens.Add(item);

    internal void AlterarCoeficiente(CodigoDeCusto codigo, decimal coeficiente) =>
        _itens[Posicao(codigo)] = new ItemDaComposicao(codigo, coeficiente);

    internal void RemoverItem(CodigoDeCusto codigo) => _itens.RemoveAt(Posicao(codigo));

    private int Posicao(CodigoDeCusto codigo)
    {
        var posicao = _itens.FindIndex(item => item.Codigo == codigo);

        return posicao >= 0
            ? posicao
            : throw new ExcecaoDeDominio($"A composição {Codigo} não usa {codigo}.");
    }

    private static string ValidarDescricao(CodigoDeCusto codigo, string descricao)
    {
        var limpa = (descricao ?? string.Empty).Trim();

        if (limpa.Length == 0)
        {
            throw new ExcecaoDeDominio($"A composição {codigo} precisa de uma descrição.");
        }

        if (limpa.Length > TamanhoMaximoDaDescricao)
        {
            throw new ExcecaoDeDominio(
                $"A descrição da composição {codigo} tem no máximo {TamanhoMaximoDaDescricao} caracteres.");
        }

        return limpa;
    }
}
