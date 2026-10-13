using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Custos;

/// <summary>
/// Quanto de um insumo — ou de outra composição — entra em uma unidade da
/// composição: 13,5 blocos por metro quadrado de alvenaria.
/// </summary>
/// <remarks>
/// Imutável: mudar o coeficiente é trocar o item, pela base de custos.
/// </remarks>
public sealed record ItemDaComposicao
{
    internal ItemDaComposicao(CodigoDeCusto codigo, decimal coeficiente)
    {
        if (coeficiente <= 0)
        {
            throw new ExcecaoDeDominio(
                $"O coeficiente de {codigo} precisa ser maior que zero. Para tirar o item, remova-o da composição.");
        }

        Codigo = codigo;
        Coeficiente = coeficiente;
    }

    public CodigoDeCusto Codigo { get; }

    /// <summary>Na unidade do item, por unidade da composição: horas por m², kg por m³.</summary>
    public decimal Coeficiente { get; }
}
