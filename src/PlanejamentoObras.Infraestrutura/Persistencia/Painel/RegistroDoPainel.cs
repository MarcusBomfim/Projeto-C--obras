using PlanejamentoObras.Dominio.ValorAgregado;

namespace PlanejamentoObras.Infraestrutura.Persistencia.Painel;

/// <summary>
/// Uma linha do painel das obras: a situação de um acompanhamento na última
/// medição. É projeção — derivada do acompanhamento, refeita a cada fato
/// dele —, e não fonte da verdade (ADR-0017).
/// </summary>
internal sealed class RegistroDoPainel
{
    public Guid AcompanhamentoId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid LinhaDeBaseId { get; set; }

    /// <summary>O nome da obra: o da linha de base contra a qual ela é medida.</summary>
    public string Obra { get; set; } = string.Empty;

    public SituacaoDaObra Situacao { get; set; }

    public int? Medicao { get; set; }

    public DateOnly? Mes { get; set; }

    public decimal OrcamentoNoTermino { get; set; }

    public decimal? ValorPlanejado { get; set; }

    public decimal? ValorAgregado { get; set; }

    public decimal? CustoReal { get; set; }

    public decimal? IndiceDeDesempenhoDePrazo { get; set; }

    public decimal? IndiceDeDesempenhoDeCusto { get; set; }

    public decimal? EstimativaNoTermino { get; set; }

    public decimal? VariacaoNoTermino { get; set; }

    public int DuracaoPlanejadaEmMeses { get; set; }

    public decimal? PrazoAgregadoEmMeses { get; set; }

    public decimal? DuracaoEstimadaEmMeses { get; set; }

    /// <summary>A versão do acompanhamento de que a linha foi feita: uma mais velha nunca grava por cima.</summary>
    public int VersaoDoAcompanhamento { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }
}
