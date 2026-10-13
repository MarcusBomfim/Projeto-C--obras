namespace PlanejamentoObras.Dominio.FisicoFinanceiro;

/// <summary>
/// Que datas do cronograma distribuem o dinheiro no tempo.
/// </summary>
/// <remarks>
/// As duas curvas S — cedo e tarde — delimitam a faixa dentro da qual a obra
/// pode andar sem atrasar a entrega. Abaixo da curva tarde, já atrasou.
/// </remarks>
public enum DatasDoCronograma
{
    /// <summary>Cada atividade no início cedo: o ritmo mais rápido possível.</summary>
    Cedo,

    /// <summary>Cada atividade no início tarde: o mais devagar que ainda entrega no prazo.</summary>
    Tarde,
}
