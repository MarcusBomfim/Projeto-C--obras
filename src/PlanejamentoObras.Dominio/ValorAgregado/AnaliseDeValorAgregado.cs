using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.ValorAgregado;

/// <summary>
/// A obra numa data de status: os indicadores de valor agregado da obra e de
/// cada linha, o prazo agregado e as curvas S.
/// </summary>
public sealed class AnaliseDeValorAgregado
{
    private readonly Dictionary<NumeroDaEap, LinhaDoValorAgregado> _porNumero;

    internal AnaliseDeValorAgregado(
        int medicao,
        DateOnly mes,
        IndicadoresDeValorAgregado obra,
        IEnumerable<LinhaDoValorAgregado> linhas,
        PrazoAgregado prazo,
        IEnumerable<PontoDaCurva> curva)
    {
        Medicao = medicao;
        Mes = mes;
        Obra = obra;
        Linhas = Array.AsReadOnly<LinhaDoValorAgregado>([.. linhas]);
        Prazo = prazo;
        Curva = Array.AsReadOnly<PontoDaCurva>([.. curva]);
        _porNumero = Linhas.ToDictionary(linha => linha.Numero);
    }

    /// <summary>O número da medição da data de status.</summary>
    public int Medicao { get; }

    /// <summary>A data de status: o mês da medição.</summary>
    public DateOnly Mes { get; }

    public IndicadoresDeValorAgregado Obra { get; }

    /// <summary>Etapas e itens, na ordem da planilha.</summary>
    public IReadOnlyList<LinhaDoValorAgregado> Linhas { get; }

    public PrazoAgregado Prazo { get; }

    /// <summary>
    /// Mês a mês, do primeiro ao último da linha de base — ou até a data de
    /// status, se ela vier depois.
    /// </summary>
    public IReadOnlyList<PontoDaCurva> Curva { get; }

    public LinhaDoValorAgregado Linha(string numero)
    {
        var chave = NumeroDaEap.Ler(numero);

        return _porNumero.TryGetValue(chave, out var linha)
            ? linha
            : throw new ExcecaoDeDominio($"Não existe a linha {chave} nesta análise.");
    }
}
