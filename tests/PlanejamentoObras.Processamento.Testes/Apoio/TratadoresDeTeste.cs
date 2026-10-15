using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanejamentoObras.Infraestrutura.Mensagens;
using PlanejamentoObras.Infraestrutura.Persistencia;

namespace PlanejamentoObras.Processamento.Testes.Apoio;

/*
 * Tratadores que só existem nos testes, para ver o processador por dentro:
 * um que anota o que recebeu e em nome de quem, um que falha, um que demora,
 * um que esgota o tempo, um que para o host. Entram depois dos tratadores do
 * processamento — o do painel trata antes deles.
 */

public static class TratadoresDeTeste
{
    /// <summary>Registra o tratador para todas as mensagens que ele trata, depois dos que já estão registrados.</summary>
    public static IServiceCollection TratarTambemCom<TTratador>(this IServiceCollection servicos)
        where TTratador : class
    {
        foreach (var contrato in typeof(TTratador).GetInterfaces()
                     .Where(contrato => contrato.IsGenericType && contrato.GetGenericTypeDefinition() == typeof(ITratadorDe<>)))
        {
            servicos.AddScoped(contrato, typeof(TTratador));
        }

        return servicos;
    }
}

/// <summary>Uma mensagem recebida, com a empresa em nome de quem foi tratada — no código e no banco.</summary>
public sealed record Anotacao(object Mensagem, Guid? Empresa, string EmpresaNoBanco);

/// <summary>O que os tratadores que anotam receberam, na ordem em que receberam.</summary>
public sealed class Anotacoes
{
    private readonly ConcurrentQueue<Anotacao> _anotacoes = new();

    public IReadOnlyList<Anotacao> Todas => [.. _anotacoes];

    public void Anotar(Anotacao anotacao) => _anotacoes.Enqueue(anotacao);
}

public sealed class TratadorQueAnota(Anotacoes anotacoes, IEmpresaAtual empresa, ContextoDoBanco contexto)
    : ITratadorDe<MensagemDeAcompanhamentoIniciado>,
      ITratadorDe<MensagemDeMedicaoRegistrada>,
      ITratadorDe<MensagemDeMedicaoDesfeita>
{
    public Task Tratar(MensagemDeAcompanhamentoIniciado mensagem, CancellationToken cancelamento) => Anotar(mensagem, cancelamento);

    public Task Tratar(MensagemDeMedicaoRegistrada mensagem, CancellationToken cancelamento) => Anotar(mensagem, cancelamento);

    public Task Tratar(MensagemDeMedicaoDesfeita mensagem, CancellationToken cancelamento) => Anotar(mensagem, cancelamento);

    private async Task Anotar(object mensagem, CancellationToken cancelamento)
    {
        // A empresa que a segurança por linha vê, na transação do tratamento.
        var noBanco = await contexto.Database
            .SqlQuery<string>($"select current_setting('planejamento.empresa_id') as \"Value\"")
            .SingleAsync(cancelamento);

        anotacoes.Anotar(new Anotacao(mensagem, empresa.Id, noBanco));
    }
}

/// <summary>Quantas vezes o tratador que falha ainda vai falhar.</summary>
public sealed class Falhas(int vezes)
{
    private int _restantes = vezes;

    public bool Falhar() => Interlocked.Decrement(ref _restantes) >= 0;
}

public sealed class TratadorQueFalha(Falhas falhas) : ITratadorDe<MensagemDeAcompanhamentoIniciado>
{
    public Task Tratar(MensagemDeAcompanhamentoIniciado mensagem, CancellationToken cancelamento) =>
        falhas.Falhar() ? throw new InvalidOperationException("Falha de propósito.") : Task.CompletedTask;
}

/// <summary>Segura a mensagem reservada um pouco: dá tempo de outro processador tentar pegá-la.</summary>
public sealed class TratadorDemorado : ITratadorDe<MensagemDeAcompanhamentoIniciado>
{
    public Task Tratar(MensagemDeAcompanhamentoIniciado mensagem, CancellationToken cancelamento) =>
        Task.Delay(TimeSpan.FromMilliseconds(20), cancelamento);
}

/// <summary>Um serviço externo que não respondeu a tempo: o cliente HTTP avisa com um cancelamento.</summary>
public sealed class TratadorQueEsgotaOTempo : ITratadorDe<MensagemDeAcompanhamentoIniciado>
{
    public Task Tratar(MensagemDeAcompanhamentoIniciado mensagem, CancellationToken cancelamento) =>
        throw new TaskCanceledException("O serviço externo não respondeu a tempo.");
}

/// <summary>O host parando no meio do tratamento.</summary>
public sealed class TratadorQueCancela(CancellationTokenSource parar) : ITratadorDe<MensagemDeAcompanhamentoIniciado>
{
    public async Task Tratar(MensagemDeAcompanhamentoIniciado mensagem, CancellationToken cancelamento)
    {
        await parar.CancelAsync();
        cancelamento.ThrowIfCancellationRequested();
    }
}
