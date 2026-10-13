using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Medicoes;

/// <summary>
/// Uma linha do boletim de medição — uma <see cref="EtapaDoBoletim"/> ou um
/// <see cref="ItemDoBoletim"/>: o contratado, o medido até o mês anterior, o
/// medido no mês e o acumulado.
/// </summary>
/// <param name="ValorContratado">O total da linha na linha de base.</param>
/// <param name="ValorNoMes">O que se mede — e se paga — no mês.</param>
public abstract record LinhaDoBoletim(
    NumeroDaEap Numero,
    string Descricao,
    Dinheiro ValorContratado,
    Dinheiro ValorAnterior,
    Dinheiro ValorNoMes,
    Dinheiro ValorAcumulado)
{
    public int Nivel => Numero.Nivel;

    /// <summary>O que falta medir.</summary>
    public Dinheiro Saldo => ValorContratado - ValorAcumulado;

    /// <summary>Quanto do contratado já foi medido, pelo valor: 0,6 quer dizer 60 %.</summary>
    public decimal PercentualExecutado => ValorContratado == Dinheiro.Zero ? 0m : ValorAcumulado / ValorContratado;
}
