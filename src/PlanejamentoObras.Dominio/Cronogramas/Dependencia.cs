using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Cronogramas;

/// <summary>
/// Um vínculo entre duas atividades do cronograma.
/// </summary>
/// <remarks>
/// <para>
/// Objeto de valor, imutável: mudar o tipo ou a defasagem de um vínculo é
/// remover o atual e criar outro. Assim a validação acontece num lugar só — a
/// criação — e não há vínculo que tenha sido válido um dia e deixado de ser.
/// </para>
/// <para>
/// O construtor é <c>internal</c> porque um vínculo isolado não sabe se fecha
/// ciclo. Quem sabe é o <see cref="Cronograma"/>, que enxerga a rede inteira —
/// então é só ele que cria.
/// </para>
/// </remarks>
public sealed record Dependencia
{
    /// <summary>Limite de sanidade para a defasagem, nos dois sentidos.</summary>
    public const int DefasagemMaximaEmDiasUteis = 5000;

    internal Dependencia(
        CodigoDaAtividade predecessora,
        CodigoDaAtividade sucessora,
        TipoDeDependencia tipo,
        int defasagemEmDiasUteis)
    {
        if (!Enum.IsDefined(tipo))
        {
            throw new ExcecaoDeDominio($"Tipo de dependência desconhecido: {(int)tipo}.");
        }

        if (Math.Abs(defasagemEmDiasUteis) > DefasagemMaximaEmDiasUteis)
        {
            throw new ExcecaoDeDominio(
                $"A defasagem entre {predecessora} e {sucessora} passa de {DefasagemMaximaEmDiasUteis} dias úteis. Confira o valor.");
        }

        Predecessora = predecessora;
        Sucessora = sucessora;
        Tipo = tipo;
        DefasagemEmDiasUteis = defasagemEmDiasUteis;
    }

    public CodigoDaAtividade Predecessora { get; }

    public CodigoDaAtividade Sucessora { get; }

    public TipoDeDependencia Tipo { get; }

    /// <summary>
    /// Espera, em dias úteis, entre os pontos amarrados. Positiva atrasa a
    /// sucessora — a cura do concreto antes de desformar. Negativa adianta —
    /// a antecipação, começar a pintura do primeiro pavimento antes de o
    /// reboco do último terminar.
    /// </summary>
    public int DefasagemEmDiasUteis { get; }

    /// <summary>"FUN → EST (TI+3)", como o engenheiro escreveria no quadro.</summary>
    public override string ToString()
    {
        var defasagem = DefasagemEmDiasUteis switch
        {
            0 => string.Empty,
            > 0 => "+" + DefasagemEmDiasUteis.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => DefasagemEmDiasUteis.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        return $"{Predecessora} → {Sucessora} ({Tipo.Sigla}{defasagem})";
    }
}
