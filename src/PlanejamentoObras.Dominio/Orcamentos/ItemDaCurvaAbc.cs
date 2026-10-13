using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// Um item na curva ABC: o valor dele, o peso, e o peso acumulado até ele.
/// </summary>
/// <param name="Peso">Quanto o item é do total: 0,25 quer dizer 25 %.</param>
/// <param name="PesoAcumulado">A soma dos pesos deste item e de todos os mais caros que ele.</param>
public sealed record ItemDaCurvaAbc(
    NumeroDaEap Numero,
    string Descricao,
    Dinheiro Total,
    decimal Peso,
    decimal PesoAcumulado,
    ClasseAbc Classe);
