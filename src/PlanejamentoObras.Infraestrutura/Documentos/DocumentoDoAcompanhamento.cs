using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;

namespace PlanejamentoObras.Infraestrutura.Documentos;

/// <summary>
/// Como um acompanhamento é gravado: os lançamentos de cada medição, na
/// ordem. A linha de base é outro agregado, referenciado pela coluna
/// <c>linha_de_base_id</c>, e não copiado para cá.
/// </summary>
internal sealed record DocumentoDoAcompanhamento
{
    public const short Formato = 1;

    public required IReadOnlyList<MedicaoGravada> Medicoes { get; init; }

    public static DocumentoDoAcompanhamento De(Acompanhamento acompanhamento) => new()
    {
        Medicoes =
        [
            .. acompanhamento.Medicoes.Select(medicao => new MedicaoGravada
            {
                Mes = medicao.Mes,
                Itens =
                [
                    .. medicao.Itens.Select(item => new LancamentoGravado
                    {
                        Item = item.Item.Valor,
                        Quantidade = item.Quantidade,
                        CustoReal = item.CustoReal.Valor,
                    }),
                ],
            }),
        ],
    };

    /// <remarks>
    /// Cada medição é registrada de novo, na ordem, pelas regras de sempre:
    /// medição fora de ordem, item que não existe ou quantidade além do
    /// contratado são recusados na leitura como seriam na escrita.
    /// </remarks>
    public Acompanhamento ParaAcompanhamento(AcompanhamentoId id, LinhaDeBase linhaDeBase)
    {
        var acompanhamento = new Acompanhamento(id, linhaDeBase);

        foreach (var medicao in Medicoes)
        {
            acompanhamento.RegistrarMedicao(
                medicao.Mes,
                medicao.Itens.Select(item => new LancamentoDeMedicao(item.Item, item.Quantidade, item.CustoReal)));
        }

        // Registrar de novo as medições gravadas não é registrá-las de novo:
        // os eventos delas já foram publicados quando aconteceram (ADR-0016).
        acompanhamento.DescartarEventos();

        return acompanhamento;
    }
}

internal sealed record MedicaoGravada
{
    public required DateOnly Mes { get; init; }

    public required IReadOnlyList<LancamentoGravado> Itens { get; init; }
}

internal sealed record LancamentoGravado
{
    public required string Item { get; init; }

    public required decimal Quantidade { get; init; }

    public required decimal CustoReal { get; init; }
}
