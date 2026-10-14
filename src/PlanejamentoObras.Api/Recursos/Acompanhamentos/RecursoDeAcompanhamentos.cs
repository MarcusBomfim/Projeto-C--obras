using Microsoft.AspNetCore.Http.HttpResults;
using PlanejamentoObras.Api.Http;
using PlanejamentoObras.Api.Seguranca;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.ValorAgregado;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Api.Recursos.Acompanhamentos;

/// <summary>
/// O acompanhamento da obra contra uma linha de base: as medições mês a
/// mês, o boletim de cada uma e o valor agregado (ADR-0008, ADR-0009).
/// </summary>
/// <remarks>
/// Registrar uma medição é alterar o acompanhamento — leva <c>If-Match</c>, e
/// a repetição de uma medição já registrada é recusada com 412 em vez de
/// medir duas vezes.
/// </remarks>
public sealed class RecursoDeAcompanhamentos : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        var acompanhamentos = api.MapGroup("/acompanhamentos").WithTags("Acompanhamentos");

        acompanhamentos.MapPost("/", Criar).RequireAuthorization(Politicas.Medir).ComIdempotencia();
        acompanhamentos.MapGet("/{id:guid}", Obter).RequireAuthorization(Politicas.Consultar);
        acompanhamentos.MapDelete("/{id:guid}", Remover).RequireAuthorization(Politicas.Medir);

        acompanhamentos.MapPost("/{id:guid}/medicoes", RegistrarMedicao).RequireAuthorization(Politicas.Medir);
        acompanhamentos.MapDelete("/{id:guid}/medicoes/{numero:int}", DesfazerMedicao).RequireAuthorization(Politicas.Medir);
        acompanhamentos.MapGet("/{id:guid}/boletins/{numero:int}", Boletim).RequireAuthorization(Politicas.Consultar);
        acompanhamentos.MapGet("/{id:guid}/valor-agregado", ValorAgregado).RequireAuthorization(Politicas.Consultar);

        api.MapGet("/linhas-de-base/{id:guid}/acompanhamentos", DaLinhaDeBase)
            .WithTags("Acompanhamentos")
            .RequireAuthorization(Politicas.Consultar);
    }

    private static async Task<Created<AcompanhamentoResposta>> Criar(
        NovoAcompanhamento novo,
        RepositorioDeAcompanhamentos acompanhamentos,
        RepositorioDeLinhasDeBase linhasDeBase,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var linhaDeBase = await Leituras.LinhaDeBase(linhasDeBase, novo.LinhaDeBase, cancelamento);
        var acompanhamento = new Acompanhamento(linhaDeBase.Agregado);

        Etiquetas.Escrever(http, await acompanhamentos.Adicionar(acompanhamento, cancelamento));

        return TypedResults.Created($"/api/acompanhamentos/{acompanhamento.Id}", AcompanhamentoResposta.De(acompanhamento));
    }

    private static async Task<Results<Ok<AcompanhamentoResposta>, StatusCodeHttpResult>> Obter(
        Guid id,
        RepositorioDeAcompanhamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lido = await Leituras.Acompanhamento(repositorio, id, cancelamento);
        Etiquetas.Escrever(http, lido.Versao);

        return Etiquetas.ClienteJaTem(http, lido.Versao)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(AcompanhamentoResposta.De(lido.Agregado));
    }

    private static async Task<NoContent> Remover(Guid id, VersaoLida versao, RepositorioDeAcompanhamentos repositorio, CancellationToken cancelamento)
    {
        await repositorio.Remover(new AcompanhamentoId(id), versao.Valor, cancelamento);

        return TypedResults.NoContent();
    }

    private static Task<Ok<AcompanhamentoResposta>> RegistrarMedicao(
        Guid id,
        NovaMedicao nova,
        VersaoLida versao,
        RepositorioDeAcompanhamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            acompanhamento => acompanhamento.RegistrarMedicao(
                nova.Mes,
                nova.Lancamentos.Select(lancamento => new LancamentoDeMedicao(lancamento.Item, lancamento.Quantidade, lancamento.CustoReal))),
            cancelamento);

    /// <summary>Desfaz a última medição — só ela: erro numa anterior se corrige com glosa na seguinte (ADR-0008).</summary>
    private static Task<Ok<AcompanhamentoResposta>> DesfazerMedicao(
        Guid id,
        int numero,
        VersaoLida versao,
        RepositorioDeAcompanhamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            acompanhamento =>
            {
                ExigirMedicao(acompanhamento, numero);

                if (numero != acompanhamento.Medicoes.Count)
                {
                    throw new ExcecaoDeDominio(
                        $"Só a última medição se desfaz — a {acompanhamento.Medicoes.Count}. Erro na medição {numero} se corrige com glosa na seguinte.");
                }

                acompanhamento.DesfazerUltimaMedicao();
            },
            cancelamento);

    private static async Task<Ok<BoletimResposta>> Boletim(Guid id, int numero, RepositorioDeAcompanhamentos repositorio, CancellationToken cancelamento)
    {
        var lido = await Leituras.Acompanhamento(repositorio, id, cancelamento);
        ExigirMedicao(lido.Agregado, numero);

        return TypedResults.Ok(BoletimResposta.De(EmissaoDoBoletim.Emitir(lido.Agregado, numero)));
    }

    /// <param name="mes">O mês da análise; sem ele, o da última medição.</param>
    private static async Task<Ok<ValorAgregadoResposta>> ValorAgregado(
        Guid id,
        DateOnly? mes,
        RepositorioDeAcompanhamentos repositorio,
        CancellationToken cancelamento)
    {
        var lido = await Leituras.Acompanhamento(repositorio, id, cancelamento);
        var analise = mes is { } doMes
            ? CalculadoraDeValorAgregado.Calcular(lido.Agregado, doMes)
            : CalculadoraDeValorAgregado.Calcular(lido.Agregado);

        return TypedResults.Ok(ValorAgregadoResposta.De(analise));
    }

    private static async Task<Ok<IReadOnlyList<AcompanhamentoDaLinhaDeBase>>> DaLinhaDeBase(
        Guid id,
        RepositorioDeAcompanhamentos acompanhamentos,
        RepositorioDeLinhasDeBase linhasDeBase,
        CancellationToken cancelamento)
    {
        await Leituras.LinhaDeBase(linhasDeBase, id, cancelamento);
        var listados = await acompanhamentos.ListarDaLinhaDeBase(new LinhaDeBaseId(id), cancelamento);

        return TypedResults.Ok<IReadOnlyList<AcompanhamentoDaLinhaDeBase>>(
            [.. listados.Select(versionado => new AcompanhamentoDaLinhaDeBase(versionado.Agregado.Valor, versionado.Versao))]);
    }

    private static void ExigirMedicao(Acompanhamento acompanhamento, int numero)
    {
        if (numero < 1 || numero > acompanhamento.Medicoes.Count)
        {
            throw new AgregadoNaoEncontrado($"Não existe a medição {numero} no acompanhamento {acompanhamento.Id}.");
        }
    }

    private static async Task<Ok<AcompanhamentoResposta>> Alterar(
        Guid id,
        VersaoLida versao,
        RepositorioDeAcompanhamentos repositorio,
        HttpContext http,
        Action<Acompanhamento> alteracao,
        CancellationToken cancelamento)
    {
        var lido = await Leituras.Acompanhamento(repositorio, id, cancelamento);
        Lidos.ConferirVersao(lido, versao, $"O acompanhamento {id}");

        alteracao(lido.Agregado);

        Etiquetas.Escrever(http, await repositorio.Atualizar(lido.Agregado, versao.Valor, cancelamento));

        return TypedResults.Ok(AcompanhamentoResposta.De(lido.Agregado));
    }
}
