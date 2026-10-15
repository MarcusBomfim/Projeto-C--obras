namespace PlanejamentoObras.Dominio.ValorAgregado;

/// <summary>Como a obra vai, pelos índices de desempenho de prazo e de custo.</summary>
public enum SituacaoDaObra
{
    /// <summary>Ainda não houve medição: não há o que dizer.</summary>
    SemMedicao,

    /// <summary>Prazo e custo dentro da faixa: os dois índices a partir de 0,95.</summary>
    EmDia,

    /// <summary>Um dos índices abaixo de 0,95 — a obra pede acompanhamento de perto.</summary>
    Atencao,

    /// <summary>Um dos índices abaixo de 0,85 — atraso ou estouro que já pede ação.</summary>
    Critica,
}

/// <summary>
/// A régua da situação: o pior dos dois índices decide. Uma obra com custo
/// em dia e prazo crítico está crítica.
/// </summary>
/// <remarks>
/// As faixas — 0,95 e 0,85 — são as de uso comum no acompanhamento de obra:
/// até 5% de desvio é ruído de medição; acima de 15%, a recuperação já
/// exige replanejamento.
/// </remarks>
public static class ClassificacaoDaObra
{
    public const decimal LimiteDeAtencao = 0.95m;

    public const decimal LimiteCritico = 0.85m;

    public static SituacaoDaObra Classificar(IndicadoresDeValorAgregado indicadores)
    {
        ArgumentNullException.ThrowIfNull(indicadores);

        // Índice sem base — nada planejado ou nada gasto até o mês — não
        // acusa nada: não há desvio a medir.
        var pior = new[] { indicadores.IndiceDeDesempenhoDePrazo, indicadores.IndiceDeDesempenhoDeCusto }
            .OfType<decimal>()
            .DefaultIfEmpty(1m)
            .Min();

        return pior < LimiteCritico ? SituacaoDaObra.Critica
            : pior < LimiteDeAtencao ? SituacaoDaObra.Atencao
            : SituacaoDaObra.EmDia;
    }
}
