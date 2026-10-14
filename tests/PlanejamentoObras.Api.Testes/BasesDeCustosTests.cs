using System.Net;
using PlanejamentoObras.Api.Recursos.Custos;
using PlanejamentoObras.Api.Seguranca;
using PlanejamentoObras.Dominio.Custos;

namespace PlanejamentoObras.Api.Testes;

/*
 * A base de custos pela API: insumos, composições que usam outras em
 * qualquer ordem, e a composição aberta, linha a linha.
 */
public sealed class BasesDeCustosTests(ApiDeTestes api)
{
    private readonly HttpClient _orcamentista = api.Cliente(Guid.CreateVersion7(), Papeis.Orcamentista);

    private Task<HttpResponseMessage> Alterar(HttpMethod metodo, string caminho, int versao, object? corpo = null) =>
        _orcamentista.Enviar(metodo, caminho, corpo, versao);

    private async Task<string> BaseDoReadme() =>
        $"/api/bases-de-custos/{await _orcamentista.Criar("/api/bases-de-custos", ObraDoReadme.BaseDeCustos()).IdCriado()}";

    [Fact]
    public async Task Abre_a_composicao_com_a_auxiliar_pelo_custo_dela()
    {
        var baseDeCustos = await BaseDoReadme();

        var alvenaria = await _orcamentista.LerComSucesso<ComposicaoAnaliticaResposta>($"{baseDeCustos}/composicoes/ALV");
        var argamassa = alvenaria.Linhas.Single(linha => linha.Codigo == "ARG");

        Assert.Equal(57.14m, alvenaria.CustoUnitario);
        Assert.Equal([17.82m, 7.75m, 22.99m, 8.58m], alvenaria.Linhas.Select(linha => linha.CustoParcial));
        Assert.Null(argamassa.Tipo);
        Assert.Equal(646.06m, argamassa.CustoUnitario);
    }

    [Fact]
    public async Task Monta_e_ajusta_a_base_passo_a_passo()
    {
        var baseDeCustos = $"/api/bases-de-custos/{await _orcamentista.Criar("/api/bases-de-custos", new { nome = "Tabela", dataBase = "2026-09-01" }).IdCriado()}";

        await Alterar(HttpMethod.Post, $"{baseDeCustos}/insumos", 1, new { codigo = "SER", descricao = "Servente", unidade = "h", tipo = "MaoDeObra", preco = 20m }).ComSucesso();
        await Alterar(HttpMethod.Post, $"{baseDeCustos}/composicoes", 2, new { codigo = "LIMP", descricao = "Limpeza", unidade = "m²", itens = new[] { new { codigo = "SER", coeficiente = 0.15m } } }).ComSucesso();
        await Alterar(HttpMethod.Patch, $"{baseDeCustos}/insumos/SER", 3, new { preco = 21.46m }).ComSucesso();
        await Alterar(HttpMethod.Patch, $"{baseDeCustos}/composicoes/LIMP/itens/SER", 4, new { coeficiente = 0.20m }).ComSucesso();
        var final = await Alterar(HttpMethod.Patch, baseDeCustos, 5, new { nome = "Tabela de outubro", dataBase = "2026-10-01" }).ComSucesso();

        var lida = await final.Ler<BaseDeCustosResposta>();
        var limpeza = await _orcamentista.LerComSucesso<ComposicaoAnaliticaResposta>($"{baseDeCustos}/composicoes/LIMP");

        Assert.Equal(6, final.Versao());
        Assert.Equal(("Tabela de outubro", new DateOnly(2026, 10, 1)), (lida.Nome, lida.DataBase));
        Assert.Equal(new InsumoResposta("SER", "Servente", "h", TipoDeInsumo.MaoDeObra, 21.46m), Assert.Single(lida.Insumos));
        Assert.Equal(4.29m, limpeza.CustoUnitario);
    }

    [Fact]
    public async Task Insumo_em_uso_nao_sai_e_a_mensagem_diz_onde_ele_e_usado()
    {
        var baseDeCustos = await BaseDoReadme();

        var resposta = await Alterar(HttpMethod.Delete, $"{baseDeCustos}/insumos/BLO", 1);
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Contains("ALV", problema.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Composicao_que_usaria_a_si_mesma_e_422()
    {
        var baseDeCustos = await BaseDoReadme();

        // A argamassa entra na alvenaria; a alvenaria na argamassa fecharia um laço.
        var resposta = await Alterar(HttpMethod.Post, $"{baseDeCustos}/composicoes/ARG/itens", 1, new { codigo = "ALV", coeficiente = 1m });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }

    [Fact]
    public async Task O_que_nao_existe_na_base_e_404()
    {
        var baseDeCustos = await BaseDoReadme();

        var respostas = new[]
        {
            await Alterar(HttpMethod.Patch, $"{baseDeCustos}/insumos/XYZ", 1, new { preco = 1m }),
            await Alterar(HttpMethod.Delete, $"{baseDeCustos}/insumos/ALV", 1),
            await Alterar(HttpMethod.Delete, $"{baseDeCustos}/composicoes/XYZ", 1),
            await Alterar(HttpMethod.Patch, $"{baseDeCustos}/composicoes/ALV/itens/CIM", 1, new { coeficiente = 1m }),
            await _orcamentista.GetAsync($"{baseDeCustos}/composicoes/BLO", TestContext.Current.CancellationToken),
        };

        Assert.All(respostas, resposta => Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode));
    }

    [Fact]
    public async Task Alterar_a_base_sem_dizer_o_que_e_400()
    {
        var baseDeCustos = await BaseDoReadme();

        Assert.Equal(HttpStatusCode.BadRequest, (await Alterar(HttpMethod.Patch, baseDeCustos, 1, new { })).StatusCode);
    }
}
