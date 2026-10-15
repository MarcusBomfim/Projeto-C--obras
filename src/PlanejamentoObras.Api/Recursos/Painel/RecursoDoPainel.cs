using Microsoft.AspNetCore.Http.HttpResults;
using PlanejamentoObras.Api.Seguranca;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Painel;

namespace PlanejamentoObras.Api.Recursos.Painel;

/// <summary>
/// O painel das obras: a situação de cada obra acompanhada da empresa, na
/// última medição — pronta, sem abrir documento nem calcular nada (ADR-0017).
/// </summary>
/// <remarks>
/// O painel é atualizado em segundo plano, pelo processador. Logo depois de
/// uma medição, ele ainda pode mostrar a anterior, e um acompanhamento que
/// acabou de ser criado pode ainda não estar nele. Cada linha diz de que
/// versão do acompanhamento veio — a mesma da <c>ETag</c> dele — e quando foi
/// atualizada.
/// </remarks>
public sealed class RecursoDoPainel : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        var painel = api.MapGroup("/painel").WithTags("Painel");

        painel.MapGet("/", Listar).RequireAuthorization(Politicas.Consultar);
        painel.MapGet("/{acompanhamento:guid}", Obter).RequireAuthorization(Politicas.Consultar);
    }

    /// <summary>As obras da empresa, em ordem de nome.</summary>
    private static async Task<Ok<IReadOnlyList<ObraNoPainelResposta>>> Listar(ConsultaDoPainel consulta, CancellationToken cancelamento) =>
        TypedResults.Ok<IReadOnlyList<ObraNoPainelResposta>>([.. (await consulta.Listar(cancelamento)).Select(ObraNoPainelResposta.De)]);

    private static async Task<Ok<ObraNoPainelResposta>> Obter(Guid acompanhamento, ConsultaDoPainel consulta, CancellationToken cancelamento)
    {
        var linha = await consulta.Obter(acompanhamento, cancelamento)
            ?? throw new AgregadoNaoEncontrado(
                $"O acompanhamento {acompanhamento} não está no painel. Se acabou de ser criado, o painel ainda não chegou nele.");

        return TypedResults.Ok(ObraNoPainelResposta.De(linha));
    }
}
