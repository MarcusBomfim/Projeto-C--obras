using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Cronogramas;

/// <summary>
/// O cronograma com datas: início, término, folgas e caminho crítico.
/// </summary>
/// <remarks>
/// É uma fotografia. Mudou a rede ou o calendário, calcula-se outra — este
/// objeto não se atualiza sozinho, e não deveria: é derivado, não estado.
/// </remarks>
public sealed class CronogramaProgramado
{
    private readonly Dictionary<CodigoDaAtividade, AtividadeProgramada> _porCodigo;

    internal CronogramaProgramado(
        string nome,
        DateOnly dataDeInicio,
        DateOnly dataDeTermino,
        int duracaoEmDiasUteis,
        IReadOnlyList<AtividadeProgramada> atividades,
        IReadOnlyList<Dependencia> dependenciasCriticas)
    {
        Nome = nome;
        DataDeInicio = dataDeInicio;
        DataDeTermino = dataDeTermino;
        DuracaoEmDiasUteis = duracaoEmDiasUteis;
        Atividades = atividades;
        DependenciasCriticas = dependenciasCriticas;
        AtividadesCriticas = [.. atividades.Where(atividade => atividade.EhCritica)];
        _porCodigo = atividades.ToDictionary(atividade => atividade.Codigo);
    }

    public string Nome { get; }

    /// <summary>O primeiro dia útil da obra — que não é necessariamente a data pedida, se ela cair num fim de semana.</summary>
    public DateOnly DataDeInicio { get; }

    /// <summary>O último dia útil da obra. Nenhuma data do cronograma passa dele.</summary>
    public DateOnly DataDeTermino { get; }

    public int DuracaoEmDiasUteis { get; }

    /// <summary>Ordenadas pelo início cedo, desempatando pela ordem de cadastro.</summary>
    public IReadOnlyList<AtividadeProgramada> Atividades { get; }

    public IReadOnlyList<AtividadeProgramada> AtividadesCriticas { get; }

    public IReadOnlyList<Dependencia> DependenciasCriticas { get; }

    public AtividadeProgramada Da(string codigo)
    {
        var chave = new CodigoDaAtividade(codigo);

        return _porCodigo.TryGetValue(chave, out var atividade)
            ? atividade
            : throw new ExcecaoDeDominio($"A atividade {chave} não faz parte deste cronograma.");
    }
}
