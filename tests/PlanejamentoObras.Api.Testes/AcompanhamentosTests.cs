using System.Net;
using PlanejamentoObras.Api.Recursos.Acompanhamentos;
using PlanejamentoObras.Api.Seguranca;

namespace PlanejamentoObras.Api.Testes;

/*
 * As medições pela API. Registrar uma é alterar o acompanhamento, com
 * If-Match: a mesma medição mandada de novo com a versão de antes é recusada
 * — e a obra não é medida, nem paga, duas vezes (ADR-0008, ADR-0015).
 */
public sealed class AcompanhamentosTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _empresa = Guid.CreateVersion7();

    private HttpClient Engenheiro() => api.Cliente(_empresa, Papeis.Engenheiro);

    private async Task<(Guid LinhaDeBase, string Acompanhamento)> Acompanhamento()
    {
        var orcamentista = api.Cliente(_empresa, Papeis.Orcamentista);
        var orcamento = await orcamentista.Criar("/api/orcamentos", ObraDoReadme.Orcamento()).IdCriado();
        var baseDeCustos = await orcamentista.Criar("/api/bases-de-custos", ObraDoReadme.BaseDeCustos()).IdCriado();
        var cronograma = await api.Cliente(_empresa, Papeis.Planejador).Criar("/api/cronogramas", RedeDoReadme.Cronograma()).IdCriado();
        var linhaDeBase = await api.Cliente(_empresa, Papeis.Gestor)
            .Criar("/api/linhas-de-base", ObraDoReadme.LinhaDeBase(orcamento, baseDeCustos, cronograma))
            .IdCriado();

        return (linhaDeBase, $"/api/acompanhamentos/{await Engenheiro().Criar("/api/acompanhamentos", new { linhaDeBase }).IdCriado()}");
    }

    private Task<HttpResponseMessage> Medir(string acompanhamento, object medicao, int versao) =>
        Engenheiro().Enviar(HttpMethod.Post, $"{acompanhamento}/medicoes", medicao, versao);

    [Fact]
    public async Task A_mesma_medicao_mandada_de_novo_e_412_e_a_obra_nao_e_medida_duas_vezes()
    {
        var (_, acompanhamento) = await Acompanhamento();

        var primeira = await Medir(acompanhamento, ObraDoReadme.MedicaoDeOutubro, versao: 1);
        var repetida = await Medir(acompanhamento, ObraDoReadme.MedicaoDeOutubro, versao: 1);
        var lido = await Engenheiro().LerComSucesso<AcompanhamentoResposta>(acompanhamento);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.PreconditionFailed), (primeira.StatusCode, repetida.StatusCode));
        Assert.Equal((1, new DateOnly(2026, 10, 1)), Assert.Single(lido.Medicoes) is var medicao ? (medicao.Numero, medicao.Mes) : default);
    }

    [Fact]
    public async Task Medir_alem_do_contratado_e_422()
    {
        var (_, acompanhamento) = await Acompanhamento();

        var resposta = await Medir(acompanhamento, new { mes = "2026-10-30", lancamentos = new[] { new { item = "2.1", quantidade = 1.5m, custoReal = 0m } } }, versao: 1);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Contains("2.1", (await resposta.Problema()).Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task So_a_ultima_medicao_se_desfaz()
    {
        var (_, acompanhamento) = await Acompanhamento();
        await Medir(acompanhamento, ObraDoReadme.MedicaoDeOutubro, versao: 1).ComSucesso();
        await Medir(acompanhamento, ObraDoReadme.MedicaoDeNovembro, versao: 2).ComSucesso();

        var anterior = await Engenheiro().Enviar(HttpMethod.Delete, $"{acompanhamento}/medicoes/1", versao: 3);
        var inexistente = await Engenheiro().Enviar(HttpMethod.Delete, $"{acompanhamento}/medicoes/5", versao: 3);
        var ultima = await Engenheiro().Enviar(HttpMethod.Delete, $"{acompanhamento}/medicoes/2", versao: 3);

        Assert.Equal(
            (HttpStatusCode.UnprocessableEntity, HttpStatusCode.NotFound, HttpStatusCode.OK),
            (anterior.StatusCode, inexistente.StatusCode, ultima.StatusCode));
        Assert.Single((await ultima.Ler<AcompanhamentoResposta>()).Medicoes);
    }

    [Fact]
    public async Task Boletim_de_medicao_que_nao_existe_e_404()
    {
        var (_, acompanhamento) = await Acompanhamento();

        var resposta = await Engenheiro().GetAsync($"{acompanhamento}/boletins/1", Cancelamento);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Valor_agregado_de_um_mes_ja_medido()
    {
        var (_, acompanhamento) = await Acompanhamento();
        await Medir(acompanhamento, ObraDoReadme.MedicaoDeOutubro, versao: 1).ComSucesso();
        await Medir(acompanhamento, ObraDoReadme.MedicaoDeNovembro, versao: 2).ComSucesso();

        var outubro = await Engenheiro().LerComSucesso<ValorAgregadoResposta>($"{acompanhamento}/valor-agregado?mes=2026-10-15");
        var semMedicao = await Engenheiro().GetAsync($"{acompanhamento}/valor-agregado?mes=2026-12-01", Cancelamento);

        Assert.Equal((1, new DateOnly(2026, 10, 1)), (outubro.Medicao, outubro.Mes));
        Assert.Equal(172_200.00m, outubro.Obra.CustoReal);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, semMedicao.StatusCode);
    }

    [Fact]
    public async Task Lista_os_acompanhamentos_da_linha_de_base()
    {
        var (linhaDeBase, acompanhamento) = await Acompanhamento();
        await Medir(acompanhamento, ObraDoReadme.MedicaoDeOutubro, versao: 1).ComSucesso();

        var lista = await Engenheiro().LerComSucesso<List<AcompanhamentoDaLinhaDeBase>>($"/api/linhas-de-base/{linhaDeBase}/acompanhamentos");

        Assert.Equal(new AcompanhamentoDaLinhaDeBase(Guid.Parse(acompanhamento.Split('/')[^1]), 2), Assert.Single(lista));
    }

    [Fact]
    public async Task Acompanhamento_de_linha_de_base_que_nao_existe_e_404()
    {
        var resposta = await Engenheiro().Criar("/api/acompanhamentos", new { linhaDeBase = Guid.CreateVersion7() });

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
