using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.ValorAgregado;

/// <summary>Os indicadores de uma etapa ou de um item: onde, na obra, está o atraso ou o estouro.</summary>
public sealed record LinhaDoValorAgregado(NumeroDaEap Numero, string Descricao, IndicadoresDeValorAgregado Indicadores)
{
    public int Nivel => Numero.Nivel;
}
