using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.FisicoFinanceiro;

/// <summary>
/// Divide um valor em partes proporcionais a pesos, ao centavo, sem perder
/// nem criar centavo.
/// </summary>
/// <remarks>
/// <para>
/// R$ 10.000,00 em seis dias, cinco em outubro e um em novembro: a parte
/// exata de outubro é 8.333,333…, e a de novembro, 1.666,666…. Arredondar
/// cada uma dá 8.333,33 + 1.666,67 = 10.000,00 — aqui fecha, mas em três
/// partes iguais de R$ 100,00, arredondar cada uma dá 33,33 × 3 = 99,99.
/// </para>
/// <para>
/// Método do maior resto: cada parte recebe os centavos inteiros da sua
/// parcela exata; os centavos que sobram vão, um a um, para as partes com o
/// maior resto. Nenhuma parte fica a um centavo ou mais da sua parcela exata,
/// e a soma é sempre o total. Empate de resto fica com a parte que vem antes
/// — o mês mais cedo.
/// </para>
/// </remarks>
internal static class Rateio
{
    public static Dinheiro[] Distribuir(Dinheiro total, IReadOnlyList<int> pesos)
    {
        if (!total.EstaEmCentavos || total < Dinheiro.Zero)
        {
            throw new InvalidOperationException($"Só se rateia valor positivo em centavos; veio {total}.");
        }

        var somaDosPesos = pesos.Sum();

        if (somaDosPesos <= 0 || pesos.Any(peso => peso < 0))
        {
            throw new InvalidOperationException("O rateio precisa de pesos que não sejam negativos e somem mais que zero.");
        }

        var centavos = total.Valor * 100;
        var partes = new decimal[pesos.Count];
        var restos = new decimal[pesos.Count];

        for (var i = 0; i < pesos.Count; i++)
        {
            var exato = centavos * pesos[i];
            partes[i] = decimal.Floor(exato / somaDosPesos);
            restos[i] = exato - (partes[i] * somaDosPesos);
        }

        var sobram = (int)(centavos - partes.Sum());

        // OrderByDescending é estável: no empate, a posição menor vem antes.
        foreach (var posicao in Enumerable.Range(0, pesos.Count).OrderByDescending(i => restos[i]).Take(sobram))
        {
            partes[posicao]++;
        }

        return [.. partes.Select(parte => new Dinheiro(parte / 100))];
    }
}
