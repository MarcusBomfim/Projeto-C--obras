namespace PlanejamentoObras.Api.Testes;

/// <summary>
/// A base de custos e o orçamento do exemplo do README, como um cliente da
/// API os enviaria. O cronograma está em <see cref="RedeDoReadme"/>.
/// </summary>
internal static class ObraDoReadme
{
    public static readonly string[] Feriados = ["2026-10-12", "2026-11-02", "2026-11-20", "2026-12-25", "2027-01-01"];

    public static object BaseDeCustos(string nome = "Tabela própria — setembro de 2026") => new
    {
        nome,
        dataBase = "2026-09-01",
        insumos = new[]
        {
            Insumo("CIM", "Cimento Portland CP II-32", "kg", "Material", 0.78m),
            Insumo("CAL", "Cal hidratada CH-III", "kg", "Material", 1.05m),
            Insumo("ARE", "Areia média lavada", "m³", "Material", 120.00m),
            Insumo("BLO", "Bloco cerâmico de vedação 9x19x19 cm", "un", "Material", 1.32m),
            Insumo("PED", "Pedreiro, com encargos sociais", "h", "MaoDeObra", 28.74m),
            Insumo("SER", "Servente, com encargos sociais", "h", "MaoDeObra", 21.46m),
            Insumo("BET", "Betoneira 400 L, custo horário produtivo", "h", "Equipamento", 1.90m),
            Insumo("FUND", "Fundações: estacas, blocos e vigas baldrame", "vb", "Material", 98_500m),
            Insumo("ESTR", "Estrutura de concreto armado", "vb", "Material", 412_000m),
            Insumo("ELET", "Instalações elétricas", "vb", "Material", 86_300m),
            Insumo("PINT", "Pintura interna e externa", "vb", "Material", 64_200m),
            Insumo("ELV", "Elevador de passageiros, 8 pessoas, fornecido e instalado", "un", "Equipamento", 180_000m),
        },
        composicoes = new[]
        {
            // A alvenaria vem antes da argamassa que ela usa: a ordem da lista não importa.
            Composicao("ALV", "Alvenaria de vedação com bloco cerâmico 9x19x19 cm", "m²", ("BLO", 13.5m), ("ARG", 0.012m), ("PED", 0.80m), ("SER", 0.40m)),
            Composicao("ARG", "Argamassa de assentamento, traço 1:2:8, preparo mecânico", "m³", ("CIM", 180m), ("CAL", 150m), ("ARE", 1.10m), ("SER", 10m), ("BET", 0.82m)),
            Composicao("LIMP", "Limpeza manual do terreno", "m²", ("SER", 0.15m)),
            Composicao("REB", "Emboço de parede, argamassa 1:2:8, 2 cm", "m²", ("ARG", 0.025m), ("PED", 0.60m), ("SER", 0.60m)),
        },
    };

    public static object Orcamento(string nome = "Residencial Vista Serra") => new
    {
        nome,
        bdi = new
        {
            parcelas = new
            {
                administracaoCentral = 4.00m,
                seguros = 0.40m,
                riscos = 1.27m,
                garantias = 0.40m,
                despesasFinanceiras = 1.23m,
                lucro = 7.40m,
                tributos = 6.15m,
            },
        },
        etapas = new[]
        {
            Etapa("Serviços preliminares", Item("LIMP", 1250m, "MOB")),
            Etapa("Infraestrutura e estrutura", Item("FUND", 1m, "FUN"), Item("ESTR", 1m, "EST")),
            Etapa("Vedações e revestimentos", Item("ALV", 2430.50m, "ALV"), Item("REB", 4861m, "REB")),
            Etapa("Instalações e acabamentos", Item("ELET", 1m, "ELE"), Item("PINT", 1m, "PIN")),
            Etapa("Equipamentos", Item("ELV", 2m, "ENT", bdi: 12m)),
        },
    };

    /// <summary>A linha de base do README, aprovada sobre o orçamento, a base e o cronograma dados.</summary>
    public static object LinhaDeBase(Guid orcamento, Guid baseDeCustos, Guid cronograma) => new
    {
        nome = "Linha de base 1 — contrato",
        aprovadaEm = "2026-09-28",
        orcamento,
        baseDeCustos,
        cronograma,
        inicio = RedeDoReadme.Inicio,
        feriados = Feriados,
    };

    public static object MedicaoDeOutubro => new
    {
        mes = "2026-10-30",
        lancamentos = new[]
        {
            new { item = "1.1", quantidade = 1250m, custoReal = 4_300m },
            new { item = "2.1", quantidade = 1m, custoReal = 104_900m },
            new { item = "2.2", quantidade = 0.15m, custoReal = 63_000m },
        },
    };

    public static object MedicaoDeNovembro => new
    {
        mes = "2026-11-30",
        lancamentos = new[]
        {
            new { item = "2.2", quantidade = 0.55m, custoReal = 232_000m },
            new { item = "3.1", quantidade = 300m, custoReal = 18_900m },
            new { item = "4.1", quantidade = 0.10m, custoReal = 9_600m },
        },
    };

    private static object Insumo(string codigo, string descricao, string unidade, string tipo, decimal preco) =>
        new { codigo, descricao, unidade, tipo, preco };

    private static object Composicao(string codigo, string descricao, string unidade, params (string Codigo, decimal Coeficiente)[] itens) =>
        new { codigo, descricao, unidade, itens = itens.Select(item => new { codigo = item.Codigo, coeficiente = item.Coeficiente }) };

    private static object Etapa(string titulo, params object[] filhos) => new { titulo, filhos };

    private static object Item(string codigo, decimal quantidade, string atividade, decimal? bdi = null) =>
        bdi is { } percentual
            ? new { tipo = "item", codigo, quantidade, atividade, bdiDiferenciado = (object?)new { percentual } }
            : new { tipo = "item", codigo, quantidade, atividade, bdiDiferenciado = (object?)null };
}
