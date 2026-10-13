namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// A classe de um item na curva ABC: onde está o dinheiro do orçamento.
/// </summary>
public enum ClasseAbc
{
    /// <summary>Os poucos itens que somam os primeiros 80 % do valor. É onde se negocia e se audita.</summary>
    A,

    /// <summary>Os que levam o acumulado de 80 % a 95 %.</summary>
    B,

    /// <summary>Os muitos itens dos últimos 5 %.</summary>
    C,
}
