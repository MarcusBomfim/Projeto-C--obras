using PlanejamentoObras.Dominio.Custos;

namespace PlanejamentoObras.Infraestrutura.Documentos;

/// <summary>
/// Como uma base de custos é gravada: preços e coeficientes. Custos de
/// composição são derivados e não vão para o banco.
/// </summary>
internal sealed record DocumentoDaBaseDeCustos
{
    public const short Formato = 1;

    public required string Nome { get; init; }

    public required DateOnly DataBase { get; init; }

    public required IReadOnlyList<InsumoGravado> Insumos { get; init; }

    public required IReadOnlyList<ComposicaoGravada> Composicoes { get; init; }

    public static DocumentoDaBaseDeCustos De(BaseDeCustos baseDeCustos) => new()
    {
        Nome = baseDeCustos.Nome,
        DataBase = baseDeCustos.DataBase,
        Insumos =
        [
            .. baseDeCustos.Insumos.Select(insumo => new InsumoGravado
            {
                Codigo = insumo.Codigo.Valor,
                Descricao = insumo.Descricao,
                Unidade = insumo.Unidade.Valor,
                Tipo = insumo.Tipo,
                Preco = insumo.Preco.Valor,
            }),
        ],
        Composicoes =
        [
            .. baseDeCustos.Composicoes.Select(composicao => new ComposicaoGravada
            {
                Codigo = composicao.Codigo.Valor,
                Descricao = composicao.Descricao,
                Unidade = composicao.Unidade.Valor,
                Itens =
                [
                    .. composicao.Itens.Select(item => new ItemDaComposicaoGravado
                    {
                        Codigo = item.Codigo.Valor,
                        Coeficiente = item.Coeficiente,
                    }),
                ],
            }),
        ],
    };

    public BaseDeCustos ParaBaseDeCustos(BaseDeCustosId id) =>
        BaseDeCustos.Reconstituir(
            id,
            Nome,
            DataBase,
            Insumos.Select(insumo => (insumo.Codigo, insumo.Descricao, insumo.Unidade, insumo.Tipo, insumo.Preco)),
            Composicoes.Select(composicao => (
                composicao.Codigo,
                composicao.Descricao,
                composicao.Unidade,
                composicao.Itens.Select(item => (item.Codigo, item.Coeficiente)))));
}

internal sealed record InsumoGravado
{
    public required string Codigo { get; init; }

    public required string Descricao { get; init; }

    public required string Unidade { get; init; }

    public required TipoDeInsumo Tipo { get; init; }

    public required decimal Preco { get; init; }
}

internal sealed record ComposicaoGravada
{
    public required string Codigo { get; init; }

    public required string Descricao { get; init; }

    public required string Unidade { get; init; }

    public required IReadOnlyList<ItemDaComposicaoGravado> Itens { get; init; }
}

internal sealed record ItemDaComposicaoGravado
{
    public required string Codigo { get; init; }

    public required decimal Coeficiente { get; init; }
}
