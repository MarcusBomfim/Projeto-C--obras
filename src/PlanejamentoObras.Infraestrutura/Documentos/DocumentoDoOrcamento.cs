using System.Text.Json.Serialization;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Infraestrutura.Documentos;

/// <summary>
/// Como um orçamento é gravado: a árvore da EAP, as quantidades e o BDI.
/// Preços e totais são derivados, calculados contra uma base de custos.
/// </summary>
/// <remarks>
/// A numeração da EAP não vai para o documento: ela é a posição, e a ordem
/// das listas já a guarda.
/// </remarks>
internal sealed record DocumentoDoOrcamento
{
    public const short Formato = 1;

    public required string Nome { get; init; }

    public required BdiGravado Bdi { get; init; }

    public required IReadOnlyList<EtapaGravada> Etapas { get; init; }

    public static DocumentoDoOrcamento De(Orcamento orcamento) => new()
    {
        Nome = orcamento.Nome,
        Bdi = BdiGravado.De(orcamento.Bdi),
        Etapas = [.. orcamento.Etapas.Select(EtapaGravada.De)],
    };

    /// <remarks>
    /// Refeito pelos métodos de sempre do orçamento, a partir do construtor
    /// com a identidade gravada: cada etapa e cada item passam pelas regras.
    /// </remarks>
    public Orcamento ParaOrcamento(OrcamentoId id)
    {
        var orcamento = new Orcamento(id, Nome, Bdi.ParaBdi());

        foreach (var etapa in Etapas)
        {
            Incluir(orcamento, etapa, dentroDe: null);
        }

        return orcamento;
    }

    private static void Incluir(Orcamento orcamento, NoGravado no, string? dentroDe)
    {
        switch (no)
        {
            case EtapaGravada etapa:
                var numero = dentroDe is null ? orcamento.AdicionarEtapa(etapa.Titulo) : orcamento.AdicionarEtapa(etapa.Titulo, dentroDe);

                foreach (var filho in etapa.Filhos)
                {
                    Incluir(orcamento, filho, numero.Valor);
                }

                break;

            case ItemDoOrcamentoGravado item when dentroDe is not null:
                var posicao = orcamento.AdicionarItem(dentroDe, item.Codigo, item.Quantidade).Valor;

                if (item.Atividade is not null)
                {
                    orcamento.VincularAtividade(posicao, item.Atividade);
                }

                if (item.BdiDiferenciado is not null)
                {
                    orcamento.DefinirBdiDiferenciado(posicao, item.BdiDiferenciado.ParaBdi());
                }

                break;

            default:
                throw new InvalidOperationException($"Linha de orçamento gravada que não é etapa nem item dentro de etapa: {no}.");
        }
    }
}

internal sealed record BdiGravado
{
    public required decimal Percentual { get; init; }

    /// <summary>Nulo quando o BDI foi informado pronto.</summary>
    public ParcelasGravadas? Parcelas { get; init; }

    public static BdiGravado De(Bdi bdi) => new()
    {
        Percentual = bdi.Percentual,
        Parcelas = bdi.Parcelas is { } parcelas
            ? new ParcelasGravadas
            {
                AdministracaoCentral = parcelas.AdministracaoCentral,
                Seguros = parcelas.Seguros,
                Riscos = parcelas.Riscos,
                Garantias = parcelas.Garantias,
                DespesasFinanceiras = parcelas.DespesasFinanceiras,
                Lucro = parcelas.Lucro,
                Tributos = parcelas.Tributos,
            }
            : null,
    };

    /// <summary>O BDI como foi gravado, sem recalcular a fórmula (ADR-0010).</summary>
    public Bdi ParaBdi() =>
        Dominio.Orcamentos.Bdi.Reconstituir(
            Percentual,
            Parcelas is { } parcelas
                ? new ParcelasDoBdi(
                    parcelas.AdministracaoCentral,
                    parcelas.Seguros,
                    parcelas.Riscos,
                    parcelas.Garantias,
                    parcelas.DespesasFinanceiras,
                    parcelas.Lucro,
                    parcelas.Tributos)
                : null);
}

internal sealed record ParcelasGravadas
{
    public required decimal AdministracaoCentral { get; init; }

    public required decimal Seguros { get; init; }

    public required decimal Riscos { get; init; }

    public required decimal Garantias { get; init; }

    public required decimal DespesasFinanceiras { get; init; }

    public required decimal Lucro { get; init; }

    public required decimal Tributos { get; init; }
}

/// <summary>Uma linha da EAP: etapa ou item, identificada no JSON por <c>"tipo"</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(EtapaGravada), "etapa")]
[JsonDerivedType(typeof(ItemDoOrcamentoGravado), "item")]
internal abstract record NoGravado
{
    public static NoGravado De(NoDaEap no) => no switch
    {
        Etapa etapa => EtapaGravada.De(etapa),
        ItemDoOrcamento item => new ItemDoOrcamentoGravado
        {
            Codigo = item.Codigo.Valor,
            Quantidade = item.Quantidade,
            Atividade = item.Atividade?.Valor,
            BdiDiferenciado = item.BdiDiferenciado is { } bdi ? BdiGravado.De(bdi) : null,
        },
        _ => throw new InvalidOperationException($"Linha de EAP de tipo desconhecido: {no.GetType().Name}."),
    };
}

internal sealed record EtapaGravada : NoGravado
{
    public required string Titulo { get; init; }

    public required IReadOnlyList<NoGravado> Filhos { get; init; }

    public static EtapaGravada De(Etapa etapa) => new()
    {
        Titulo = etapa.Titulo,
        Filhos = [.. etapa.Filhos.Select(NoGravado.De)],
    };
}

internal sealed record ItemDoOrcamentoGravado : NoGravado
{
    public required string Codigo { get; init; }

    public required decimal Quantidade { get; init; }

    public string? Atividade { get; init; }

    public BdiGravado? BdiDiferenciado { get; init; }
}
