using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// Uma linha da planilha calculada: uma <see cref="LinhaDeEtapa"/> ou uma
/// <see cref="LinhaDeItem"/>.
/// </summary>
/// <param name="CustoTotal">O custo direto da linha, sem BDI.</param>
/// <param name="Total">O preço da linha, com BDI.</param>
/// <param name="Peso">Quanto a linha é do total do orçamento: 0,25 quer dizer 25 %.</param>
public abstract record LinhaDoOrcamento(
    NumeroDaEap Numero,
    string Descricao,
    Dinheiro CustoTotal,
    Dinheiro Total,
    decimal Peso)
{
    public int Nivel => Numero.Nivel;
}
