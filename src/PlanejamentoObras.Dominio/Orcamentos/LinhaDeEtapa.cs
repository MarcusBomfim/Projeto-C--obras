using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// O subtotal de uma etapa: a soma das linhas logo abaixo dela.
/// </summary>
public sealed record LinhaDeEtapa(
    NumeroDaEap Numero,
    string Descricao,
    Dinheiro CustoTotal,
    Dinheiro Total,
    decimal Peso)
    : LinhaDoOrcamento(Numero, Descricao, CustoTotal, Total, Peso);
