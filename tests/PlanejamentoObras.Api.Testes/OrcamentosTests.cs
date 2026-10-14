using System.Net;
using PlanejamentoObras.Api.Recursos.Orcamentos;
using PlanejamentoObras.Api.Seguranca;

namespace PlanejamentoObras.Api.Testes;

/*
 * O orçamento pela API: a EAP numerada como a planilha, o BDI de dois
 * jeitos, e a base de custos escolhida a cada cálculo (ADR-0005).
 */
public sealed class OrcamentosTests(ApiDeTestes api)
{
    private static readonly string[] CodigosDoOrcamento = ["LIMP", "FUND", "ESTR", "ALV", "REB", "ELET", "PINT", "ELV"];

    private readonly HttpClient _orcamentista = api.Cliente(Guid.CreateVersion7(), Papeis.Orcamentista);

    private Task<HttpResponseMessage> Alterar(HttpMethod metodo, string caminho, int versao, object? corpo = null) =>
        _orcamentista.Enviar(metodo, caminho, corpo, versao);

    private async Task<string> OrcamentoDoReadme() =>
        $"/api/orcamentos/{await _orcamentista.Criar("/api/orcamentos", ObraDoReadme.Orcamento()).IdCriado()}";

    [Fact]
    public async Task Cria_a_EAP_inteira_numerada_como_a_planilha()
    {
        var criado = await _orcamentista.Criar("/api/orcamentos", ObraDoReadme.Orcamento()).ComSucesso();
        var orcamento = await criado.Ler<OrcamentoResposta>();
        var elevador = Assert.IsType<ItemDoOrcamentoResposta>(Assert.Single(orcamento.Etapas[4].Filhos));

        Assert.Equal(["1", "2", "3", "4", "5"], orcamento.Etapas.Select(etapa => etapa.Numero));
        Assert.Equal(["3.1", "3.2"], orcamento.Etapas[2].Filhos.Select(filho => filho.Numero));
        Assert.Equal((22.88m, 6.15m), (orcamento.Bdi.Percentual, orcamento.Bdi.Parcelas?.Tributos));
        Assert.Equal(("5.1", "ELV", "ENT", 12m), (elevador.Numero, elevador.Codigo, elevador.Atividade, elevador.BdiDiferenciado?.Percentual));
    }

    [Fact]
    public async Task Monta_a_EAP_passo_a_passo_com_subetapa()
    {
        var orcamento = $"/api/orcamentos/{await _orcamentista.Criar("/api/orcamentos", new { nome = "Casa", bdi = new { percentual = 25m } }).IdCriado()}";

        await Alterar(HttpMethod.Post, $"{orcamento}/etapas", 1, new { titulo = "Estrutura" }).ComSucesso();
        await Alterar(HttpMethod.Post, $"{orcamento}/etapas", 2, new { titulo = "Fundações", dentroDe = "1" }).ComSucesso();
        await Alterar(HttpMethod.Post, $"{orcamento}/itens", 3, new { dentroDe = "1.1", codigo = "FUND", quantidade = 1m }).ComSucesso();
        await Alterar(HttpMethod.Patch, $"{orcamento}/etapas/1", 4, new { titulo = "Infraestrutura e estrutura" }).ComSucesso();
        await Alterar(HttpMethod.Patch, $"{orcamento}/itens/1.1.1", 5, new { quantidade = 2m }).ComSucesso();
        await Alterar(HttpMethod.Put, $"{orcamento}/itens/1.1.1/atividade", 6, new { atividade = "fun" }).ComSucesso();
        var final = await Alterar(HttpMethod.Put, $"{orcamento}/itens/1.1.1/bdi", 7, new { percentual = 12m }).ComSucesso();

        var lido = await final.Ler<OrcamentoResposta>();
        var subetapa = Assert.IsType<EtapaResposta>(Assert.Single(lido.Etapas[0].Filhos));
        var item = Assert.IsType<ItemDoOrcamentoResposta>(Assert.Single(subetapa.Filhos));

        Assert.Equal(8, final.Versao());
        Assert.Equal(("Infraestrutura e estrutura", "1.1", "Fundações"), (lido.Etapas[0].Titulo, subetapa.Numero, subetapa.Titulo));
        Assert.Equal(("1.1.1", 2m, "FUN", 12m), (item.Numero, item.Quantidade, item.Atividade, item.BdiDiferenciado?.Percentual));
    }

