using System.Net;
using PlanejamentoObras.Api.Recursos;
using PlanejamentoObras.Api.Seguranca;

namespace PlanejamentoObras.Api.Testes;

/*
 * Duas construtoras na mesma API. A empresa vem do token; para a outra, o
 * cronograma não existe — 404, e não 403: "existe, mas não é seu" já seria
 * vazar (ADR-0013).
 */
public sealed class EmpresasTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _alfa = Guid.CreateVersion7();

    private readonly Guid _beta = Guid.CreateVersion7();

    [Fact]
    public async Task O_que_uma_empresa_cria_nao_existe_para_a_outra()
    {
        var daAlfa = api.Cliente(_alfa, Papeis.Planejador);
        var daBeta = api.Cliente(_beta, Papeis.Planejador);
        var cronograma = (await daAlfa.Criar("/api/cronogramas", RedeDoReadme.Cronograma())).Headers.Location!.ToString();

        var ler = await daBeta.GetAsync(cronograma, Cancelamento);
        var renomear = await daBeta.Enviar(HttpMethod.Patch, cronograma, new { nome = "Invadido" }, versao: 1);
        var remover = await daBeta.Enviar(HttpMethod.Delete, cronograma, versao: 1);
        var listaDaBeta = await (await daBeta.GetAsync("/api/cronogramas", Cancelamento)).Ler<List<ResumoResposta>>();

        Assert.All(new[] { ler, renomear, remover }, resposta => Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode));
        Assert.Empty(listaDaBeta);
        Assert.Equal(HttpStatusCode.OK, (await daAlfa.GetAsync(cronograma, Cancelamento)).StatusCode);
    }

    [Fact]
    public async Task A_mensagem_para_a_outra_empresa_e_a_mesma_de_um_id_que_nunca_existiu()
    {
        var cronograma = (await api.Cliente(_alfa, Papeis.Planejador).Criar("/api/cronogramas", new { nome = "Casa" })).Headers.Location!.ToString();
        var inventado = $"/api/cronogramas/{Guid.CreateVersion7()}";
        var daBeta = api.Cliente(_beta);

        var deOutraEmpresa = await (await daBeta.GetAsync(cronograma, Cancelamento)).Problema();
        var queNuncaExistiu = await (await daBeta.GetAsync(inventado, Cancelamento)).Problema();

        Assert.Equal($"Não existe o cronograma {cronograma.Split('/')[^1]}.", deOutraEmpresa.Detail);
        Assert.Equal($"Não existe o cronograma {inventado.Split('/')[^1]}.", queNuncaExistiu.Detail);
        Assert.Equal(queNuncaExistiu.Title, deOutraEmpresa.Title);
    }
}
