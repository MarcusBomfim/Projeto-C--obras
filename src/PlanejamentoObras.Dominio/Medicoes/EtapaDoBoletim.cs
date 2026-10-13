using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Medicoes;

/// <summary>O subtotal de uma etapa no boletim: a soma dos itens dentro dela.</summary>
public sealed record EtapaDoBoletim(
    NumeroDaEap Numero,
    string Descricao,
    Dinheiro ValorContratado,
    Dinheiro ValorAnterior,
    Dinheiro ValorNoMes,
    Dinheiro ValorAcumulado)
    : LinhaDoBoletim(Numero, Descricao, ValorContratado, ValorAnterior, ValorNoMes, ValorAcumulado);
