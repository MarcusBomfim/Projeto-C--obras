namespace PlanejamentoObras.Dominio.Cronogramas;

/// <summary>
/// Como uma atividade se amarra a outra.
/// </summary>
/// <remarks>
/// As siglas são as do MS Project em português, que é o que o engenheiro de
/// planejamento já usa: TI, II, TT, IT.
/// </remarks>
public enum TipoDeDependencia
{
    /// <summary>
    /// TI — a sucessora só começa depois que a predecessora termina. O caso
    /// comum: o reboco depois da alvenaria.
    /// </summary>
    TerminoInicio,

    /// <summary>
    /// II — a sucessora só começa depois que a predecessora começa. Fôrma e
    /// armação avançando juntas, com alguns dias de distância.
    /// </summary>
    InicioInicio,

    /// <summary>
    /// TT — a sucessora só termina depois que a predecessora termina. A
    /// limpeza fina acompanha a pintura e só acaba quando ela acaba.
    /// </summary>
    TerminoTermino,

    /// <summary>
    /// IT — a sucessora só termina depois que a predecessora começa. Raro: o
    /// equipamento provisório só sai quando o definitivo começa a operar.
    /// </summary>
    InicioTermino,
}

/// <summary>
/// O que cada tipo significa para o cálculo do cronograma.
/// </summary>
/// <remarks>
/// <para>
/// Membros de extensão do C# 14: <c>tipo.Sigla</c> se lê como propriedade do
/// próprio enum, que não pode ter membros.
/// </para>
/// <para>
/// As duas propriedades booleanas são o que torna o cálculo do caminho crítico
/// uniforme. Todo vínculo diz "o ponto X da sucessora vem depois do ponto Y da
/// predecessora, mais a defasagem" — e X e Y são sempre um início ou um
/// término. Com isso, os quatro tipos viram uma fórmula só, e não quatro
/// ramos de <c>switch</c> repetidos na ida e na volta.
/// </para>
/// </remarks>
public static class TipoDeDependenciaExtensoes
{
    extension(TipoDeDependencia tipo)
    {
        public string Sigla => tipo switch
        {
            TipoDeDependencia.TerminoInicio => "TI",
            TipoDeDependencia.InicioInicio => "II",
            TipoDeDependencia.TerminoTermino => "TT",
            TipoDeDependencia.InicioTermino => "IT",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de dependência desconhecido."),
        };

        /// <summary>O vínculo parte do término da predecessora (TI e TT), e não do início.</summary>
        public bool AncoraNoTerminoDaPredecessora => tipo is TipoDeDependencia.TerminoInicio or TipoDeDependencia.TerminoTermino;

        /// <summary>O vínculo restringe o término da sucessora (TT e IT), e não o início.</summary>
        public bool RestringeOTerminoDaSucessora => tipo is TipoDeDependencia.TerminoTermino or TipoDeDependencia.InicioTermino;
    }
}
