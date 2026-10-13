using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// As parcelas de que se compõe o BDI, em percentual: 4,00 quer dizer 4,00 %.
/// </summary>
/// <remarks>
/// As de cima incidem sobre o custo direto; tributos incidem sobre o preço
/// de venda — por isso entram dividindo, e não somando. Ver
/// <see cref="Bdi.PelaFormulaDoTcu"/>.
/// </remarks>
public sealed record ParcelasDoBdi
{
    public ParcelasDoBdi(
        decimal administracaoCentral,
        decimal seguros,
        decimal riscos,
        decimal garantias,
        decimal despesasFinanceiras,
        decimal lucro,
        decimal tributos)
    {
        AdministracaoCentral = Validar(administracaoCentral, "administração central");
        Seguros = Validar(seguros, "seguros");
        Riscos = Validar(riscos, "riscos");
        Garantias = Validar(garantias, "garantias");
        DespesasFinanceiras = Validar(despesasFinanceiras, "despesas financeiras");
        Lucro = Validar(lucro, "lucro");
        Tributos = Validar(tributos, "tributos");
    }

    public decimal AdministracaoCentral { get; }

    public decimal Seguros { get; }

    public decimal Riscos { get; }

    public decimal Garantias { get; }

    public decimal DespesasFinanceiras { get; }

    public decimal Lucro { get; }

    /// <summary>PIS, COFINS, ISS e, quando houver, a contribuição previdenciária sobre a receita.</summary>
    public decimal Tributos { get; }

    /*
     * Menos de 100 %, e não "até 100 %": tributo de 100 % sobre o preço de
     * venda zera o divisor da fórmula. Na prática nenhuma parcela chega perto;
     * o limite está aqui para o erro de digitação não virar divisão por zero.
     */
    private static decimal Validar(decimal percentual, string parcela)
    {
        if (percentual is < 0 or >= 100)
        {
            throw new ExcecaoDeDominio($"A parcela de {parcela} do BDI vai de 0 a menos de 100 %; {percentual} não serve.");
        }

        if (percentual != decimal.Round(percentual, 2))
        {
            throw new ExcecaoDeDominio($"A parcela de {parcela} do BDI tem no máximo duas casas decimais.");
        }

        return percentual;
    }
}
