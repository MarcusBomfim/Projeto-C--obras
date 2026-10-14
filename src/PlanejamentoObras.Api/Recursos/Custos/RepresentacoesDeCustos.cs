using PlanejamentoObras.Dominio.Custos;

namespace PlanejamentoObras.Api.Recursos.Custos;

/// <summary>
/// Uma base de custos nova: o nome, a data-base dos preços e, se quiser, os
/// insumos e as composições de uma vez. Uma composição pode usar outra que
/// venha depois dela na lista.
/// </summary>
public sealed record NovaBaseDeCustos(
    string Nome,
    DateOnly DataBase,
    IReadOnlyList<NovoInsumo>? Insumos = null,
    IReadOnlyList<NovaComposicao>? Composicoes = null);

public sealed record NovoInsumo(string Codigo, string Descricao, string Unidade, TipoDeInsumo Tipo, decimal Preco);

public sealed record NovaComposicao(string Codigo, string Descricao, string Unidade, IReadOnlyList<NovoItemDaComposicao>? Itens = null);

/// <summary>Um insumo ou outra composição dentro da composição, e quanto dele entra numa unidade dela.</summary>
public sealed record NovoItemDaComposicao(string Codigo, decimal Coeficiente);

/// <summary>O que muda na base; o que não vier fica como está.</summary>
public sealed record AlteracaoDaBaseDeCustos(string? Nome = null, DateOnly? DataBase = null);

public sealed record NovoPreco(decimal Preco);

public sealed record NovoCoeficiente(decimal Coeficiente);

public sealed record BaseDeCustosResposta(
    Guid Id,
    string Nome,
    DateOnly DataBase,
    IReadOnlyList<InsumoResposta> Insumos,
    IReadOnlyList<ComposicaoResposta> Composicoes)
{
    public static BaseDeCustosResposta De(BaseDeCustos baseDeCustos) =>
        new(
            baseDeCustos.Id.Valor,
            baseDeCustos.Nome,
            baseDeCustos.DataBase,
            [.. baseDeCustos.Insumos.Select(insumo =>
                new InsumoResposta(insumo.Codigo.Valor, insumo.Descricao, insumo.Unidade.Valor, insumo.Tipo, insumo.Preco.Valor))],
            [.. baseDeCustos.Composicoes.Select(composicao => new ComposicaoResposta(
                composicao.Codigo.Valor,
                composicao.Descricao,
                composicao.Unidade.Valor,
                [.. composicao.Itens.Select(item => new ItemDaComposicaoResposta(item.Codigo.Valor, item.Coeficiente))]))]);
}

public sealed record InsumoResposta(string Codigo, string Descricao, string Unidade, TipoDeInsumo Tipo, decimal Preco);

public sealed record ComposicaoResposta(string Codigo, string Descricao, string Unidade, IReadOnlyList<ItemDaComposicaoResposta> Itens);

public sealed record ItemDaComposicaoResposta(string Codigo, decimal Coeficiente);

/// <summary>A composição aberta, linha a linha, com o custo de cada uma — como vai para o anexo do orçamento.</summary>
public sealed record ComposicaoAnaliticaResposta(
    string Codigo,
    string Descricao,
    string Unidade,
    decimal CustoUnitario,
    IReadOnlyList<LinhaDaComposicaoResposta> Linhas)
{
    public static ComposicaoAnaliticaResposta De(ComposicaoAnalitica analitica) =>
        new(
            analitica.Codigo.Valor,
            analitica.Descricao,
            analitica.Unidade.Valor,
            analitica.CustoUnitario.Valor,
            [.. analitica.Linhas.Select(linha => new LinhaDaComposicaoResposta(
                linha.Codigo.Valor,
                linha.Descricao,
                linha.Unidade.Valor,
                linha.Tipo,
                linha.Coeficiente,
                linha.CustoUnitario.Valor,
                linha.CustoParcial.Valor))]);
}

/// <summary>Uma linha da composição aberta. Sem tipo, é outra composição — uma auxiliar.</summary>
public sealed record LinhaDaComposicaoResposta(
    string Codigo,
    string Descricao,
    string Unidade,
    TipoDeInsumo? Tipo,
    decimal Coeficiente,
    decimal CustoUnitario,
    decimal CustoParcial);
