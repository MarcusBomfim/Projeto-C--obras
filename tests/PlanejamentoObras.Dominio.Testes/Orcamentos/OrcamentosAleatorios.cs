using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Dominio.Testes.Cronogramas;

namespace PlanejamentoObras.Dominio.Testes.Orcamentos;

/// <summary>
/// Bases de custos e orçamentos sorteados, para os testes por propriedade.
/// </summary>
/// <remarks>
/// Mesmos lotes e mesmas sementes fixas das redes sorteadas do cronograma.
/// </remarks>
internal static class OrcamentosAleatorios
{
    public static TheoryData<int> Lotes() => RedesAleatorias.Lotes();

    public static IEnumerable<int> SementesDoLote(int lote) => RedesAleatorias.SementesDoLote(lote);

    /// <summary>
    /// Uma base com até 12 insumos e 10 composições — com auxiliares em até
    /// vários níveis — e um orçamento de até 4 etapas, com subetapas, itens
    /// que são composição ou insumo, e BDI informado, pela fórmula ou
    /// diferenciado.
    /// </summary>
    /// <remarks>
    /// Para não haver ciclo, uma composição só usa o que foi cadastrado antes
    /// dela. Preços, coeficientes e quantidades caem em toda a faixa de casas
    /// decimais permitida, para o arredondamento ter meio centavo de verdade
    /// para decidir.
    /// </remarks>
    public static (BaseDeCustos Base, Orcamento Orcamento) Gerar(int semente)
    {
        var sorteio = new Random(semente);
        var baseDeCustos = new BaseDeCustos($"Base {semente}", new DateOnly(2026, 9, 1));
        var codigos = new List<string>();
        var tipos = Enum.GetValues<TipoDeInsumo>();

        var insumos = Sortear(sorteio, 3, 13);

        for (var i = 0; i < insumos; i++)
        {
            baseDeCustos.AdicionarInsumo($"I{i}", $"Insumo {i}", "un", tipos[Sortear(sorteio, 0, tipos.Length)], Sortear(sorteio, 0, 100_000) / 100m);
            codigos.Add($"I{i}");
        }

        var composicoes = Sortear(sorteio, 2, 11);

        for (var c = 0; c < composicoes; c++)
        {
            var codigo = $"C{c}";
            baseDeCustos.AdicionarComposicao(codigo, $"Composição {c}", "m²");

            foreach (var item in codigos.OrderBy(_ => Sortear(sorteio, 0, int.MaxValue)).Take(Sortear(sorteio, 1, 6)))
            {
                baseDeCustos.AdicionarItem(codigo, item, Sortear(sorteio, 1, 50_000) / 1000m);
            }

            codigos.Add(codigo);
        }

        var orcamento = new Orcamento($"Orçamento {semente}", BdiQualquer(sorteio));
        var etapas = Sortear(sorteio, 1, 5);

        for (var e = 0; e < etapas; e++)
        {
            Preencher(orcamento, orcamento.AdicionarEtapa($"Etapa {e}").Valor, nivel: 1, sorteio, codigos);
        }

        return (baseDeCustos, orcamento);
    }

    private static void Preencher(Orcamento orcamento, string etapa, int nivel, Random sorteio, List<string> codigos)
    {
        var filhos = Sortear(sorteio, 0, 5);

        for (var f = 0; f < filhos; f++)
        {
            if (nivel < 3 && Sortear(sorteio, 0, 4) == 0)
            {
                Preencher(orcamento, orcamento.AdicionarEtapa($"Subetapa {f}", etapa).Valor, nivel + 1, sorteio, codigos);
                continue;
            }

            var item = orcamento.AdicionarItem(
                etapa,
                codigos[Sortear(sorteio, 0, codigos.Count)],
                Sortear(sorteio, 1, 1_000_000) / 100m);

            if (Sortear(sorteio, 0, 5) == 0)
            {
                orcamento.DefinirBdiDiferenciado(item.Valor, BdiQualquer(sorteio));
            }
        }
    }

    private static Bdi BdiQualquer(Random sorteio)
    {
        if (Sortear(sorteio, 0, 2) == 0)
        {
            return Bdi.Informado(Sortear(sorteio, 0, 4001) / 100m);
        }

        decimal Parcela(int maximoEmCentesimos) => Sortear(sorteio, 0, maximoEmCentesimos + 1) / 100m;

        return Bdi.PelaFormulaDoTcu(new ParcelasDoBdi(
            Parcela(800), Parcela(150), Parcela(300), Parcela(150), Parcela(200), Parcela(1200), Parcela(1500)));
    }

#pragma warning disable CA5394 // Aleatório não criptográfico de propósito: o que se quer é reprodutibilidade.
    private static int Sortear(Random sorteio, int de, int ateAntesDe) => sorteio.Next(de, ateAntesDe);
#pragma warning restore CA5394
}
