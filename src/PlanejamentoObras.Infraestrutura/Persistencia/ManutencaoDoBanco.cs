using Microsoft.EntityFrameworkCore;

namespace PlanejamentoObras.Infraestrutura.Persistencia;

/// <summary>
/// A limpeza do que perdeu a serventia: chaves de idempotência vencidas e
/// mensagens já tratadas (ADR-0014, ADR-0017).
/// </summary>
/// <remarks>
/// A limpeza atravessa empresas, e por isso passa por funções do banco que
/// fazem só isto — apagar o que é anterior a uma data —, como a reserva da
/// fila. Mensagem abandonada não sai: espera alguém olhar o porquê.
/// </remarks>
public sealed class ManutencaoDoBanco(ContextoDoBanco contexto)
{
    /// <returns>Quantas chaves foram apagadas.</returns>
    public Task<int> ApagarChavesDeIdempotenciaAntesDe(DateTimeOffset limite, CancellationToken cancelamento = default) =>
        contexto.Database
            .SqlQuery<int>($"select planejamento.apagar_chaves_de_idempotencia_antes_de({limite}) as \"Value\"")
            .SingleAsync(cancelamento);

    /// <returns>Quantas mensagens foram apagadas.</returns>
    public Task<int> ApagarMensagensTratadasAntesDe(DateTimeOffset limite, CancellationToken cancelamento = default) =>
        contexto.Database
            .SqlQuery<int>($"select planejamento.apagar_mensagens_tratadas_antes_de({limite}) as \"Value\"")
            .SingleAsync(cancelamento);
}
