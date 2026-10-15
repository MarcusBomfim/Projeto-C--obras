using System.Net;
using PlanejamentoObras.Api.Recursos.Painel;

namespace PlanejamentoObras.Api.Testes;

/// <summary>
/// Os passos que um teste espera que deem certo — e que, se não derem,
/// falham dizendo o que a API respondeu, em vez de quebrar mais adiante.
/// </summary>
internal static class Conferencias
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    /// <summary>O pedido deu certo; senão, o teste falha com o status e o corpo da resposta.</summary>
    public static async Task<HttpResponseMessage> ComSucesso(this Task<HttpResponseMessage> pedido)
    {
        var resposta = await pedido;

        Assert.True(
            resposta.IsSuccessStatusCode,
            $"{resposta.RequestMessage?.Method} {resposta.RequestMessage?.RequestUri}: {(int)resposta.StatusCode} {await resposta.Content.ReadAsStringAsync(Cancelamento)}");

        return resposta;
    }

    /// <summary>O id do que a criação criou, tirado do <c>Location</c>.</summary>
    public static async Task<Guid> IdCriado(this Task<HttpResponseMessage> criacao)
    {
        var resposta = await criacao.ComSucesso();

        return Guid.Parse(resposta.Headers.Location!.ToString().Split('/')[^1]);
    }

    public static async Task<T> LerComSucesso<T>(this HttpClient cliente, string caminho) =>
        await (await cliente.GetAsync(caminho, Cancelamento).ComSucesso()).Ler<T>();

    /// <summary>A linha da obra no painel — ou nada, se o processador ainda não chegou nela.</summary>
    public static async Task<ObraNoPainelResposta?> LerDoPainel(this HttpClient cliente, Guid acompanhamento)
    {
        var pedido = cliente.GetAsync($"/api/painel/{acompanhamento}", Cancelamento);

        return (await pedido).StatusCode == HttpStatusCode.NotFound
            ? null
            : await (await pedido.ComSucesso()).Ler<ObraNoPainelResposta>();
    }
}
