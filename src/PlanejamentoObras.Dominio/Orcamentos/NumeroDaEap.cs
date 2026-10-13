using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// A posição de uma linha na EAP — a Estrutura Analítica do Projeto: "2",
/// "2.1", "2.1.3".
/// </summary>
/// <remarks>
/// É a forma como o engenheiro se refere a uma linha ("o item 2.1.3 da
/// planilha"), e é por ela que o orçamento é editado. A numeração vem da
/// posição: removida a etapa 1, a antiga etapa 2 passa a ser a 1 — como numa
/// planilha (ADR-0005).
/// </remarks>
public sealed record NumeroDaEap
{
    private NumeroDaEap(string valor, int nivel)
    {
        Valor = valor;
        Nivel = nivel;
    }

    /// <summary>"2.1.3".</summary>
    public string Valor { get; }

    /// <summary>1 para uma etapa de primeiro nível, 2 para o que está dentro dela, e assim por diante.</summary>
    public int Nivel { get; }

    public static NumeroDaEap Ler(string texto) =>
        TentarLer(texto, out var numero)
            ? numero
            : throw new ExcecaoDeDominio(
                $"\"{texto}\" não é um número de EAP. Use posições a partir de 1, separadas por ponto: 2.1.3.");

    /// <summary>Como <see cref="Ler"/>, para quem junta vários erros antes de acusá-los.</summary>
    internal static bool TentarLer(string? texto, [NotNullWhen(true)] out NumeroDaEap? numero)
    {
        var partes = (texto ?? string.Empty).Trim().Split('.');
        var posicoes = new List<int>(partes.Length);

        foreach (var parte in partes)
        {
            if (!int.TryParse(parte, NumberStyles.None, CultureInfo.InvariantCulture, out var posicao) || posicao < 1)
            {
                numero = null;

                return false;
            }

            posicoes.Add(posicao);
        }

        numero = De(posicoes);

        return true;
    }

    /// <summary>As posições, a partir de 1: "2.1.3" dá 2, 1 e 3.</summary>
    public IReadOnlyList<int> Posicoes() =>
        [.. Valor.Split('.').Select(parte => int.Parse(parte, CultureInfo.InvariantCulture))];

    public override string ToString() => Valor;

    internal static NumeroDaEap De(IReadOnlyList<int> posicoes) =>
        new(string.Join('.', posicoes), posicoes.Count);

    internal NumeroDaEap Filho(int posicao) => new($"{Valor}.{posicao}", Nivel + 1);

    /// <summary>As etapas acima desta linha, de cima para baixo: de "2.2.1", as etapas 2 e 2.2.</summary>
    internal IEnumerable<NumeroDaEap> Ancestrais()
    {
        var posicoes = Posicoes();

        for (var nivel = 1; nivel < posicoes.Count; nivel++)
        {
            yield return De([.. posicoes.Take(nivel)]);
        }
    }
}
