using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlanejamentoObras.Api.Http;
using PlanejamentoObras.Api.Seguranca;
using PlanejamentoObras.Dominio.Calendarios;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Api.Recursos.Cronogramas;

/// <summary>
/// Os cronogramas: a rede de atividades e vínculos, o caminho crítico e a
/// programação no calendário.
/// </summary>
/// <remarks>
/// Cada alteração é um pedido com o nome do que se faz — renomear,
/// adicionar uma atividade, remover um vínculo —, e não um PUT do
/// cronograma inteiro: é o domínio que confere cada passo, e a mensagem de
/// erro fala do passo que falhou. Toda alteração devolve o cronograma como
/// ficou, com a versão nova na ETag.
/// </remarks>
public sealed class RecursoDeCronogramas : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        var cronogramas = api.MapGroup("/cronogramas").WithTags("Cronogramas");

        cronogramas.MapGet("/", Listar).RequireAuthorization(Politicas.Consultar);
        cronogramas.MapPost("/", Criar).RequireAuthorization(Politicas.Planejar).ComIdempotencia();
        cronogramas.MapGet("/{id:guid}", Obter).RequireAuthorization(Politicas.Consultar);
        cronogramas.MapPatch("/{id:guid}", Renomear).RequireAuthorization(Politicas.Planejar);
        cronogramas.MapDelete("/{id:guid}", Remover).RequireAuthorization(Politicas.Planejar);

        cronogramas.MapPost("/{id:guid}/atividades", AdicionarAtividade).RequireAuthorization(Politicas.Planejar);
        cronogramas.MapPatch("/{id:guid}/atividades/{codigo}", AlterarAtividade).RequireAuthorization(Politicas.Planejar);
        cronogramas.MapDelete("/{id:guid}/atividades/{codigo}", RemoverAtividade).RequireAuthorization(Politicas.Planejar);

        cronogramas.MapPost("/{id:guid}/dependencias", AdicionarDependencia).RequireAuthorization(Politicas.Planejar);
        cronogramas
            .MapDelete("/{id:guid}/dependencias/{predecessora}/{sucessora}", RemoverDependencia)
            .RequireAuthorization(Politicas.Planejar);

        cronogramas.MapGet("/{id:guid}/caminho-critico", CaminhoCritico).RequireAuthorization(Politicas.Consultar);
        cronogramas.MapGet("/{id:guid}/programacao", Programacao).RequireAuthorization(Politicas.Consultar);
    }

    private static async Task<Ok<IReadOnlyList<ResumoResposta>>> Listar(RepositorioDeCronogramas repositorio, CancellationToken cancelamento)
    {
        var resumos = await repositorio.Listar(cancelamento);

        return TypedResults.Ok<IReadOnlyList<ResumoResposta>>([.. resumos.Select(ResumoResposta.De)]);
    }

    private static async Task<Created<CronogramaResposta>> Criar(
        NovoCronograma novo,
        RepositorioDeCronogramas repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var cronograma = new Cronograma(novo.Nome);

        foreach (var atividade in novo.Atividades ?? [])
        {
            cronograma.AdicionarAtividade(atividade.Codigo, atividade.Descricao, atividade.DuracaoEmDiasUteis);
        }

        foreach (var dependencia in novo.Dependencias ?? [])
        {
            cronograma.AdicionarDependencia(dependencia.Predecessora, dependencia.Sucessora, dependencia.Tipo, dependencia.DefasagemEmDiasUteis);
        }

        Etiquetas.Escrever(http, await repositorio.Adicionar(cronograma, cancelamento));

        return TypedResults.Created($"/api/cronogramas/{cronograma.Id}", CronogramaResposta.De(cronograma));
    }

    private static async Task<Results<Ok<CronogramaResposta>, StatusCodeHttpResult>> Obter(
        Guid id,
        RepositorioDeCronogramas repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lido = await Ler(repositorio, id, cancelamento);
        Etiquetas.Escrever(http, lido.Versao);

        return Etiquetas.ClienteJaTem(http, lido.Versao)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(CronogramaResposta.De(lido.Agregado));
    }

    private static Task<Ok<CronogramaResposta>> Renomear(
        Guid id,
        NovoNome novo,
        VersaoLida versao,
        RepositorioDeCronogramas repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, cronograma => cronograma.Renomear(novo.Nome), cancelamento);

    private static async Task<NoContent> Remover(Guid id, VersaoLida versao, RepositorioDeCronogramas repositorio, CancellationToken cancelamento)
    {
        await repositorio.Remover(new CronogramaId(id), versao.Valor, cancelamento);

        return TypedResults.NoContent();
    }

    private static Task<Ok<CronogramaResposta>> AdicionarAtividade(
        Guid id,
        NovaAtividade nova,
        VersaoLida versao,
        RepositorioDeCronogramas repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            cronograma => cronograma.AdicionarAtividade(nova.Codigo, nova.Descricao, nova.DuracaoEmDiasUteis),
            cancelamento);

    private static Task<Ok<CronogramaResposta>> AlterarAtividade(
        Guid id,
        string codigo,
        AlteracaoDeAtividade alteracao,
        VersaoLida versao,
        RepositorioDeCronogramas repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        if (alteracao is { Descricao: null, DuracaoEmDiasUteis: null })
        {
            throw new PedidoInvalido("Diga o que muda na atividade: descricao, duracaoEmDiasUteis, ou as duas.");
        }

        return Alterar(
            id,
            versao,
            repositorio,
            http,
            cronograma =>
            {
                ExigirAtividade(cronograma, codigo);

                if (alteracao.Descricao is { } descricao)
                {
                    cronograma.AlterarDescricaoDaAtividade(codigo, descricao);
                }

                if (alteracao.DuracaoEmDiasUteis is { } duracao)
                {
                    cronograma.AlterarDuracaoDaAtividade(codigo, duracao);
                }
            },
            cancelamento);
    }

    private static Task<Ok<CronogramaResposta>> RemoverAtividade(
        Guid id,
        string codigo,
        VersaoLida versao,
        RepositorioDeCronogramas repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            cronograma =>
            {
                ExigirAtividade(cronograma, codigo);
                cronograma.RemoverAtividade(codigo);
            },
            cancelamento);

    private static Task<Ok<CronogramaResposta>> AdicionarDependencia(
        Guid id,
        NovaDependencia nova,
        VersaoLida versao,
        RepositorioDeCronogramas repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            cronograma => cronograma.AdicionarDependencia(nova.Predecessora, nova.Sucessora, nova.Tipo, nova.DefasagemEmDiasUteis),
            cancelamento);

    private static Task<Ok<CronogramaResposta>> RemoverDependencia(
        Guid id,
        string predecessora,
        string sucessora,
        VersaoLida versao,
        RepositorioDeCronogramas repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            cronograma =>
            {
                var de = new CodigoDaAtividade(predecessora);
                var para = new CodigoDaAtividade(sucessora);

                if (!cronograma.Dependencias.Any(dependencia => dependencia.Predecessora == de && dependencia.Sucessora == para))
                {
                    throw new AgregadoNaoEncontrado($"Não existe vínculo de {de} para {para} no cronograma {cronograma.Id}.");
                }

                cronograma.RemoverDependencia(predecessora, sucessora);
            },
            cancelamento);

    private static async Task<Ok<CaminhoCriticoResposta>> CaminhoCritico(
        Guid id,
        RepositorioDeCronogramas repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lido = await Ler(repositorio, id, cancelamento);
        Etiquetas.Escrever(http, lido.Versao);

        return TypedResults.Ok(CaminhoCriticoResposta.De(CalculadoraDeCaminhoCritico.Analisar(lido.Agregado)));
    }

    /// <param name="inicio">O dia pedido para começar a obra; se não for útil, ela começa no próximo útil.</param>
    /// <param name="feriados">Os feriados, um parâmetro por data: <c>feriados=2026-10-12&amp;feriados=2026-11-02</c>.</param>
    /// <param name="sabadoUtil">Se a obra trabalha aos sábados.</param>
    private static async Task<Ok<ProgramacaoResposta>> Programacao(
        Guid id,
        DateOnly inicio,
        [FromQuery] DateOnly[]? feriados,
        RepositorioDeCronogramas repositorio,
        HttpContext http,
        CancellationToken cancelamento,
        bool sabadoUtil = false)
    {
        var lido = await Ler(repositorio, id, cancelamento);
        var calendario = sabadoUtil
            ? CalendarioDeTrabalho.DeSegundaASabado(feriados)
            : CalendarioDeTrabalho.DeSegundaASexta(feriados);

        Etiquetas.Escrever(http, lido.Versao);

        return TypedResults.Ok(ProgramacaoResposta.De(ProgramacaoDoCronograma.Programar(lido.Agregado, inicio, calendario)));
    }

    private static async Task<Versionado<Cronograma>> Ler(RepositorioDeCronogramas repositorio, Guid id, CancellationToken cancelamento) =>
        await repositorio.Obter(new CronogramaId(id), cancelamento)
        ?? throw new AgregadoNaoEncontrado($"Não existe o cronograma {id}.");

    /// <summary>Lê, confere a versão, aplica a alteração pelo domínio e grava — devolvendo o cronograma como ficou.</summary>
    private static async Task<Ok<CronogramaResposta>> Alterar(
        Guid id,
        VersaoLida versao,
        RepositorioDeCronogramas repositorio,
        HttpContext http,
        Action<Cronograma> alteracao,
        CancellationToken cancelamento)
    {
        var lido = await Ler(repositorio, id, cancelamento);
        Lidos.ConferirVersao(lido, versao, $"O cronograma {id}");

        alteracao(lido.Agregado);

        Etiquetas.Escrever(http, await repositorio.Atualizar(lido.Agregado, versao.Valor, cancelamento));

        return TypedResults.Ok(CronogramaResposta.De(lido.Agregado));
    }

    private static void ExigirAtividade(Cronograma cronograma, string codigo)
    {
        if (!cronograma.ContemAtividade(codigo))
        {
            throw new AgregadoNaoEncontrado($"Não existe a atividade {codigo} no cronograma {cronograma.Id}.");
        }
    }
}
