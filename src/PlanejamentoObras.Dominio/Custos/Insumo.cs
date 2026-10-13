using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Custos;

/// <summary>
/// O que se compra ou se paga por unidade: o saco de cimento, a hora do
/// pedreiro, a hora da betoneira.
/// </summary>
/// <remarks>
/// O preço é o final, na data-base da base de custos: para mão de obra, já
/// com os encargos sociais. Calcular encargos é trabalho de quem publica a
/// base de preços (SINAPI, tabela própria da construtora), não do orçamento.
/// </remarks>
public sealed class Insumo
{
    public const int TamanhoMaximoDaDescricao = 200;

    internal Insumo(CodigoDeCusto codigo, string descricao, UnidadeDeMedida unidade, TipoDeInsumo tipo, Dinheiro preco)
    {
        if (!Enum.IsDefined(tipo))
        {
            throw new ExcecaoDeDominio($"O tipo do insumo {codigo} não é válido.");
        }

        Codigo = codigo;
        Descricao = ValidarDescricao(codigo, descricao);
        Unidade = unidade;
        Tipo = tipo;
        Preco = ValidarPreco(codigo, preco);
    }

    public CodigoDeCusto Codigo { get; }

    public string Descricao { get; }

    public UnidadeDeMedida Unidade { get; }

    public TipoDeInsumo Tipo { get; }

    /// <summary>O preço de uma unidade, em centavos.</summary>
    public Dinheiro Preco { get; private set; }

    internal void AlterarPreco(Dinheiro novoPreco) => Preco = ValidarPreco(Codigo, novoPreco);

    private static string ValidarDescricao(CodigoDeCusto codigo, string descricao)
    {
        var limpa = (descricao ?? string.Empty).Trim();

        if (limpa.Length == 0)
        {
            throw new ExcecaoDeDominio($"O insumo {codigo} precisa de uma descrição.");
        }

        if (limpa.Length > TamanhoMaximoDaDescricao)
        {
            throw new ExcecaoDeDominio(
                $"A descrição do insumo {codigo} tem no máximo {TamanhoMaximoDaDescricao} caracteres.");
        }

        return limpa;
    }

    /*
     * Preço em centavos, como sai da nota fiscal e da tabela de preços. Um
     * preço de R$ 1,3249 não existe no mundo — e aceitá-lo faria a primeira
     * conta da composição já não se reproduzir à mão (ADR-0004).
     */
    private static Dinheiro ValidarPreco(CodigoDeCusto codigo, Dinheiro preco)
    {
        if (preco < Dinheiro.Zero)
        {
            throw new ExcecaoDeDominio($"O preço do insumo {codigo} não pode ser negativo.");
        }

        if (!preco.EstaEmCentavos)
        {
            throw new ExcecaoDeDominio($"O preço do insumo {codigo} vai até o centavo; {preco} tem casas demais.");
        }

        return preco;
    }
}
