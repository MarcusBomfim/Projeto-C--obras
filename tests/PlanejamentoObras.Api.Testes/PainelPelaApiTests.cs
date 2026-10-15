using System.Net;
using PlanejamentoObras.Api.Recursos.Painel;
using PlanejamentoObras.Api.Seguranca;
using PlanejamentoObras.Dominio.ValorAgregado;

namespace PlanejamentoObras.Api.Testes;

/*
 * O painel das obras pela API: só leitura, de qualquer pessoa da empresa — e
 * só das obras dela. A linha chega depois da gravação, quando o processador
 * trata a mensagem; até lá, o acompanhamento não está no painel (ADR-0017).
 */
public sealed class PainelPelaApiTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _empresa = Guid.CreateVersion7();

    private async Task<Guid> UmAcompanhamento()
    {
        var orcamentista = api.Cliente(_empresa, Papeis.Orcamentista);
        var orcamento = await orcamentista.Criar("/api/orcamentos", ObraDoReadme.Orcamento()).IdCriado();
        var baseDeCustos = await orcamentista.Criar("/api/bases-de-custos", ObraDoReadme.BaseDeCustos()).IdCriado();
        var cronograma = await api.Cliente(_empresa, Papeis.Planejador).Criar("/api/cronogramas", RedeDoReadme.Cronograma()).IdCriado();
        var linhaDeBase = await api.Cliente(_empresa, Papeis.Gestor)
            .Criar("/api/linhas-de-base", ObraDoReadme.LinhaDeBase(orcamento, baseDeCustos, cronograma))
            .IdCriado();

        return await api.Cliente(_empresa, Papeis.Engenheiro).Criar("/api/acompanhamentos", new { linhaDeBase }).IdCriado();
    }

    [Fact]
    public async Task Obra_recem_acompanhada_entra_no_painel_sem_medicao_so_para_a_empresa_dela()
    {
        var acompanhamento = await UmAcompanhamento();

        // Ler é de qualquer papel da empresa.
        var planejador = api.Cliente(_empresa, Papeis.Planejador);
        ObraNoPainelResposta? noPainel = null;
        await api.ProcessarAte(async () => (noPainel = await planejador.LerDoPainel(acompanhamento)) is not null, "o acompanhamento novo chegar ao painel");

        Assert.Equal(
            (SituacaoDaObra.SemMedicao, (int?)null, (decimal?)null, 1),
            (noPainel!.Situacao, noPainel.Medicao, noPainel.IndiceDeDesempenhoDePrazo, noPainel.VersaoDoAcompanhamento));
        Assert.Equal(noPainel, Assert.Single(await planejador.LerComSucesso<List<ObraNoPainelResposta>>("/api/painel")));

        // Outra construtora não vê a obra — nem para saber que ela existe.
        var deOutra = api.Cliente(Guid.CreateVersion7(), Papeis.Gestor);
        Assert.Empty(await deOutra.LerComSucesso<List<ObraNoPainelResposta>>("/api/painel"));
        Assert.Null(await deOutra.LerDoPainel(acompanhamento));
    }

    [Fact]
    public async Task Acompanhamento_fora_do_painel_e_404_com_o_motivo()
    {
        var resposta = await api.Cliente(_empresa, Papeis.Engenheiro).GetAsync($"/api/painel/{Guid.CreateVersion7()}", Cancelamento);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Contains("o painel ainda não chegou nele", (await resposta.Problema()).Detail, StringComparison.Ordinal);
    }
}
