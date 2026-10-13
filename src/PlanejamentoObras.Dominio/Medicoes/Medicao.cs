namespace PlanejamentoObras.Dominio.Medicoes;

/// <summary>
/// A medição de um mês, registrada: o que cada item executou e custou nele.
/// </summary>
/// <remarks>
/// Registrada, não muda mais — como o boletim que já foi assinado e pago.
/// Erro numa medição antiga se corrige com glosa numa medição nova; só a
/// última pode ser desfeita, antes de a seguinte existir (ADR-0008).
/// </remarks>
public sealed class Medicao
{
    internal Medicao(int numero, DateOnly mes, IEnumerable<ItemDaMedicao> itens)
    {
        Numero = numero;
        Mes = mes;
        Itens = Array.AsReadOnly<ItemDaMedicao>([.. itens]);
    }

    /// <summary>1 para a primeira medição da obra, 2 para a segunda, e assim por diante.</summary>
    public int Numero { get; }

    /// <summary>O mês de competência, como o primeiro dia dele.</summary>
    public DateOnly Mes { get; }

    /// <summary>Só os itens lançados; os outros não executaram nem custaram nada no mês.</summary>
    public IReadOnlyList<ItemDaMedicao> Itens { get; }
}
