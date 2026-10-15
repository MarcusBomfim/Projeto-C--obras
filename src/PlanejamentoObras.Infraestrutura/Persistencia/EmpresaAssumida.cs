namespace PlanejamentoObras.Infraestrutura.Persistencia;

/// <summary>
/// A empresa de uma tarefa em segundo plano. Começa nenhuma — a tarefa ainda
/// não sabe para quem vai trabalhar —, e passa a ser a da mensagem que ela
/// pegou da fila (ADR-0017).
/// </summary>
/// <remarks>
/// Uma por escopo, e uma empresa por escopo: assumir outra no meio do caminho
/// é engano, e é recusado.
/// </remarks>
public sealed class EmpresaAssumida : IEmpresaAtual
{
    public Guid? Id { get; private set; }

    public void Assumir(Guid empresa)
    {
        if (empresa == Guid.Empty)
        {
            throw new ArgumentException("A empresa não pode ser o Guid vazio.", nameof(empresa));
        }

        if (Id is { } atual && atual != empresa)
        {
            throw new InvalidOperationException($"Esta tarefa já trabalha em nome da empresa {atual}; uma tarefa, uma empresa.");
        }

        Id = empresa;
    }
}
