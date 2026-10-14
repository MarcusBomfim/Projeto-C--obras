using System.Net;
using PlanejamentoObras.Api.Recursos;
using PlanejamentoObras.Api.Recursos.LinhasDeBase;
using PlanejamentoObras.Api.Seguranca;

namespace PlanejamentoObras.Api.Testes;

/*
 * A linha de base pela API: o plano aprovado é uma cópia, e não muda quando
 * o orçamento muda (ADR-0007). E não sai do banco enquanto houver
 * acompanhamento medido contra ela.
 */
public sealed class LinhasDeBaseTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _empresa = Guid.CreateVersion7();

    private HttpClient Cliente(params string[] papeis) => api.Cliente(_empresa, papeis);

    private async Task<(Guid Orcamento, Guid BaseDeCustos, Guid Cronograma)> Plano()
    {
        var orcamentista = Cliente(Papeis.Orcamentista);

        return (
            await orcamentista.Criar("/api/orcamentos", ObraDoReadme.Orcamento()).IdCriado(),
            await orcamentista.Criar("/api/bases-de-custos", ObraDoReadme.BaseDeCustos()).IdCriado(),
            await Cliente(Papeis.Planejador).Criar("/api/cronogramas", RedeDoReadme.Cronograma()).IdCriado());
    }

    [Fact]
    public async Task A_linha_de_base_nao_muda_quando_o_orcamento_muda()
    {
        var (orcamento, baseDeCustos, cronograma) = await Plano();
        var linhaDeBase = await Cliente(Papeis.Gestor).Criar("/api/linhas-de-base", ObraDoReadme.LinhaDeBase(orcamento, baseDeCustos, cronograma)).IdCriado();

        // Um aditivo: o orçamento passa a ter três elevadores.
        await Cliente(Papeis.Orcamentista).Enviar(HttpMethod.Patch, $"/api/orcamentos/{orcamento}/itens/5.1", new { quantidade = 3m }, versao: 1).ComSucesso();

        var plano = await Cliente().LerComSucesso<LinhaDeBaseResposta>($"/api/linhas-de-base/{linhaDeBase}");
        var elevador = plano.Linhas.OfType<ItemPlanejadoResposta>().Single(item => item.Numero == "5.1");

        Assert.Equal((1_667_428.66m, 2m), (plano.Total, elevador.Quantidade));
        Assert.Equal(["2026-10-01", "2026-11-01", "2026-12-01", "2027-01-01"], plano.Meses.Select(mes => mes.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(plano.Meses.Count, elevador.ValoresPlanejados.Count);
    }

    [Fact]
    public async Task Sem_o_orcamento_a_base_ou_o_cronograma_e_404()
    {
        var (orcamento, baseDeCustos, cronograma) = await Plano();
        var gestor = Cliente(Papeis.Gestor);
        var inventado = Guid.CreateVersion7();

        var respostas = new[]
        {
            await gestor.Criar("/api/linhas-de-base", ObraDoReadme.LinhaDeBase(inventado, baseDeCustos, cronograma)),
            await gestor.Criar("/api/linhas-de-base", ObraDoReadme.LinhaDeBase(orcamento, inventado, cronograma)),
            await gestor.Criar("/api/linhas-de-base", ObraDoReadme.LinhaDeBase(orcamento, baseDeCustos, inventado)),
        };

        Assert.All(respostas, resposta => Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode));
    }

    [Fact]
    public async Task Item_sem_atividade_impede_a_aprovacao()
    {
        var (orcamento, baseDeCustos, cronograma) = await Plano();
        await Cliente(Papeis.Orcamentista).Enviar(HttpMethod.Delete, $"/api/orcamentos/{orcamento}/itens/4.2/atividade", versao: 1).ComSucesso();

        var resposta = await Cliente(Papeis.Gestor).Criar("/api/linhas-de-base", ObraDoReadme.LinhaDeBase(orcamento, baseDeCustos, cronograma));
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Contains("4.2", problema.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Linha_de_base_em_uso_nao_sai_ate_sair_o_acompanhamento()
    {
        var (orcamento, baseDeCustos, cronograma) = await Plano();
        var gestor = Cliente(Papeis.Gestor);
        var linhaDeBase = await gestor.Criar("/api/linhas-de-base", ObraDoReadme.LinhaDeBase(orcamento, baseDeCustos, cronograma)).IdCriado();
        var acompanhamento = await Cliente(Papeis.Engenheiro).Criar("/api/acompanhamentos", new { linhaDeBase }).IdCriado();

        var emUso = await gestor.Enviar(HttpMethod.Delete, $"/api/linhas-de-base/{linhaDeBase}", versao: 1);
        await Cliente(Papeis.Engenheiro).Enviar(HttpMethod.Delete, $"/api/acompanhamentos/{acompanhamento}", versao: 1).ComSucesso();
        var livre = await gestor.Enviar(HttpMethod.Delete, $"/api/linhas-de-base/{linhaDeBase}", versao: 1);

        Assert.Equal(HttpStatusCode.Conflict, emUso.StatusCode);
        Assert.EndsWith("#em-uso", (await emUso.Problema()).Type, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, livre.StatusCode);
    }

    [Fact]
    public async Task Lista_as_linhas_de_base_da_empresa()
    {
        var (orcamento, baseDeCustos, cronograma) = await Plano();
        await Cliente(Papeis.Gestor).Criar("/api/linhas-de-base", ObraDoReadme.LinhaDeBase(orcamento, baseDeCustos, cronograma)).IdCriado();

        var lista = await (await Cliente().GetAsync("/api/linhas-de-base", Cancelamento)).Ler<List<ResumoResposta>>();

        Assert.Equal("Linha de base 1 — contrato", Assert.Single(lista).Nome);
    }
}
