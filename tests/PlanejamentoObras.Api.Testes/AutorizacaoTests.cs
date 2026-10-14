using System.Net;
using PlanejamentoObras.Api.Seguranca;

namespace PlanejamentoObras.Api.Testes;

/*
 * Quem pode o quê. Ler, qualquer pessoa da empresa; alterar o cronograma,
 * só quem planeja — os outros papéis recebem 403, mesmo sendo da empresa
 * (ADR-0015).
 */
public sealed class AutorizacaoTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _empresa = Guid.CreateVersion7();

    private async Task<string> UmCronograma()
    {
        var criado = await api.Cliente(_empresa, Papeis.Planejador).Criar("/api/cronogramas", RedeDoReadme.Cronograma());

        return criado.Headers.Location!.ToString();
    }

    [Theory]
    [InlineData]
    [InlineData(Papeis.Orcamentista)]
    [InlineData(Papeis.Gestor)]
    [InlineData(Papeis.Engenheiro)]
    [InlineData(Papeis.Orcamentista, Papeis.Gestor, Papeis.Engenheiro)]
    public async Task Sem_o_papel_de_planejador_nao_se_altera_cronograma(params string[] papeis)
    {
        var cronograma = await UmCronograma();
        var cliente = api.Cliente(_empresa, papeis);

        var criar = await cliente.Criar("/api/cronogramas", new { nome = "Outro" });
        var renomear = await cliente.Enviar(HttpMethod.Patch, cronograma, new { nome = "Outro nome" }, versao: 1);
        var atividade = await cliente.Enviar(HttpMethod.Post, $"{cronograma}/atividades", new { codigo = "LIM", descricao = "Limpeza", duracaoEmDiasUteis = 2 }, versao: 1);
        var remover = await cliente.Enviar(HttpMethod.Delete, cronograma, versao: 1);

        Assert.All(
            new[] { criar, renomear, atividade, remover },
            resposta => Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode));
    }

    [Theory]
    [InlineData]
    [InlineData(Papeis.Orcamentista)]
    [InlineData(Papeis.Engenheiro)]
    public async Task Qualquer_pessoa_da_empresa_le_o_cronograma(params string[] papeis)
    {
        var cronograma = await UmCronograma();
        var cliente = api.Cliente(_empresa, papeis);

        var leituras = new[]
        {
            await cliente.GetAsync("/api/cronogramas", Cancelamento),
            await cliente.GetAsync(cronograma, Cancelamento),
            await cliente.GetAsync($"{cronograma}/caminho-critico", Cancelamento),
            await cliente.GetAsync($"{cronograma}/programacao?inicio={RedeDoReadme.Inicio}", Cancelamento),
        };

        Assert.All(leituras, resposta => Assert.Equal(HttpStatusCode.OK, resposta.StatusCode));
    }

    [Fact]
    public async Task O_planejador_cria_altera_e_remove()
    {
        var cliente = api.Cliente(_empresa, Papeis.Planejador);

        var criar = await cliente.Criar("/api/cronogramas", new { nome = "Casa" });
        var cronograma = criar.Headers.Location!.ToString();
        var renomear = await cliente.Enviar(HttpMethod.Patch, cronograma, new { nome = "Casa térrea" }, versao: 1);
        var remover = await cliente.Enviar(HttpMethod.Delete, cronograma, versao: 2);

        Assert.Equal(
            (HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NoContent),
            (criar.StatusCode, renomear.StatusCode, remover.StatusCode));
    }
}
