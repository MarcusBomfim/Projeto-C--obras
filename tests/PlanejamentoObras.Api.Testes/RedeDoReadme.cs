namespace PlanejamentoObras.Api.Testes;

/// <summary>
/// A obra do exemplo do README, como um cliente da API a enviaria: os
/// números que o README mostra são os que a API tem de devolver.
/// </summary>
internal static class RedeDoReadme
{
    public const string Inicio = "2026-10-05";

    public const string Feriados = "feriados=2026-10-12&feriados=2026-11-02&feriados=2026-11-20&feriados=2026-12-25&feriados=2027-01-01";

    public static object Cronograma(string nome = "Residencial Vista Serra") => new
    {
        nome,
        atividades = new[]
        {
            new { codigo = "MOB", descricao = "Mobilização do canteiro", duracaoEmDiasUteis = 5 },
            new { codigo = "FUN", descricao = "Fundações", duracaoEmDiasUteis = 10 },
            new { codigo = "EST", descricao = "Estrutura", duracaoEmDiasUteis = 20 },
            new { codigo = "ALV", descricao = "Alvenaria", duracaoEmDiasUteis = 15 },
            new { codigo = "ELE", descricao = "Instalações elétricas", duracaoEmDiasUteis = 12 },
            new { codigo = "REB", descricao = "Reboco", duracaoEmDiasUteis = 8 },
            new { codigo = "PIN", descricao = "Pintura", duracaoEmDiasUteis = 6 },
            new { codigo = "ENT", descricao = "Entrega das chaves", duracaoEmDiasUteis = 0 },
        },
        dependencias = new[]
        {
            new { predecessora = "MOB", sucessora = "FUN" },
            new { predecessora = "FUN", sucessora = "EST" },
            new { predecessora = "EST", sucessora = "ALV" },
            new { predecessora = "EST", sucessora = "ELE" },
            new { predecessora = "ALV", sucessora = "REB" },
            new { predecessora = "ELE", sucessora = "REB" },
            new { predecessora = "REB", sucessora = "PIN" },
            new { predecessora = "PIN", sucessora = "ENT" },
        },
    };
}
