using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using PlanejamentoObras.Api.Http;
using PlanejamentoObras.Api.Seguranca;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Api.Recursos.LinhasDeBase;

/// <summary>
/// As linhas de base: o plano aprovado, congelado, contra o qual a obra é
/// medida (ADR-0007). Não se alteram — plano novo é linha de base nova.
/// </summary>
public sealed class RecursoDeLinhasDeBase : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        var linhasDeBase = api.MapGroup("/linhas-de-base").WithTags("Linhas de base");

        linhasDeBase.MapGet("/", Listar).RequireAuthorization(Politicas.Consultar);
        linhasDeBase.MapPost("/", Aprovar).RequireAuthorization(Politicas.AprovarLinhaDeBase).ComIdempotencia();
        linhasDeBase.MapGet("/{id:guid}", Obter).RequireAuthorization(Politicas.Consultar);
        linhasDeBase.MapDelete("/{id:guid}", Remover).RequireAuthorization(Politicas.AprovarLinhaDeBase);
    }

    private static async Task<Ok<IReadOnlyList<ResumoResposta>>> Listar(RepositorioDeLinhasDeBase repositorio, CancellationToken cancelamento)
    {
        var resumos = await repositorio.Listar(cancelamento);

        return TypedResults.Ok<IReadOnlyList<ResumoResposta>>([.. resumos.Select(ResumoResposta.De)]);
    }

    /// <summary>
    /// Congela o plano: o orçamento calculado contra a base de custos, e o
    /// cronograma programado no calendário — copiados, sem referência ao que
    /// pode mudar depois.
    /// </summary>
    private static async Task<Created<LinhaDeBaseResposta>> Aprovar(
        NovaLinhaDeBase nova,
        RepositorioDeLinhasDeBase linhasDeBase,
        RepositorioDeOrcamentos orcamentos,
        RepositorioDeBasesDeCustos bases,
        RepositorioDeCronogramas cronogramas,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var orcamento = await Leituras.Orcamento(orcamentos, nova.Orcamento, cancelamento);
        var baseDeCustos = await Leituras.BaseDeCustos(bases, nova.BaseDeCustos, cancelamento);
        var cronograma = await Leituras.Cronograma(cronogramas, nova.Cronograma, cancelamento);

        var planilha = CalculadoraDoOrcamento.Calcular(orcamento.Agregado, baseDeCustos.Agregado);
        var obra = ProgramacaoDoCronograma.Programar(cronograma.Agregado, nova.Inicio, Leituras.Calendario(nova.Feriados, nova.SabadoUtil));
        var linhaDeBase = LinhaDeBase.Estabelecer(nova.Nome, nova.AprovadaEm, planilha, obra, nova.Datas);

        Etiquetas.Escrever(http, await linhasDeBase.Adicionar(linhaDeBase, cancelamento));

        return TypedResults.Created($"/api/linhas-de-base/{linhaDeBase.Id}", LinhaDeBaseResposta.De(linhaDeBase));
    }

    private static async Task<Results<Ok<LinhaDeBaseResposta>, StatusCodeHttpResult>> Obter(
        Guid id,
        RepositorioDeLinhasDeBase repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.LinhaDeBase(repositorio, id, cancelamento);
        Etiquetas.Escrever(http, lida.Versao);

        return Etiquetas.ClienteJaTem(http, lida.Versao)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(LinhaDeBaseResposta.De(lida.Agregado));
    }

    /// <remarks>Recusado com 409 enquanto houver acompanhamento medido contra ela.</remarks>
    private static async Task<NoContent> Remover(Guid id, VersaoLida versao, RepositorioDeLinhasDeBase repositorio, CancellationToken cancelamento)
    {
        await repositorio.Remover(new LinhaDeBaseId(id), versao.Valor, cancelamento);

        return TypedResults.NoContent();
    }
}

