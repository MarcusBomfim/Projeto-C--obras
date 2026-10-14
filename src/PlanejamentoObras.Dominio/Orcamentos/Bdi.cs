using System.Globalization;
using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// Benefícios e Despesas Indiretas: o percentual que transforma custo direto
/// em preço de venda.
/// </summary>
/// <remarks>
/// <para>
/// O custo direto paga o que vai para a obra — material, mão de obra,
/// equipamento. O BDI paga o resto: o escritório central, o seguro, o risco,
/// o custo financeiro, os tributos e o lucro. Preço = custo × (1 + BDI).
/// </para>
/// <para>
/// O percentual vai com duas casas, como aparece na planilha, e é com ele —
/// e não com o valor exato da fórmula — que se calcula o preço (ADR-0005).
/// </para>
/// </remarks>
public sealed record Bdi
{
    public const decimal PercentualMaximo = 100m;

    public static readonly Bdi Zero = new(0m, parcelas: null);

    private Bdi(decimal percentual, ParcelasDoBdi? parcelas)
    {
        Percentual = percentual;
        Parcelas = parcelas;
    }

    /// <summary>Em percentual, com duas casas: 22,88 quer dizer 22,88 %.</summary>
    public decimal Percentual { get; }

    /// <summary>As parcelas de que o BDI foi calculado; nulo quando foi informado pronto.</summary>
    public ParcelasDoBdi? Parcelas { get; }

    /// <summary>Um BDI dado pronto — pelo edital, pelo cliente, pela política da empresa.</summary>
    public static Bdi Informado(decimal percentual)
    {
        if (percentual != decimal.Round(percentual, 2))
        {
            throw new ExcecaoDeDominio("O BDI tem no máximo duas casas decimais: é o número que vai para a planilha.");
        }

        return new Bdi(ValidarFaixa(percentual), parcelas: null);
    }

    /// <summary>
    /// Refaz um BDI gravado exatamente como foi gravado: o percentual e, se
    /// houver, as parcelas — sem recalcular a fórmula.
    /// </summary>
    /// <remarks>
    /// O percentual gravado é o que foi aprovado e foi para a planilha. Se um
    /// dia a regra de arredondamento mudar, recalcular na leitura mudaria em
    /// silêncio o preço de um orçamento já fechado (ADR-0010).
    /// </remarks>
    internal static Bdi Reconstituir(decimal percentual, ParcelasDoBdi? parcelas) =>
        new(Informado(percentual).Percentual, parcelas);

    /// <summary>
    /// O BDI calculado pela fórmula do Acórdão 2.622/2013 do TCU:
    /// <c>BDI = (1 + AC + S + R + G) × (1 + DF) × (1 + L) / (1 − I) − 1</c>.
    /// </summary>
    /// <remarks>
    /// Administração central, seguros, riscos e garantias somam-se sobre o
    /// custo; despesas financeiras e lucro incidem em cascata; tributos
    /// incidem sobre o preço de venda, e por isso dividem. O resultado é
    /// arredondado a duas casas pela NBR 5891.
    /// </remarks>
    public static Bdi PelaFormulaDoTcu(ParcelasDoBdi parcelas)
    {
        ArgumentNullException.ThrowIfNull(parcelas);

        var fator =
            (1 + ((parcelas.AdministracaoCentral + parcelas.Seguros + parcelas.Riscos + parcelas.Garantias) / 100))
            * (1 + (parcelas.DespesasFinanceiras / 100))
            * (1 + (parcelas.Lucro / 100))
            / (1 - (parcelas.Tributos / 100));

        var percentual = decimal.Round((fator - 1) * 100, 2, MidpointRounding.ToEven);

        return new Bdi(ValidarFaixa(percentual), parcelas);
    }

    /// <summary>O preço unitário: o custo unitário com o BDI, arredondado ao centavo.</summary>
    public Dinheiro Aplicar(Dinheiro custoUnitario) => (custoUnitario * (1 + (Percentual / 100))).EmCentavos();

    /// <summary>"22,88 %".</summary>
    public override string ToString() =>
        Percentual.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',') + " %";

    private static decimal ValidarFaixa(decimal percentual) =>
        percentual is >= 0 and <= PercentualMaximo
            ? percentual
            : throw new ExcecaoDeDominio($"O BDI vai de 0 a {PercentualMaximo} %; {percentual} % não serve. Confira as parcelas.");
}
