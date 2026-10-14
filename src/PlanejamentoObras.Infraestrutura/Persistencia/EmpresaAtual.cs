namespace PlanejamentoObras.Infraestrutura.Persistencia;

/// <summary>
/// A empresa em nome de quem o contexto lê e grava. Cada construtora só
/// enxerga as suas obras (ADR-0013).
/// </summary>
/// <remarks>
/// Quem hospeda a persistência diz qual é: a API tira do token de quem fez o
/// pedido; os testes e as ferramentas usam uma <see cref="EmpresaFixa"/>. Sem
/// empresa — <see langword="null"/> —, toda consulta volta vazia e nada se
/// grava.
/// </remarks>
public interface IEmpresaAtual
{
    Guid? Id { get; }
}

/// <summary>Uma empresa só, a mesma o tempo todo: para testes, ferramentas e tarefas em nome de uma empresa.</summary>
public sealed record EmpresaFixa : IEmpresaAtual
{
    public EmpresaFixa(Guid? id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A empresa não pode ser o Guid vazio: para nenhuma empresa, use EmpresaFixa.Nenhuma.", nameof(id));
        }

        Id = id;
    }

    /// <summary>Nenhuma empresa: para quem não lê nem grava agregados — o dotnet-ef gerando migrações, por exemplo.</summary>
    public static EmpresaFixa Nenhuma { get; } = new(id: null);

    public Guid? Id { get; }
}
