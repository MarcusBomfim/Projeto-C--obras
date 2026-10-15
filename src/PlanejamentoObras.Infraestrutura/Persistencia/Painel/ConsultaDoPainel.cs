using Microsoft.EntityFrameworkCore;
using PlanejamentoObras.Dominio.ValorAgregado;

namespace PlanejamentoObras.Infraestrutura.Persistencia.Painel;

/// <summary>
/// Lê o painel das obras da empresa: uma linha por obra acompanhada, pronta
/// — sem abrir documento nem calcular nada.
/// </summary>
/// <remarks>
/// O painel é atualizado em segundo plano, e pode estar alguns instantes
/// atrás da última medição. Cada linha diz quando foi atualizada e de que
/// versão do acompanhamento veio.
/// </remarks>
public sealed class ConsultaDoPainel(ContextoDoBanco contexto)
{
    /// <summary>As obras da empresa, em ordem de nome.</summary>
    public async Task<IReadOnlyList<PainelDaObra>> Listar(CancellationToken cancelamento = default)
    {
        var linhas = await contexto.Paineis
            .AsNoTracking()
            .OrderBy(linha => linha.Obra)
            .ThenBy(linha => linha.AcompanhamentoId)
            .ToListAsync(cancelamento);

        return [.. linhas.Select(PainelDaObra.De)];
    }

    public async Task<PainelDaObra?> Obter(Guid acompanhamento, CancellationToken cancelamento = default)
    {
        var linha = await contexto.Paineis
            .AsNoTracking()
            .SingleOrDefaultAsync(registro => registro.AcompanhamentoId == acompanhamento, cancelamento);

        return linha is null ? null : PainelDaObra.De(linha);
    }
}

/// <summary>A situação de uma obra na última medição, como o painel a mostra.</summary>
public sealed record PainelDaObra(
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
    internal static PainelDaObra De(RegistroDoPainel linha) =>
        new(
            linha.AcompanhamentoId,
            linha.LinhaDeBaseId,
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
