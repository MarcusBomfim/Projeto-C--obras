namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// Uma linha da EAP: uma <see cref="Etapa"/>, que agrupa, ou um
/// <see cref="ItemDoOrcamento"/>, que é orçado.
/// </summary>
/// <remarks>
/// O construtor é <c>private protected</c>: só este assembly cria tipos de
/// linha, e quem percorre a EAP pode contar com só existirem os dois.
/// </remarks>
public abstract class NoDaEap
{
    private protected NoDaEap()
    {
    }
}
