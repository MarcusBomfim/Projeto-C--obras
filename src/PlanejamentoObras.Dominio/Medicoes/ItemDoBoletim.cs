using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Medicoes;

/// <summary>
/// Um item no boletim: quantidades e valores — contratado, anterior, no mês e
/// acumulado.
/// </summary>
/// <param name="PrecoUnitario">O da linha de base: é a ele que se mede.</param>
public sealed record ItemDoBoletim(
    NumeroDaEap Numero,
    string Descricao,
    Dinheiro ValorContratado,
    Dinheiro ValorAnterior,
    Dinheiro ValorNoMes,
    Dinheiro ValorAcumulado,
    UnidadeDeMedida Unidade,
    Dinheiro PrecoUnitario,
    decimal QuantidadeContratada,
    decimal QuantidadeAnterior,
    decimal QuantidadeNoMes,
    decimal QuantidadeAcumulada)
    : LinhaDoBoletim(Numero, Descricao, ValorContratado, ValorAnterior, ValorNoMes, ValorAcumulado);
