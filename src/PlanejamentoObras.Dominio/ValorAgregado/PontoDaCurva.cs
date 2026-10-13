using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.ValorAgregado;

/// <summary>
/// Um mês das curvas S: valor planejado, valor agregado e custo real,
/// acumulados até o fim dele.
/// </summary>
/// <param name="ValorAgregado">Nulo depois da data de status: o futuro ainda não foi medido.</param>
/// <param name="CustoReal">Nulo depois da data de status.</param>
public sealed record PontoDaCurva(DateOnly Mes, Dinheiro ValorPlanejado, Dinheiro? ValorAgregado, Dinheiro? CustoReal);
