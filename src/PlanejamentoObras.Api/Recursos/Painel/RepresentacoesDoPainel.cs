using PlanejamentoObras.Dominio.ValorAgregado;
using PlanejamentoObras.Infraestrutura.Persistencia.Painel;

namespace PlanejamentoObras.Api.Recursos.Painel;

/// <summary>
/// Uma obra no painel: a situação dela e os indicadores da última medição, a
/// custo. Sem medição, os indicadores vêm nulos; sem base para um índice —
/// nada planejado ou nada gasto até o mês —, o índice vem nulo.
/// </summary>
/// <param name="Obra">O nome da obra no painel: o da linha de base contra a qual ela é medida.</param>
/// <param name="VersaoDoAcompanhamento">A versão do acompanhamento de que a linha foi feita.</param>
public sealed record ObraNoPainelResposta(
    Guid Acompanhamento,
    Guid LinhaDeBase,
    string Obra,
    SituacaoDaObra Situacao,
    int? Medicao,
    DateOnly? Mes,
    decimal OrcamentoNoTermino,
    decimal? ValorPlanejado,
    decimal? ValorAgregado,
    decimal? CustoReal,
    decimal? IndiceDeDesempenhoDePrazo,
    decimal? IndiceDeDesempenhoDeCusto,
    decimal? EstimativaNoTermino,
    decimal? VariacaoNoTermino,
    int DuracaoPlanejadaEmMeses,
    decimal? PrazoAgregadoEmMeses,
    decimal? DuracaoEstimadaEmMeses,
    int VersaoDoAcompanhamento,
    DateTimeOffset AtualizadoEm)
{
    public static ObraNoPainelResposta De(PainelDaObra linha)
    {
        ArgumentNullException.ThrowIfNull(linha);

        return new(
            linha.Acompanhamento,
            linha.LinhaDeBase,
            linha.Obra,
            linha.Situacao,
            linha.Medicao,
            linha.Mes,
            linha.OrcamentoNoTermino,
            linha.ValorPlanejado,
            linha.ValorAgregado,
            linha.CustoReal,
            linha.IndiceDeDesempenhoDePrazo,
            linha.IndiceDeDesempenhoDeCusto,
            linha.EstimativaNoTermino,
            linha.VariacaoNoTermino,
            linha.DuracaoPlanejadaEmMeses,
            linha.PrazoAgregadoEmMeses,
            linha.DuracaoEstimadaEmMeses,
            linha.VersaoDoAcompanhamento,
            linha.AtualizadoEm);
    }
}
