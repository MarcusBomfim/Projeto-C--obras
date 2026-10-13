namespace PlanejamentoObras.Dominio.Cronogramas;

/// <summary>
/// Os tempos de uma atividade na rede, em dias úteis contados a partir do
/// início da obra.
/// </summary>
/// <remarks>
/// <para>
/// Intervalos semiabertos: uma atividade que vai de 5 a 15 ocupa os dias
/// úteis 5, 6, …, 14 — o término é o primeiro dia em que ela já não está
/// mais acontecendo. É o que faz a soma fechar sem "mais um" e "menos um"
/// espalhados pelo código: término = início + duração, sempre. Ver ADR-0003.
/// </para>
/// <para>
/// O cedo vem da ida pela rede; o tarde, da volta. A distância entre os dois é
/// a folga total — quanto a atividade pode escorregar sem atrasar a obra.
/// </para>
/// </remarks>
/// <param name="FolgaTotal">Quanto pode atrasar sem atrasar a obra.</param>
/// <param name="FolgaLivre">Quanto pode atrasar sem atrasar nenhuma sucessora.</param>
/// <param name="MarcoDeTermino">
/// Para marcos: verdadeiro quando o que posiciona o marco é o término de algo
/// (a conclusão da pintura), e não o início de algo ou o começo da obra.
/// Decide se ele aparece no último dia do que conclui ou no primeiro dia do
/// que inicia — ver ADR-0003.
/// </param>
public sealed record TemposDaAtividade(
    CodigoDaAtividade Codigo,
    int DuracaoEmDiasUteis,
    int InicioCedo,
    int TerminoCedo,
    int InicioTarde,
    int TerminoTarde,
    int FolgaTotal,
    int FolgaLivre,
    bool MarcoDeTermino)
{
    /// <summary>Atrasar uma atividade crítica atrasa a obra, dia por dia.</summary>
    public bool EhCritica => FolgaTotal == 0;

    public bool EhMarco => DuracaoEmDiasUteis == 0;
}
