using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.FisicoFinanceiro;

/// <summary>
/// Um mês do cronograma físico-financeiro: quanto se executa nele e quanto
/// já se executou até ele — um ponto da curva S.
/// </summary>
/// <param name="Numero">O mês da obra, a partir de 1.</param>
/// <param name="Mes">O primeiro dia do mês de calendário: 01/10/2026 é outubro de 2026.</param>
/// <param name="Percentual">Quanto o mês é do total da obra: 0,25 quer dizer 25 %.</param>
/// <param name="PercentualAcumulado">Quanto da obra está executado ao fim do mês.</param>
public sealed record PeriodoDoFisicoFinanceiro(
    int Numero,
    DateOnly Mes,
    Dinheiro Valor,
    decimal Percentual,
    Dinheiro Acumulado,
    decimal PercentualAcumulado);
