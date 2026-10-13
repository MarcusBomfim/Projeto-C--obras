using PlanejamentoObras.Dominio.Cronogramas;

namespace PlanejamentoObras.Dominio.Testes.Cronogramas;

/// <summary>
/// Redes acíclicas sorteadas, para os testes por propriedade.
/// </summary>
/// <remarks>
/// As sementes são fixas. Um teste aleatório que falha uma vez em cada mil
/// execuções e não se reproduz é pior do que nenhum teste.
/// </remarks>
internal static class RedesAleatorias
{
    private const int Quantidade = 400;
    private const int PorLote = 50;

    /// <summary>
    /// As redes vão em lotes de 50: uma falha aponta o lote, a mensagem
    /// aponta a semente, e a semente reproduz a rede.
    /// </summary>
    public static TheoryData<int> Lotes()
    {
        var lotes = new TheoryData<int>();

        for (var lote = 0; lote < Quantidade / PorLote; lote++)
        {
            lotes.Add(lote);
        }

        return lotes;
    }

    public static IEnumerable<int> SementesDoLote(int lote) => Enumerable.Range(lote * PorLote, PorLote);

    /// <summary>
    /// Uma rede acíclica com até 30 atividades, marcos, os quatro tipos de
    /// vínculo e defasagens de -3 a +5.
    /// </summary>
    /// <remarks>
    /// Para garantir que não há ciclo, cada atividade recebe uma posição
    /// sorteada, e só se cria vínculo de posição menor para maior. A ordem de
    /// cadastro é outra, também sorteada — senão a ordem topológica sairia
    /// igual à de cadastro e o teste não exercitaria a ordenação.
    /// </remarks>
    public static Cronograma Gerar(int semente)
    {
#pragma warning disable CA5394 // Aleatório não criptográfico de propósito: o que se quer é reprodutibilidade.
        var sorteio = new Random(semente);
        var quantidade = sorteio.Next(1, 31);
        var cronograma = new Cronograma($"Rede {semente}");

        var posicoes = Enumerable.Range(0, quantidade).OrderBy(_ => sorteio.Next()).ToArray();

        for (var i = 0; i < quantidade; i++)
        {
            // Um terço de chance de ser marco: é onde mora o caso estranho.
            var duracao = sorteio.Next(3) == 0 ? 0 : sorteio.Next(1, 16);
            cronograma.AdicionarAtividade($"A{i}", $"Atividade {i}", duracao);
        }

        var tipos = Enum.GetValues<TipoDeDependencia>();
        var pares = new List<(int De, int Para)>();

        for (var i = 0; i < quantidade; i++)
        {
            for (var j = 0; j < quantidade; j++)
            {
                if (posicoes[i] < posicoes[j] && sorteio.NextDouble() < 0.2)
                {
                    pares.Add((i, j));
                }
            }
        }

        foreach (var (de, para) in pares.OrderBy(_ => sorteio.Next()))
        {
            cronograma.AdicionarDependencia(
                $"A{de}",
                $"A{para}",
                tipos[sorteio.Next(tipos.Length)],
                sorteio.Next(-3, 6));
        }
#pragma warning restore CA5394

        return cronograma;
    }
}
