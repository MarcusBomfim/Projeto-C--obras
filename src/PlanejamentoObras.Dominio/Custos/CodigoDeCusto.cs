using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Custos;

/// <summary>
/// Identifica um insumo ou uma composição numa base de custos: "00007271",
/// "ALV-01", "ARG.1-2-8".
/// </summary>
/// <remarks>
/// <para>
/// Insumos e composições dividem o mesmo espaço de códigos dentro de uma base:
/// um código é de um ou de outro, nunca dos dois. Assim, quem monta uma
/// composição diz só "usa ARG-01", sem precisar dizer se ARG-01 é insumo ou
/// composição — e não existe código ambíguo.
/// </para>
/// <para>
/// Mesmas regras e mesmos motivos do código da atividade: classe, e não
/// struct, para o <c>default</c> não criar um código inválido; sem acento nem
/// espaço, porque o código circula por planilha, arquivo e URL.
/// </para>
/// </remarks>
public sealed record CodigoDeCusto
{
    public const int TamanhoMaximo = 20;

    public CodigoDeCusto(string valor)
    {
        var limpo = (valor ?? string.Empty).Trim().ToUpperInvariant();

        if (limpo.Length == 0)
        {
            throw new ExcecaoDeDominio("O código do insumo ou da composição é obrigatório.");
        }

        if (limpo.Length > TamanhoMaximo)
        {
            throw new ExcecaoDeDominio(
                $"O código do insumo ou da composição tem no máximo {TamanhoMaximo} caracteres; \"{limpo}\" tem {limpo.Length}.");
        }

        if (!limpo.All(caractere => char.IsAsciiLetterOrDigit(caractere) || caractere is '.' or '-' or '_'))
        {
            throw new ExcecaoDeDominio(
                $"O código \"{limpo}\" só pode ter letras sem acento, números, ponto, hífen e sublinhado.");
        }

        Valor = limpo;
    }

    /// <summary>O código normalizado: sem espaços nas pontas e em maiúsculas.</summary>
    public string Valor { get; }

    public override string ToString() => Valor;
}
