using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Medicoes;

/// <summary>
/// Emite o boletim de uma medição.
/// </summary>
/// <remarks>
/// <para>
/// O valor acumulado de um item é quantidade acumulada × preço unitário,
/// arredondado ao centavo; o do mês é a diferença entre o acumulado e o
/// anterior. Calcular o do mês direto — quantidade do mês × preço — e somar
/// os meses perderia ou criaria centavos: o item medido em 0,5 + 0,5 de um
/// preço de R$ 0,15 daria 0,08 + 0,08 = 0,16, mais que os 0,15 contratados.
/// Pela diferença, dá 0,08 + 0,07, e o item todo medido vale exatamente o
/// contratado (ADR-0008).
/// </para>
/// <para>
/// Etapas e o total do boletim são somas de valores já em centavos.
/// </para>
/// </remarks>
public static class EmissaoDoBoletim
{
    public static BoletimDeMedicao Emitir(Acompanhamento acompanhamento, int medicao)
    {
        ArgumentNullException.ThrowIfNull(acompanhamento);

        var registrada = acompanhamento.Medicao(medicao);
        var linhaDeBase = acompanhamento.LinhaDeBase;
        var antes = acompanhamento.QuantidadesAcumuladas(medicao - 1);
        var depois = acompanhamento.QuantidadesAcumuladas(medicao);

        var somas = linhaDeBase.Linhas.ToDictionary(linha => linha.Numero, _ => (Anterior: Dinheiro.Zero, Acumulado: Dinheiro.Zero));
        var itens = new Dictionary<NumeroDaEap, ItemDoBoletim>();

        foreach (var item in linhaDeBase.Itens)
        {
            var quantidadeAnterior = antes.GetValueOrDefault(item.Numero);
            var quantidadeAcumulada = depois.GetValueOrDefault(item.Numero);
            var valorAnterior = (item.PrecoUnitario * quantidadeAnterior).EmCentavos();
            var valorAcumulado = (item.PrecoUnitario * quantidadeAcumulada).EmCentavos();

            foreach (var etapa in item.Numero.Ancestrais())
            {
                var (anterior, acumulado) = somas[etapa];
                somas[etapa] = (anterior + valorAnterior, acumulado + valorAcumulado);
            }

            itens[item.Numero] = new ItemDoBoletim(
                item.Numero,
                item.Descricao,
                item.Total,
                valorAnterior,
                valorAcumulado - valorAnterior,
                valorAcumulado,
                item.Unidade,
                item.PrecoUnitario,
                item.Quantidade,
                quantidadeAnterior,
                quantidadeAcumulada - quantidadeAnterior,
                quantidadeAcumulada);
        }

        var linhas = linhaDeBase.Linhas.Select(linha => linha is ItemPlanejado
            ? (LinhaDoBoletim)itens[linha.Numero]
            : new EtapaDoBoletim(
                linha.Numero,
                linha.Descricao,
                linha.Total,
                somas[linha.Numero].Anterior,
                somas[linha.Numero].Acumulado - somas[linha.Numero].Anterior,
                somas[linha.Numero].Acumulado));

        return new BoletimDeMedicao(registrada, linhas);
    }
}
