using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Cronogramas;

/// <summary>
/// O resultado do cálculo do caminho crítico, em dias úteis.
/// </summary>
/// <remarks>
/// Não sabe nada de calendário: tudo aqui é número de dia útil a partir do
/// início. Quem precisa de data usa <see cref="ProgramacaoDoCronograma"/>, que
/// pega esta análise e passa pelo calendário.
/// </remarks>
public sealed class AnaliseDoCaminhoCritico
{
    private readonly Dictionary<CodigoDaAtividade, TemposDaAtividade> _porCodigo;

    internal AnaliseDoCaminhoCritico(
        int duracaoEmDiasUteis,
        IReadOnlyList<TemposDaAtividade> atividades,
        IReadOnlyList<Dependencia> dependenciasCriticas)
    {
        DuracaoEmDiasUteis = duracaoEmDiasUteis;
        Atividades = atividades;
        DependenciasCriticas = dependenciasCriticas;
        AtividadesCriticas = [.. atividades.Where(tempos => tempos.EhCritica)];
        _porCodigo = atividades.ToDictionary(tempos => tempos.Codigo);
    }

    /// <summary>Quantos dias úteis a obra leva, do início ao término da última atividade.</summary>
    public int DuracaoEmDiasUteis { get; }

    /// <summary>Todas as atividades, ordenadas pelo início cedo — a ordem de um gráfico de Gantt.</summary>
    public IReadOnlyList<TemposDaAtividade> Atividades { get; }

    public IReadOnlyList<TemposDaAtividade> AtividadesCriticas { get; }

    /// <summary>
    /// Os vínculos que formam o caminho crítico: ligam duas atividades críticas
    /// e não têm folga entre elas.
    /// </summary>
    /// <remarks>
    /// Vínculos, e não "o caminho" como uma lista. Pode haver mais de um
    /// caminho crítico, e enumerar todos os caminhos de um grafo cresce de
    /// forma exponencial no pior caso. O conjunto de vínculos críticos é o que
    /// um Gantt precisa para destacar o caminho — e é linear.
    /// </remarks>
    public IReadOnlyList<Dependencia> DependenciasCriticas { get; }

    public TemposDaAtividade Da(string codigo)
    {
        var chave = new CodigoDaAtividade(codigo);

        return _porCodigo.TryGetValue(chave, out var tempos)
            ? tempos
            : throw new ExcecaoDeDominio($"A atividade {chave} não faz parte desta análise.");
    }
}
