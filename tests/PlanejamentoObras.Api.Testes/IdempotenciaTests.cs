using System.Net;
using PlanejamentoObras.Api.Recursos;
using PlanejamentoObras.Api.Seguranca;

namespace PlanejamentoObras.Api.Testes;

/*
 * Criar é seguro de repetir: a mesma chave de idempotência, com o mesmo
 * pedido, recebe a mesma resposta — e nada se cria duas vezes, nem com as
 * repetições chegando juntas (ADR-0014).
 */
public sealed class IdempotenciaTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _empresa = Guid.CreateVersion7();

    private HttpClient Planejador() => api.Cliente(_empresa, Papeis.Planejador);

    private Task<HttpResponseMessage> Criar(object corpo, string? chave) =>
        Planejador().Enviar(HttpMethod.Post, "/api/cronogramas", corpo, chave: chave);

    private async Task<int> QuantosCronogramas() =>
        (await (await Planejador().GetAsync("/api/cronogramas", Cancelamento)).Ler<List<ResumoResposta>>()).Count;

    [Fact]
    public async Task Criar_sem_chave_e_400()
    {
        var resposta = await Criar(new { nome = "Casa" }, chave: null);
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("Idempotency-Key", problema.Detail, StringComparison.Ordinal);
        Assert.Equal(0, await QuantosCronogramas());
    }

    [Fact]
    public async Task A_repeticao_recebe_a_mesma_resposta_e_nada_se_cria_de_novo()
    {
        var chave = Guid.NewGuid().ToString();

        var primeira = await Criar(RedeDoReadme.Cronograma(), chave);
        var repetida = await Criar(RedeDoReadme.Cronograma(), chave);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (primeira.StatusCode, repetida.StatusCode));
        Assert.Equal(primeira.Headers.Location, repetida.Headers.Location);
        Assert.Equal(primeira.Headers.ETag, repetida.Headers.ETag);
        Assert.Equal(await primeira.Content.ReadAsStringAsync(Cancelamento), await repetida.Content.ReadAsStringAsync(Cancelamento));
        Assert.False(primeira.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal("true", repetida.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.Equal(1, await QuantosCronogramas());
    }

    [Fact]
    public async Task A_repeticao_devolve_a_resposta_original_mesmo_depois_de_alterar()
    {
        var chave = Guid.NewGuid().ToString();
        var primeira = await Criar(new { nome = "Casa" }, chave);
        await Planejador().Enviar(HttpMethod.Patch, primeira.Headers.Location!.ToString(), new { nome = "Casa térrea" }, versao: 1);

        var repetida = await Criar(new { nome = "Casa" }, chave);

        Assert.Equal("\"1\"", repetida.Headers.ETag?.Tag);
        Assert.Contains("\"nome\":\"Casa\"", await repetida.Content.ReadAsStringAsync(Cancelamento), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_mesma_chave_com_outro_pedido_e_422()
    {
        var chave = Guid.NewGuid().ToString();
        await Criar(new { nome = "Casa" }, chave);

        var outro = await Criar(new { nome = "Galpão" }, chave);
        var problema = await outro.Problema();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, outro.StatusCode);
        Assert.EndsWith("#chave-reutilizada", problema.Type, StringComparison.Ordinal);
        Assert.Equal(1, await QuantosCronogramas());
    }

    [Fact]
    public async Task Repeticoes_simultaneas_criam_um_so()
    {
        var chave = Guid.NewGuid().ToString();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Criar(RedeDoReadme.Cronograma(), chave)));

        Assert.All(respostas, resposta => Assert.Equal(HttpStatusCode.Created, resposta.StatusCode));
        Assert.Single(respostas.Select(resposta => resposta.Headers.Location).Distinct());
        Assert.Equal(9, respostas.Count(resposta => resposta.Headers.Contains("Idempotent-Replayed")));
        Assert.Equal(1, await QuantosCronogramas());
    }

    [Fact]
    public async Task Pedido_recusado_nao_gasta_a_chave()
    {
        // A criação falhou, e a reserva da chave foi desfeita com ela: a
        // mesma chave serve para a tentativa corrigida.
        var chave = Guid.NewGuid().ToString();
        var comCiclo = new
        {
            nome = "Casa",
            atividades = new[] { new { codigo = "A", descricao = "A", duracaoEmDiasUteis = 1 } },
            dependencias = new[] { new { predecessora = "A", sucessora = "A" } },
        };

        var recusado = await Criar(comCiclo, chave);
        var corrigido = await Criar(new { nome = "Casa" }, chave);

        Assert.Equal((HttpStatusCode.UnprocessableEntity, HttpStatusCode.Created), (recusado.StatusCode, corrigido.StatusCode));
        Assert.False(corrigido.Headers.Contains("Idempotent-Replayed"));
    }

    [Fact]
    public async Task A_chave_de_uma_empresa_nao_vale_para_outra()
    {
        var chave = Guid.NewGuid().ToString();

        var daPrimeira = await Criar(new { nome = "Casa" }, chave);
        var daSegunda = await api.Cliente(Guid.CreateVersion7(), Papeis.Planejador)
            .Enviar(HttpMethod.Post, "/api/cronogramas", new { nome = "Casa" }, chave: chave);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (daPrimeira.StatusCode, daSegunda.StatusCode));
        Assert.NotEqual(daPrimeira.Headers.Location, daSegunda.Headers.Location);
    }
}
