using Microsoft.Extensions.DependencyInjection;
using PlanejamentoObras.Infraestrutura.Testes.Persistencia;

namespace PlanejamentoObras.Processamento.Testes.Apoio;

/// <summary>
/// O começo de todo teste que mexe na fila: a caixa de saída vazia, uma
/// empresa nova e um relógio parado.
/// </summary>
/// <remarks>
/// Vazia porque a fila é uma só para o banco inteiro e reserva mensagem de
/// qualquer empresa: o que o teste reserva tem de ser o que ele mesmo gravou.
/// Pelo mesmo motivo, os testes deste assembly rodam um de cada vez.
/// </remarks>
public abstract class TesteComFila(BancoDeTestes banco) : IAsyncLifetime
{
    protected static readonly DateOnly Outubro = new(2026, 10, 1);

    protected static readonly DateOnly Novembro = new(2026, 11, 1);

    protected static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    protected BancoDeTestes Banco => banco;

    protected Guid Empresa { get; } = Guid.CreateVersion7();

    protected RelogioDeTeste Relogio { get; } = new();

    protected ObrasDaEmpresa Obras(Guid? empresa = null) => new(banco, empresa ?? Empresa, Relogio);

    protected AmbienteDoProcessador Processamento(Action<IServiceCollection>? registrar = null) => new(banco, Relogio, registrar);

    public async ValueTask InitializeAsync()
    {
        // Sem banco, o teste que precisa dele é pulado ao pedir a conexão.
        if (banco.Disponivel)
        {
            await CaixaNoBanco.Esvaziar(banco);
        }
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    /// <summary>Espera a condição valer, perguntando de tempos em tempos, até o limite.</summary>
    protected static async Task Ate(Func<Task<bool>> condicao, TimeSpan limite, string descricao)
    {
        ArgumentNullException.ThrowIfNull(condicao);

        var fim = DateTime.UtcNow + limite;

        while (!await condicao())
        {
            Assert.True(DateTime.UtcNow < fim, $"Passou {limite} e ainda não: {descricao}.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), Cancelamento);
        }
    }
}
