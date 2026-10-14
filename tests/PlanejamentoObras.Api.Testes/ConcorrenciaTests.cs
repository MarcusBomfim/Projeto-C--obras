using System.Net;
using System.Net.Http.Headers;
using PlanejamentoObras.Api.Recursos.Cronogramas;
using PlanejamentoObras.Api.Seguranca;

namespace PlanejamentoObras.Api.Testes;

/*
 * A versão que o banco confere (ADR-0012), pelo HTTP: sai na ETag de toda
 * leitura e volta no If-Match de toda alteração. Sem If-Match, 428; com uma
 * versão que já não é a atual, 412 — e o corpo do problema diz qual é a
 * atual (ADR-0015).
 */
public sealed class ConcorrenciaTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly HttpClient _planejador = api.Cliente(Guid.CreateVersion7(), Papeis.Planejador);

    private async Task<string> UmCronograma() =>
        (await _planejador.Criar("/api/cronogramas", new { nome = "Casa" })).Headers.Location!.ToString();

    private Task<HttpResponseMessage> Renomear(string cronograma, string nome, int? versao) =>
        _planejador.Enviar(HttpMethod.Patch, cronograma, new { nome }, versao);

    [Fact]
    public async Task A_versao_sai_na_ETag_de_toda_leitura_e_sobe_a_cada_alteracao()
    {
        var criado = await _planejador.Criar("/api/cronogramas", new { nome = "Casa" });
        var cronograma = criado.Headers.Location!.ToString();
        var lido = await _planejador.GetAsync(cronograma, Cancelamento);
        var renomeado = await Renomear(cronograma, "Casa térrea", versao: 1);

        Assert.Equal("\"1\"", criado.Headers.ETag?.Tag);
        Assert.False(criado.Headers.ETag?.IsWeak);
        Assert.Equal("\"1\"", lido.Headers.ETag?.Tag);
        Assert.Equal("\"2\"", renomeado.Headers.ETag?.Tag);
        Assert.Equal("Casa térrea", (await renomeado.Ler<CronogramaResposta>()).Nome);
    }

    [Fact]
    public async Task Alteracao_sem_If_Match_e_428()
    {
        var cronograma = await UmCronograma();

        var resposta = await Renomear(cronograma, "Casa térrea", versao: null);
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.PreconditionRequired, resposta.StatusCode);
        Assert.EndsWith("#versao-ausente", problema.Type, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("*", HttpStatusCode.PreconditionRequired)]
    [InlineData("1", HttpStatusCode.BadRequest)]
    [InlineData("W/\"1\"", HttpStatusCode.BadRequest)]
    [InlineData("\"1\", \"2\"", HttpStatusCode.BadRequest)]
    [InlineData("\"zero\"", HttpStatusCode.BadRequest)]
    public async Task If_Match_que_nao_e_uma_versao_so_e_recusado(string ifMatch, HttpStatusCode esperado)
    {
        var cronograma = await UmCronograma();

        using var pedido = new HttpRequestMessage(HttpMethod.Patch, cronograma) { Content = System.Net.Http.Json.JsonContent.Create(new { nome = "Casa térrea" }) };
        pedido.Headers.TryAddWithoutValidation("If-Match", ifMatch);

        var resposta = await _planejador.SendAsync(pedido, Cancelamento);

        Assert.Equal(esperado, resposta.StatusCode);
    }

    [Fact]
    public async Task Versao_que_ja_nao_e_a_atual_e_412_com_a_versao_atual()
    {
        var cronograma = await UmCronograma();
        await Renomear(cronograma, "Casa térrea", versao: 1);

        var atrasado = await Renomear(cronograma, "Casa de praia", versao: 1);
        var problema = await atrasado.Problema();

        Assert.Equal(HttpStatusCode.PreconditionFailed, atrasado.StatusCode);
        Assert.Equal("2", problema.Extensions["versaoAtual"]?.ToString());
        Assert.Null(atrasado.Headers.ETag);
        Assert.True(atrasado.Headers.CacheControl?.NoStore);
        Assert.Contains("mudou desde a versão 1: está na versão 2", problema.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Versao_velha_e_412_antes_de_qualquer_outro_erro()
    {
        // Na versão 1 a atividade existia; na 2, não. Quem leu a 1 fica
        // sabendo que o cronograma mudou — e não recebe um 404 sobre um
        // estado que nunca viu.
        var cronograma = (await _planejador.Criar("/api/cronogramas", RedeDoReadme.Cronograma())).Headers.Location!.ToString();
        await _planejador.Enviar(HttpMethod.Delete, $"{cronograma}/atividades/ENT", versao: 1);

        var resposta = await _planejador.Enviar(HttpMethod.Patch, $"{cronograma}/atividades/ENT", new { duracaoEmDiasUteis = 1 }, versao: 1);

        Assert.Equal(HttpStatusCode.PreconditionFailed, resposta.StatusCode);
    }

    [Fact]
    public async Task Remover_com_versao_velha_e_412_e_o_cronograma_fica()
    {
        var cronograma = await UmCronograma();
        await Renomear(cronograma, "Casa térrea", versao: 1);

        var remover = await _planejador.Enviar(HttpMethod.Delete, cronograma, versao: 1);

        Assert.Equal(HttpStatusCode.PreconditionFailed, remover.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _planejador.GetAsync(cronograma, Cancelamento)).StatusCode);
    }

    [Fact]
    public async Task De_alteracoes_simultaneas_sobre_a_mesma_versao_so_uma_passa()
    {
        var cronograma = await UmCronograma();

        var respostas = await Task.WhenAll(Enumerable.Range(1, 10).Select(n => Renomear(cronograma, $"Casa {n}", versao: 1)));

        Assert.Equal(1, respostas.Count(resposta => resposta.StatusCode == HttpStatusCode.OK));
        Assert.Equal(9, respostas.Count(resposta => resposta.StatusCode == HttpStatusCode.PreconditionFailed));
    }

    [Fact]
    public async Task Quem_ja_tem_a_versao_atual_recebe_304()
    {
        var cronograma = await UmCronograma();

        using var condicional = new HttpRequestMessage(HttpMethod.Get, cronograma);
        condicional.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"1\""));
        var naoMudou = await _planejador.SendAsync(condicional, Cancelamento);

        await Renomear(cronograma, "Casa térrea", versao: 1);

        using var depois = new HttpRequestMessage(HttpMethod.Get, cronograma);
        depois.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"1\""));
        var mudou = await _planejador.SendAsync(depois, Cancelamento);

        Assert.Equal(HttpStatusCode.NotModified, naoMudou.StatusCode);
        Assert.Empty(await naoMudou.Content.ReadAsByteArrayAsync(Cancelamento));
        Assert.Equal((HttpStatusCode.OK, "\"2\""), (mudou.StatusCode, mudou.Headers.ETag?.Tag));
    }
}
