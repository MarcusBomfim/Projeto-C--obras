using Microsoft.AspNetCore.Http.HttpResults;
using PlanejamentoObras.Api.Http;
using PlanejamentoObras.Api.Seguranca;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Api.Recursos.Custos;

/// <summary>As bases de custos: insumos com preço, composições que os combinam, e a composição aberta.</summary>
public sealed class RecursoDeBasesDeCustos : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        var bases = api.MapGroup("/bases-de-custos").WithTags("Bases de custos");

        bases.MapGet("/", Listar).RequireAuthorization(Politicas.Consultar);
        bases.MapPost("/", Criar).RequireAuthorization(Politicas.Orcar).ComIdempotencia();
        bases.MapGet("/{id:guid}", Obter).RequireAuthorization(Politicas.Consultar);
        bases.MapPatch("/{id:guid}", AlterarBase).RequireAuthorization(Politicas.Orcar);
        bases.MapDelete("/{id:guid}", Remover).RequireAuthorization(Politicas.Orcar);

        bases.MapPost("/{id:guid}/insumos", AdicionarInsumo).RequireAuthorization(Politicas.Orcar);
        bases.MapPatch("/{id:guid}/insumos/{codigo}", AlterarPreco).RequireAuthorization(Politicas.Orcar);
        bases.MapDelete("/{id:guid}/insumos/{codigo}", RemoverInsumo).RequireAuthorization(Politicas.Orcar);

        bases.MapPost("/{id:guid}/composicoes", AdicionarComposicao).RequireAuthorization(Politicas.Orcar);
        bases.MapGet("/{id:guid}/composicoes/{codigo}", ComposicaoAnalitica).RequireAuthorization(Politicas.Consultar);
        bases.MapDelete("/{id:guid}/composicoes/{codigo}", RemoverComposicao).RequireAuthorization(Politicas.Orcar);
        bases.MapPost("/{id:guid}/composicoes/{codigo}/itens", AdicionarItem).RequireAuthorization(Politicas.Orcar);
        bases.MapPatch("/{id:guid}/composicoes/{codigo}/itens/{item}", AlterarCoeficiente).RequireAuthorization(Politicas.Orcar);
        bases.MapDelete("/{id:guid}/composicoes/{codigo}/itens/{item}", RemoverItem).RequireAuthorization(Politicas.Orcar);
    }

    private static async Task<Ok<IReadOnlyList<ResumoResposta>>> Listar(RepositorioDeBasesDeCustos repositorio, CancellationToken cancelamento)
    {
        var resumos = await repositorio.Listar(cancelamento);

        return TypedResults.Ok<IReadOnlyList<ResumoResposta>>([.. resumos.Select(ResumoResposta.De)]);
    }

    private static async Task<Created<BaseDeCustosResposta>> Criar(
        NovaBaseDeCustos nova,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var baseDeCustos = new BaseDeCustos(nova.Nome, nova.DataBase);

        foreach (var insumo in nova.Insumos ?? [])
        {
            baseDeCustos.AdicionarInsumo(insumo.Codigo, insumo.Descricao, insumo.Unidade, insumo.Tipo, insumo.Preco);
        }

        // Todas as composições primeiro, os itens depois: uma composição pode
        // usar outra que vem depois dela na lista.
        foreach (var composicao in nova.Composicoes ?? [])
        {
            baseDeCustos.AdicionarComposicao(composicao.Codigo, composicao.Descricao, composicao.Unidade);
        }

        foreach (var composicao in nova.Composicoes ?? [])
        {
            foreach (var item in composicao.Itens ?? [])
            {
                baseDeCustos.AdicionarItem(composicao.Codigo, item.Codigo, item.Coeficiente);
            }
        }

        Etiquetas.Escrever(http, await repositorio.Adicionar(baseDeCustos, cancelamento));

        return TypedResults.Created($"/api/bases-de-custos/{baseDeCustos.Id}", BaseDeCustosResposta.De(baseDeCustos));
    }

    private static async Task<Results<Ok<BaseDeCustosResposta>, StatusCodeHttpResult>> Obter(
        Guid id,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.BaseDeCustos(repositorio, id, cancelamento);
        Etiquetas.Escrever(http, lida.Versao);

        return Etiquetas.ClienteJaTem(http, lida.Versao)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(BaseDeCustosResposta.De(lida.Agregado));
    }

    private static Task<Ok<BaseDeCustosResposta>> AlterarBase(
        Guid id,
        AlteracaoDaBaseDeCustos alteracao,
        VersaoLida versao,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        if (alteracao is { Nome: null, DataBase: null })
        {
            throw new PedidoInvalido("Diga o que muda na base: nome, dataBase, ou os dois.");
        }

        return Alterar(
            id,
            versao,
            repositorio,
            http,
            baseDeCustos =>
            {
                if (alteracao.Nome is { } nome)
                {
                    baseDeCustos.Renomear(nome);
                }

                if (alteracao.DataBase is { } dataBase)
                {
                    baseDeCustos.AlterarDataBase(dataBase);
                }
            },
            cancelamento);
    }

    private static async Task<NoContent> Remover(Guid id, VersaoLida versao, RepositorioDeBasesDeCustos repositorio, CancellationToken cancelamento)
    {
        await repositorio.Remover(new BaseDeCustosId(id), versao.Valor, cancelamento);

        return TypedResults.NoContent();
    }

    private static Task<Ok<BaseDeCustosResposta>> AdicionarInsumo(
        Guid id,
        NovoInsumo novo,
        VersaoLida versao,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            baseDeCustos => baseDeCustos.AdicionarInsumo(novo.Codigo, novo.Descricao, novo.Unidade, novo.Tipo, novo.Preco),
            cancelamento);

    private static Task<Ok<BaseDeCustosResposta>> AlterarPreco(
        Guid id,
        string codigo,
        NovoPreco novo,
        VersaoLida versao,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            baseDeCustos =>
            {
                ExigirInsumo(baseDeCustos, codigo);
                baseDeCustos.AlterarPrecoDoInsumo(codigo, novo.Preco);
            },
            cancelamento);

    private static Task<Ok<BaseDeCustosResposta>> RemoverInsumo(
        Guid id,
        string codigo,
        VersaoLida versao,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            baseDeCustos =>
            {
                ExigirInsumo(baseDeCustos, codigo);
                baseDeCustos.RemoverInsumo(codigo);
            },
            cancelamento);

    private static Task<Ok<BaseDeCustosResposta>> AdicionarComposicao(
        Guid id,
        NovaComposicao nova,
        VersaoLida versao,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            baseDeCustos =>
            {
                baseDeCustos.AdicionarComposicao(nova.Codigo, nova.Descricao, nova.Unidade);

                foreach (var item in nova.Itens ?? [])
                {
                    baseDeCustos.AdicionarItem(nova.Codigo, item.Codigo, item.Coeficiente);
                }
            },
            cancelamento);

    private static async Task<Ok<ComposicaoAnaliticaResposta>> ComposicaoAnalitica(
        Guid id,
        string codigo,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.BaseDeCustos(repositorio, id, cancelamento);
        ExigirComposicao(lida.Agregado, codigo);
        Etiquetas.Escrever(http, lida.Versao);

        return TypedResults.Ok(ComposicaoAnaliticaResposta.De(CalculadoraDeCustos.Calcular(lida.Agregado, codigo)));
    }

    private static Task<Ok<BaseDeCustosResposta>> RemoverComposicao(
        Guid id,
        string codigo,
        VersaoLida versao,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            baseDeCustos =>
            {
                ExigirComposicao(baseDeCustos, codigo);
                baseDeCustos.RemoverComposicao(codigo);
            },
            cancelamento);

    private static Task<Ok<BaseDeCustosResposta>> AdicionarItem(
        Guid id,
        string codigo,
        NovoItemDaComposicao novo,
        VersaoLida versao,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            baseDeCustos =>
            {
                ExigirComposicao(baseDeCustos, codigo);
                baseDeCustos.AdicionarItem(codigo, novo.Codigo, novo.Coeficiente);
            },
            cancelamento);

    private static Task<Ok<BaseDeCustosResposta>> AlterarCoeficiente(
        Guid id,
        string codigo,
        string item,
        NovoCoeficiente novo,
        VersaoLida versao,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            baseDeCustos =>
            {
                ExigirItem(baseDeCustos, codigo, item);
                baseDeCustos.AlterarCoeficiente(codigo, item, novo.Coeficiente);
            },
            cancelamento);

    private static Task<Ok<BaseDeCustosResposta>> RemoverItem(
        Guid id,
        string codigo,
        string item,
        VersaoLida versao,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            baseDeCustos =>
            {
                ExigirItem(baseDeCustos, codigo, item);
                baseDeCustos.RemoverItem(codigo, item);
            },
            cancelamento);

    private static async Task<Ok<BaseDeCustosResposta>> Alterar(
        Guid id,
        VersaoLida versao,
        RepositorioDeBasesDeCustos repositorio,
        HttpContext http,
        Action<BaseDeCustos> alteracao,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.BaseDeCustos(repositorio, id, cancelamento);
        Lidos.ConferirVersao(lida, versao, $"A base de custos {id}");

        alteracao(lida.Agregado);

        Etiquetas.Escrever(http, await repositorio.Atualizar(lida.Agregado, versao.Valor, cancelamento));

        return TypedResults.Ok(BaseDeCustosResposta.De(lida.Agregado));
    }

    private static void ExigirInsumo(BaseDeCustos baseDeCustos, string codigo)
    {
        var procurado = new CodigoDeCusto(codigo);

        if (!baseDeCustos.Insumos.Any(insumo => insumo.Codigo == procurado))
        {
            throw new AgregadoNaoEncontrado($"Não existe o insumo {procurado} na base de custos {baseDeCustos.Id}.");
        }
    }

    private static void ExigirComposicao(BaseDeCustos baseDeCustos, string codigo)
    {
        var procurado = new CodigoDeCusto(codigo);

        if (!baseDeCustos.Composicoes.Any(composicao => composicao.Codigo == procurado))
        {
            throw new AgregadoNaoEncontrado($"Não existe a composição {procurado} na base de custos {baseDeCustos.Id}.");
        }
    }

    private static void ExigirItem(BaseDeCustos baseDeCustos, string composicao, string item)
    {
        ExigirComposicao(baseDeCustos, composicao);

        var procurado = new CodigoDeCusto(item);

        if (!baseDeCustos.Composicao(composicao).Itens.Any(existente => existente.Codigo == procurado))
        {
            throw new AgregadoNaoEncontrado($"A composição {new CodigoDeCusto(composicao)} não tem o item {procurado}.");
        }
    }
}
