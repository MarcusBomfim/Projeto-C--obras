using PlanejamentoObras.Dominio.Custos;

namespace PlanejamentoObras.Dominio.Testes.Custos;

/// <summary>
/// Uma base pequena com uma composição auxiliar dentro de outra: a argamassa
/// dentro da alvenaria.
/// </summary>
/// <remarks>
/// Preços e coeficientes inventados, na ordem de grandeza de uma tabela de
/// 2026. Os custos esperados nos testes foram calculados à mão a partir
/// deles:
///
///   ARG  argamassa 1:2:8, m³
///        CIM  180     × 0,78   = 140,40
///        CAL  150     × 1,05   = 157,50
///        ARE    1,10  × 120,00 = 132,00
///        SER   10     × 21,46  = 214,60
///        BET    0,82  × 1,90   =   1,558 →   1,56
///                                         ──────
///                                         646,06
///
///   ALV  alvenaria de bloco cerâmico, m²
///        BLO   13,5   × 1,32   =  17,82
///        ARG    0,012 × 646,06 =   7,75272 → 7,75
///        PED    0,80  × 28,74  =  22,992   → 22,99
///        SER    0,40  × 21,46  =   8,584   →  8,58
///                                         ──────
///                                          57,14
/// </remarks>
internal static class BaseDeExemplo
{
    public static BaseDeCustos Criar()
    {
        var baseDeCustos = new BaseDeCustos("Tabela própria — setembro de 2026", new DateOnly(2026, 9, 1));

        baseDeCustos.AdicionarInsumo("CIM", "Cimento Portland CP II-32", "kg", TipoDeInsumo.Material, 0.78m);
        baseDeCustos.AdicionarInsumo("CAL", "Cal hidratada CH-III", "kg", TipoDeInsumo.Material, 1.05m);
        baseDeCustos.AdicionarInsumo("ARE", "Areia média lavada", "m³", TipoDeInsumo.Material, 120.00m);
        baseDeCustos.AdicionarInsumo("BLO", "Bloco cerâmico de vedação 9x19x19 cm", "un", TipoDeInsumo.Material, 1.32m);
        baseDeCustos.AdicionarInsumo("PED", "Pedreiro, com encargos sociais", "h", TipoDeInsumo.MaoDeObra, 28.74m);
        baseDeCustos.AdicionarInsumo("SER", "Servente, com encargos sociais", "h", TipoDeInsumo.MaoDeObra, 21.46m);
        baseDeCustos.AdicionarInsumo("BET", "Betoneira 400 L, custo horário produtivo", "h", TipoDeInsumo.Equipamento, 1.90m);

        baseDeCustos.AdicionarComposicao("ARG", "Argamassa de assentamento, traço 1:2:8, preparo mecânico", "m³");
        baseDeCustos.AdicionarItem("ARG", "CIM", 180m);
        baseDeCustos.AdicionarItem("ARG", "CAL", 150m);
        baseDeCustos.AdicionarItem("ARG", "ARE", 1.10m);
        baseDeCustos.AdicionarItem("ARG", "SER", 10m);
        baseDeCustos.AdicionarItem("ARG", "BET", 0.82m);

        baseDeCustos.AdicionarComposicao("ALV", "Alvenaria de vedação com bloco cerâmico 9x19x19 cm", "m²");
        baseDeCustos.AdicionarItem("ALV", "BLO", 13.5m);
        baseDeCustos.AdicionarItem("ALV", "ARG", 0.012m);
        baseDeCustos.AdicionarItem("ALV", "PED", 0.80m);
        baseDeCustos.AdicionarItem("ALV", "SER", 0.40m);

        return baseDeCustos;
    }
}