/// <summary>
/// A linha de base a aprovar: de que orçamento, calculado contra que base, e
/// de que cronograma, programado em que calendário.
/// </summary>
public sealed record NovaLinhaDeBase(
    string Nome,
    DateOnly AprovadaEm,
    Guid Orcamento,
    Guid BaseDeCustos,
    Guid Cronograma,
    DateOnly Inicio,
    IReadOnlyList<DateOnly>? Feriados = null,
    bool SabadoUtil = false,
    DatasDoCronograma Datas = DatasDoCronograma.Cedo);

public sealed record LinhaDeBaseResposta(
    Guid Id,
    string Nome,
    DateOnly AprovadaEm,
    DatasDoCronograma Datas,
    DateOnly InicioPlanejado,
    DateOnly TerminoPlanejado,
    IReadOnlyList<DateOnly> Meses,
    decimal Total,
    decimal CustoTotal,
    IReadOnlyList<LinhaPlanejadaResposta> Linhas)
{
    public static LinhaDeBaseResposta De(LinhaDeBase linhaDeBase) =>
        new(
            linhaDeBase.Id.Valor,
            linhaDeBase.Nome,
            linhaDeBase.AprovadaEm,
            linhaDeBase.Datas,
            linhaDeBase.InicioPlanejado,
            linhaDeBase.TerminoPlanejado,
            linhaDeBase.Meses,
            linhaDeBase.Total.Valor,
            linhaDeBase.CustoTotal.Valor,
            [.. linhaDeBase.Linhas.Select(LinhaPlanejadaResposta.De)]);
}

/// <summary>Uma linha do plano, com o planejado de cada mês da obra, a preço e a custo, na ordem dos meses.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(EtapaPlanejadaResposta), "etapa")]
[JsonDerivedType(typeof(ItemPlanejadoResposta), "item")]
public abstract record LinhaPlanejadaResposta(
    string Numero,
    string Descricao,
    decimal Total,
    IReadOnlyList<decimal> ValoresPlanejados,
    decimal CustoTotal,
    IReadOnlyList<decimal> CustosPlanejados)
{
    public static LinhaPlanejadaResposta De(LinhaPlanejada linha)
    {
        ArgumentNullException.ThrowIfNull(linha);

        IReadOnlyList<decimal> valores = [.. linha.ValoresPlanejados.Select(valor => valor.Valor)];
        IReadOnlyList<decimal> custos = [.. linha.CustosPlanejados.Select(custo => custo.Valor)];

        return linha is ItemPlanejado item
            ? new ItemPlanejadoResposta(
                item.Numero.Valor,
                item.Descricao,
                item.Total.Valor,
                valores,
                item.CustoTotal.Valor,
                custos,
                item.Unidade.Valor,
                item.Quantidade,
                item.PrecoUnitario.Valor,
                item.CustoUnitario.Valor,
                item.Atividade.Valor)
            : new EtapaPlanejadaResposta(linha.Numero.Valor, linha.Descricao, linha.Total.Valor, valores, linha.CustoTotal.Valor, custos);
    }
}

public sealed record EtapaPlanejadaResposta(
    string Numero,
    string Descricao,
    decimal Total,
    IReadOnlyList<decimal> ValoresPlanejados,
    decimal CustoTotal,
    IReadOnlyList<decimal> CustosPlanejados)
    : LinhaPlanejadaResposta(Numero, Descricao, Total, ValoresPlanejados, CustoTotal, CustosPlanejados);

public sealed record ItemPlanejadoResposta(
    string Numero,
    string Descricao,
    decimal Total,
    IReadOnlyList<decimal> ValoresPlanejados,
    decimal CustoTotal,
    IReadOnlyList<decimal> CustosPlanejados,
    string Unidade,
    decimal Quantidade,
    decimal PrecoUnitario,
    decimal CustoUnitario,
    string Atividade)
    : LinhaPlanejadaResposta(Numero, Descricao, Total, ValoresPlanejados, CustoTotal, CustosPlanejados);
