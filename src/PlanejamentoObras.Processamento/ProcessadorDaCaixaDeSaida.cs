using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.CaixaDeSaida;

namespace PlanejamentoObras.Processamento;

/// <summary>
/// Trata a caixa de saída, uma mensagem por transação: reserva a próxima,
/// entrega aos tratadores do tipo dela, em nome da empresa dela, e marca a
/// mensagem como tratada — tudo na mesma transação (ADR-0017).
/// </summary>
/// <remarks>
/// <para>
/// O que o tratador grava e a marca de tratada são confirmados juntos: ou a
/// mensagem foi tratada e o efeito ficou, ou nenhum dos dois.
/// </para>
/// <para>
/// Se um tratador falha, o que ele gravou é desfeito até o ponto anterior ao
/// tratamento — a mensagem continua reservada —, a falha é registrada nela, e
/// ela volta depois de uma espera que dobra a cada falha. Tentativas demais,
/// e a fila desiste dela.
/// </para>
/// </remarks>
public sealed partial class ProcessadorDaCaixaDeSaida(IServiceScopeFactory escopos, ILogger<ProcessadorDaCaixaDeSaida> registro)
{
    private const string AntesDoTratamento = "antes_do_tratamento";

    /// <summary>Trata as mensagens pendentes até não sobrar nenhuma pronta.</summary>
    /// <returns>Quantas foram tratadas — com sucesso ou não.</returns>
    public async Task<int> ProcessarPendentes(CancellationToken cancelamento = default)
    {
        var tratadas = 0;

        while (await ProcessarUma(cancelamento))
        {
            tratadas++;
        }

        return tratadas;
    }

    /// <returns>Se havia uma mensagem pronta para tratar.</returns>
    public async Task<bool> ProcessarUma(CancellationToken cancelamento = default)
    {
        await using var escopo = escopos.CreateAsyncScope();
        var servicos = escopo.ServiceProvider;
        var contexto = servicos.GetRequiredService<ContextoDoBanco>();
        var fila = servicos.GetRequiredService<FilaDaCaixaDeSaida>();

        await using var transacao = await contexto.Database.BeginTransactionAsync(cancelamento);

        if (await fila.ReservarProxima(cancelamento) is not { } mensagem)
        {
            return false;
        }

        await transacao.CreateSavepointAsync(AntesDoTratamento, cancelamento);

        try
        {
            await Despachante.Despachar(servicos, mensagem.Ler(), cancelamento);
            await fila.MarcarTratada(mensagem.Id, cancelamento);
            Tratada(registro, mensagem.Id, mensagem.Tipo);
        }
        catch (Exception erro) when (!cancelamento.IsCancellationRequested)
        {
            // Um tratador falhar não pode parar a fila — nem um tempo esgotado
            // lá dentro, que também chega como cancelamento: só o host parando
            // interrompe o tratamento. O que o tratador gravou sai, e a falha
            // fica registrada na mensagem, que volta mais tarde.
            await transacao.RollbackToSavepointAsync(AntesDoTratamento, cancelamento);
            contexto.ChangeTracker.Clear();

            if (await fila.RegistrarFalha(mensagem, erro, cancelamento))
            {
                Abandonada(registro, erro, mensagem.Id, mensagem.Tipo, mensagem.Tentativas + 1);
            }
            else
            {
                Falhou(registro, erro, mensagem.Id, mensagem.Tipo, mensagem.Tentativas + 1);
            }
        }

        await transacao.CommitAsync(cancelamento);

        return true;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "A mensagem {Mensagem} ({Tipo}) foi tratada.")]
    private static partial void Tratada(ILogger registro, Guid mensagem, string tipo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A mensagem {Mensagem} ({Tipo}) falhou na tentativa {Tentativa}.")]
    private static partial void Falhou(ILogger registro, Exception erro, Guid mensagem, string tipo, int tentativa);

    [LoggerMessage(Level = LogLevel.Error, Message = "A mensagem {Mensagem} ({Tipo}) falhou {Tentativas} vezes e foi abandonada: alguém precisa ver por quê.")]
    private static partial void Abandonada(ILogger registro, Exception erro, Guid mensagem, string tipo, int tentativas);
}