    [Fact]
    public async Task Desvincula_e_tira_o_BDI_diferenciado()
    {
        var orcamento = await OrcamentoDoReadme();

        await Alterar(HttpMethod.Delete, $"{orcamento}/itens/5.1/bdi", 1).ComSucesso();
        var final = await Alterar(HttpMethod.Delete, $"{orcamento}/itens/5.1/atividade", 2).ComSucesso();

        var elevador = Assert.IsType<ItemDoOrcamentoResposta>((await final.Ler<OrcamentoResposta>()).Etapas[4].Filhos[0]);

        Assert.Null(elevador.Atividade);
        Assert.Null(elevador.BdiDiferenciado);
    }

    [Fact]
    public async Task Remover_uma_etapa_renumera_as_seguintes()
    {
        var orcamento = await OrcamentoDoReadme();

        var lido = await (await Alterar(HttpMethod.Delete, $"{orcamento}/etapas/1", 1).ComSucesso()).Ler<OrcamentoResposta>();

        Assert.Equal(("Infraestrutura e estrutura", 4), (lido.Etapas[0].Titulo, lido.Etapas.Count));
        Assert.Equal(["1.1", "1.2"], lido.Etapas[0].Filhos.Select(filho => filho.Numero));
    }

    [Fact]
    public async Task A_planilha_contra_uma_base_sem_os_codigos_lista_todos_os_que_faltam()
    {
        var orcamento = await OrcamentoDoReadme();
        var vazia = await _orcamentista.Criar("/api/bases-de-custos", new { nome = "Vazia", dataBase = "2026-09-01" }).IdCriado();

        var resposta = await _orcamentista.GetAsync($"{orcamento}/planilha?baseDeCustos={vazia}", TestContext.Current.CancellationToken);
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.All(CodigosDoOrcamento, codigo => Assert.Contains(codigo, problema.Detail, StringComparison.Ordinal));
    }

    [Fact]
    public async Task O_que_nao_existe_na_EAP_e_404()
    {
        var orcamento = await OrcamentoDoReadme();

        var respostas = new[]
        {
            await Alterar(HttpMethod.Patch, $"{orcamento}/itens/9.9", 1, new { quantidade = 1m }),
            await Alterar(HttpMethod.Patch, $"{orcamento}/etapas/2.1", 1, new { titulo = "2.1 é um item, e não uma etapa" }),
            await Alterar(HttpMethod.Delete, $"{orcamento}/itens/2", 1),
            await Alterar(HttpMethod.Post, $"{orcamento}/itens", 1, new { dentroDe = "8", codigo = "ALV", quantidade = 1m }),
        };

        Assert.All(respostas, resposta => Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode));
    }

    [Fact]
    public async Task BDI_pelos_dois_jeitos_ao_mesmo_tempo_e_400()
    {
        var resposta = await _orcamentista.Criar(
            "/api/orcamentos",
            new { nome = "Casa", bdi = new { percentual = 25m, parcelas = new { administracaoCentral = 4m, seguros = 0m, riscos = 0m, garantias = 0m, despesasFinanceiras = 0m, lucro = 0m, tributos = 0m } } });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task O_tipo_do_no_pode_vir_em_qualquer_posicao_mas_tem_de_vir()
    {
        const string ComTipoNoFim = """
            {"nome": "Casa", "bdi": {"percentual": 25},
             "etapas": [{"titulo": "Estrutura", "filhos": [{"codigo": "ESTR", "quantidade": 1, "tipo": "item"}]}]}
            """;
        const string SemTipo = """
            {"nome": "Casa", "bdi": {"percentual": 25},
             "etapas": [{"titulo": "Estrutura", "filhos": [{"codigo": "ESTR", "quantidade": 1}]}]}
            """;

        const string FilhoVazio = """
            {"nome": "Casa", "bdi": {"percentual": 25}, "etapas": [{"titulo": "Estrutura", "filhos": [{}]}]}
            """;

        var comTipoNoFim = await _orcamentista.Criar("/api/orcamentos", ComTipoNoFim);
        var semTipo = await _orcamentista.Criar("/api/orcamentos", SemTipo);
        var filhoVazio = await _orcamentista.Criar("/api/orcamentos", FilhoVazio);

        Assert.Equal(
            (HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest),
            (comTipoNoFim.StatusCode, semTipo.StatusCode, filhoVazio.StatusCode));
        Assert.Contains("\"etapa\" ou \"item\"", (await filhoVazio.Problema()).Detail, StringComparison.Ordinal);
    }
}
