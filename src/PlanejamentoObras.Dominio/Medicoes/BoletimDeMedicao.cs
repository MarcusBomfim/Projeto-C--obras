using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Medicoes;

/// <summary>
/// O boletim de medição: o documento que diz quanto se executou no mês, a
/// preço de contrato — e, portanto, quanto se paga.
/// </summary>
public sealed class BoletimDeMedicao
{
    private readonly Dictionary<NumeroDaEap, LinhaDoBoletim> _porNumero;

    internal BoletimDeMedicao(Medicao medicao, IEnumerable<LinhaDoBoletim> linhas)
    {
        Numero = medicao.Numero;
        Mes = medicao.Mes;
        Linhas = Array.AsReadOnly<LinhaDoBoletim>([.. linhas]);
        _porNumero = Linhas.ToDictionary(linha => linha.Numero);

        var primeiroNivel = Linhas.Where(linha => linha.Nivel == 1).ToList();
        ValorContratado = primeiroNivel.Aggregate(Dinheiro.Zero, (soma, linha) => soma + linha.ValorContratado);
        ValorAnterior = primeiroNivel.Aggregate(Dinheiro.Zero, (soma, linha) => soma + linha.ValorAnterior);
        ValorNoMes = primeiroNivel.Aggregate(Dinheiro.Zero, (soma, linha) => soma + linha.ValorNoMes);
        ValorAcumulado = primeiroNivel.Aggregate(Dinheiro.Zero, (soma, linha) => soma + linha.ValorAcumulado);
    }

    /// <summary>O número da medição: "3ª medição".</summary>
    public int Numero { get; }

    public DateOnly Mes { get; }

    /// <summary>Etapas e itens, na ordem da planilha — todos, inclusive os que não andaram.</summary>
    public IReadOnlyList<LinhaDoBoletim> Linhas { get; }

    public Dinheiro ValorContratado { get; }

    public Dinheiro ValorAnterior { get; }

    /// <summary>O valor da medição: o que se paga no mês.</summary>
    public Dinheiro ValorNoMes { get; }

    public Dinheiro ValorAcumulado { get; }

    public Dinheiro Saldo => ValorContratado - ValorAcumulado;

    public decimal PercentualExecutado => ValorContratado == Dinheiro.Zero ? 0m : ValorAcumulado / ValorContratado;

    public LinhaDoBoletim Linha(string numero)
    {
        var chave = NumeroDaEap.Ler(numero);

        return _porNumero.TryGetValue(chave, out var linha)
            ? linha
            : throw new ExcecaoDeDominio($"Não existe a linha {chave} neste boletim.");
    }

    public ItemDoBoletim Item(string numero) =>
        Linha(numero) as ItemDoBoletim
        ?? throw new ExcecaoDeDominio($"A linha {numero} é uma etapa, e não um item.");
}
