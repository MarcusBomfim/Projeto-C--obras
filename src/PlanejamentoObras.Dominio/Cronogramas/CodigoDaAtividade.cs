using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Cronogramas;

/// <summary>
/// Identifica uma atividade dentro do cronograma: "FUN-010", "EST.02", "A1".
/// </summary>
/// <remarks>
/// <para>
/// É <c>record</c> de classe, e não <c>record struct</c>, de propósito. Todo
/// struct tem um valor padrão que não passa pelo construtor —
/// <c>default(CodigoDaAtividade)</c> seria um código com valor nulo, válido
/// para o compilador e impossível pelas regras. Como classe, a única forma de
/// existir um código é passando pela validação.
/// </para>
/// <para>
/// A propriedade é só de leitura, sem <c>init</c>: assim nem a expressão
/// <c>with</c> consegue produzir um código sem validar.
/// </para>
/// </remarks>
public sealed record CodigoDaAtividade
{
    public const int TamanhoMaximo = 20;

    public CodigoDaAtividade(string valor)
    {
        var limpo = (valor ?? string.Empty).Trim().ToUpperInvariant();

        if (limpo.Length == 0)
        {
            throw new ExcecaoDeDominio("O código da atividade é obrigatório.");
        }

        if (limpo.Length > TamanhoMaximo)
        {
            throw new ExcecaoDeDominio(
                $"O código da atividade tem no máximo {TamanhoMaximo} caracteres; \"{limpo}\" tem {limpo.Length}.");
        }

        if (!limpo.All(EhCaractereValido))
        {
            throw new ExcecaoDeDominio(
                $"O código \"{limpo}\" só pode ter letras sem acento, números, ponto, hífen e sublinhado.");
        }

        Valor = limpo;
    }

    /// <summary>O código normalizado: sem espaços nas pontas e em maiúsculas.</summary>
    public string Valor { get; }

    public override string ToString() => Valor;

    /*
     * Sem acento e sem espaço porque o código circula: vai para planilha,
     * para nome de arquivo, para a URL da API. "FUNDAÇÃO 1" quebra em cada um
     * desses lugares de um jeito diferente.
     */
    private static bool EhCaractereValido(char caractere) =>
        char.IsAsciiLetterOrDigit(caractere) || caractere is '.' or '-' or '_';
}
