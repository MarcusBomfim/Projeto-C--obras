using PlanejamentoObras.Dominio.Cronogramas;

namespace PlanejamentoObras.Api.Recursos.Cronogramas;

/*
 * O que entra e o que sai pela API, separado dos tipos do domínio: o
 * contrato HTTP não muda quando o domínio é refatorado, e o domínio não
 * ganha atributo de serialização. Os pedidos têm o nome do que pedem; as
 * respostas, o sufixo Resposta.
 */

/// <summary>Um cronograma novo: o nome e, se quiser, a rede inteira de uma vez.</summary>
public sealed record NovoCronograma(string Nome, IReadOnlyList<NovaAtividade>? Atividades = null, IReadOnlyList<NovaDependencia>? Dependencias = null);

public sealed record NovaAtividade(string Codigo, string Descricao, int DuracaoEmDiasUteis);

/// <summary>O que muda numa atividade; o que não vier fica como está.</summary>
public sealed record AlteracaoDeAtividade(string? Descricao = null, int? DuracaoEmDiasUteis = null);

/// <summary>Um vínculo novo: término-início e sem defasagem, se não disser outra coisa.</summary>
public sealed record NovaDependencia(
    string Predecessora,
    string Sucessora,
    TipoDeDependencia Tipo = TipoDeDependencia.TerminoInicio,
    int DefasagemEmDiasUteis = 0);

public sealed record CronogramaResposta(Guid Id, string Nome, IReadOnlyList<AtividadeResposta> Atividades, IReadOnlyList<DependenciaResposta> Dependencias)
{
    public static CronogramaResposta De(Cronograma cronograma) =>
        new(
            cronograma.Id.Valor,
            cronograma.Nome,
            [.. cronograma.Atividades.Select(atividade =>
                new AtividadeResposta(atividade.Codigo.Valor, atividade.Descricao, atividade.DuracaoEmDiasUteis, atividade.EhMarco))],
            [.. cronograma.Dependencias.Select(DependenciaResposta.De)]);
}

public sealed record AtividadeResposta(string Codigo, string Descricao, int DuracaoEmDiasUteis, bool EhMarco);

public sealed record DependenciaResposta(string Predecessora, string Sucessora, TipoDeDependencia Tipo, int DefasagemEmDiasUteis)
{
    public static DependenciaResposta De(Dependencia dependencia) =>
        new(dependencia.Predecessora.Valor, dependencia.Sucessora.Valor, dependencia.Tipo, dependencia.DefasagemEmDiasUteis);
}

/// <summary>O caminho crítico em dias úteis contados do início da obra: o dia 0 é o primeiro.</summary>
public sealed record CaminhoCriticoResposta(
    int DuracaoEmDiasUteis,
    IReadOnlyList<TemposDaAtividadeResposta> Atividades,
    IReadOnlyList<DependenciaResposta> DependenciasCriticas)
{
    public static CaminhoCriticoResposta De(AnaliseDoCaminhoCritico analise) =>
        new(
            analise.DuracaoEmDiasUteis,
            [.. analise.Atividades.Select(tempos => new TemposDaAtividadeResposta(
                tempos.Codigo.Valor,
                tempos.DuracaoEmDiasUteis,
                tempos.InicioCedo,
                tempos.TerminoCedo,
                tempos.InicioTarde,
                tempos.TerminoTarde,
                tempos.FolgaTotal,
                tempos.FolgaLivre,
                tempos.EhCritica,
                tempos.EhMarco))],
            [.. analise.DependenciasCriticas.Select(DependenciaResposta.De)]);
}

public sealed record TemposDaAtividadeResposta(
    string Codigo,
    int DuracaoEmDiasUteis,
    int InicioCedo,
    int TerminoCedo,
    int InicioTarde,
    int TerminoTarde,
    int FolgaTotal,
    int FolgaLivre,
    bool EhCritica,
    bool EhMarco);

/// <summary>O cronograma no calendário: as datas de cada atividade, cedo e tarde.</summary>
public sealed record ProgramacaoResposta(
    DateOnly DataDeInicio,
    DateOnly DataDeTermino,
    int DuracaoEmDiasUteis,
    IReadOnlyList<AtividadeProgramadaResposta> Atividades,
    IReadOnlyList<DependenciaResposta> DependenciasCriticas)
{
    public static ProgramacaoResposta De(CronogramaProgramado programado) =>
        new(
            programado.DataDeInicio,
            programado.DataDeTermino,
            programado.DuracaoEmDiasUteis,
            [.. programado.Atividades.Select(atividade => new AtividadeProgramadaResposta(
                atividade.Codigo.Valor,
                atividade.Descricao,
                atividade.DuracaoEmDiasUteis,
                atividade.InicioCedo,
                atividade.TerminoCedo,
                atividade.InicioTarde,
                atividade.TerminoTarde,
                atividade.FolgaTotal,
                atividade.FolgaLivre,
                atividade.EhCritica,
                atividade.EhMarco))],
            [.. programado.DependenciasCriticas.Select(DependenciaResposta.De)]);
}

public sealed record AtividadeProgramadaResposta(
    string Codigo,
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
