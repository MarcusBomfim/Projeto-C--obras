namespace PlanejamentoObras.Dominio.Custos;

/// <summary>
/// A natureza de um insumo — a mesma classificação das bases de preços
/// públicas.
/// </summary>
public enum TipoDeInsumo
{
    Material,

    /// <summary>Hora de trabalho, com os encargos sociais já no preço.</summary>
    MaoDeObra,

    /// <summary>Hora de máquina: betoneira, guindaste, retroescavadeira.</summary>
    Equipamento,
}
