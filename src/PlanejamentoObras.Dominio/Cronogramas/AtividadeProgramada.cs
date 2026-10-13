namespace PlanejamentoObras.Dominio.Cronogramas;

/// <summary>
/// Uma atividade com datas de calendário: o que vai para o Gantt.
/// </summary>
/// <remarks>
/// Aqui as datas são INCLUSIVAS, como o engenheiro lê: uma alvenaria de
/// segunda a sexta tem término na sexta. Os intervalos semiabertos ficam na
/// análise em dias úteis, que é onde fazem a conta fechar; na saída para gente,
/// vira a convenção de gente. Ver ADR-0003.
/// </remarks>
/// <param name="FolgaTotal">Em dias úteis.</param>
/// <param name="FolgaLivre">Em dias úteis.</param>
public sealed record AtividadeProgramada(
    CodigoDaAtividade Codigo,
    string Descricao,
    int DuracaoEmDiasUteis,
    DateOnly InicioCedo,
    DateOnly TerminoCedo,
    DateOnly InicioTarde,
    DateOnly TerminoTarde,
    int FolgaTotal,
    int FolgaLivre,
    bool EhCritica,
    bool EhMarco);
