using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Custos;

/// <summary>
/// A unidade em que algo é medido e pago: "m²", "m³", "kg", "h", "un", "vb".
/// </summary>
/// <remarks>
/// Guardada como foi escrita, sem tabela fechada de unidades. Cada base de
/// preços tem as suas — SINAPI escreve "M2", tabela própria escreve "m²" — e
/// o sistema não converte uma na outra: o coeficiente de uma composição já
/// diz quanto de cada coisa entra, na unidade de cada uma.
/// </remarks>
public sealed record UnidadeDeMedida
{
    public const int TamanhoMaximo = 10;

    public UnidadeDeMedida(string valor)
    {
        var limpo = (valor ?? string.Empty).Trim();

        if (limpo.Length == 0)
        {
            throw new ExcecaoDeDominio("A unidade de medida é obrigatória.");
        }

        if (limpo.Length > TamanhoMaximo)
        {
            throw new ExcecaoDeDominio(
                $"A unidade de medida tem no máximo {TamanhoMaximo} caracteres; \"{limpo}\" tem {limpo.Length}.");
        }

        Valor = limpo;
    }

    public string Valor { get; }

    public override string ToString() => Valor;
}
