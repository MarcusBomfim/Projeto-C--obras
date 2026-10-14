using PlanejamentoObras.Dominio.Cronogramas;

namespace PlanejamentoObras.Infraestrutura.Documentos;

/// <summary>
/// Como um cronograma é gravado: a rede, na ordem de cadastro — que decide
/// desempates no caminho crítico e a ordem do Gantt.
/// </summary>
internal sealed record DocumentoDoCronograma
{
    /// <summary>A versão deste formato. Mudou o formato, muda o número — e entra uma migração dos documentos.</summary>
    public const short Formato = 1;

    public required string Nome { get; init; }

    public required IReadOnlyList<AtividadeGravada> Atividades { get; init; }

    public required IReadOnlyList<DependenciaGravada> Dependencias { get; init; }

    public static DocumentoDoCronograma De(Cronograma cronograma) => new()
    {
        Nome = cronograma.Nome,
        Atividades =
        [
            .. cronograma.Atividades.Select(atividade => new AtividadeGravada
            {
                Codigo = atividade.Codigo.Valor,
                Descricao = atividade.Descricao,
                DuracaoEmDiasUteis = atividade.DuracaoEmDiasUteis,
            }),
        ],
        Dependencias =
        [
            .. cronograma.Dependencias.Select(dependencia => new DependenciaGravada
            {
                Predecessora = dependencia.Predecessora.Valor,
                Sucessora = dependencia.Sucessora.Valor,
                Tipo = dependencia.Tipo,
                DefasagemEmDiasUteis = dependencia.DefasagemEmDiasUteis,
            }),
        ],
    };

    public Cronograma ParaCronograma(CronogramaId id) =>
        Cronograma.Reconstituir(
            id,
            Nome,
            Atividades.Select(atividade => (atividade.Codigo, atividade.Descricao, atividade.DuracaoEmDiasUteis)),
            Dependencias.Select(dependencia => (dependencia.Predecessora, dependencia.Sucessora, dependencia.Tipo, dependencia.DefasagemEmDiasUteis)));
}

internal sealed record AtividadeGravada
{
    public required string Codigo { get; init; }

    public required string Descricao { get; init; }

    public required int DuracaoEmDiasUteis { get; init; }
}

internal sealed record DependenciaGravada
{
    public required string Predecessora { get; init; }

    public required string Sucessora { get; init; }

    public required TipoDeDependencia Tipo { get; init; }

    public required int DefasagemEmDiasUteis { get; init; }
}
