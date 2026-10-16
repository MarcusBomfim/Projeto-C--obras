using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using PlanejamentoObras.Infraestrutura.Testes.Persistencia;

namespace PlanejamentoObras.Processamento.Testes.Apoio;

/// <summary>
/// O processamento montado como o host o monta — com o papel do processador
/// no banco dos testes —, mas com o relógio do teste e, se o teste quiser,
/// tratadores a mais. Os serviços em segundo plano ficam registrados e
/// parados: o teste chama o processador quando quer.
/// </summary>
public sealed class AmbienteDoProcessador : IAsyncDisposable
{
    private readonly ServiceProvider _provedor;

    /// <param name="conexao">Outro banco no lugar do dos testes — um que não está no ar, por exemplo.</param>
    public AmbienteDoProcessador(
        BancoDeTestes banco,
        TimeProvider relogio,
        Action<IServiceCollection>? registrar = null,
        string? conexao = null)
    {
        ArgumentNullException.ThrowIfNull(banco);

        var servicos = new ServiceCollection()
            .AddLogging()
            .AddSingleton(relogio);

        servicos.AdicionarProcessamento(conexao ?? banco.ConexaoDoProcessador);
        registrar?.Invoke(servicos);

        _provedor = servicos.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public ProcessadorDaCaixaDeSaida Processador => _provedor.GetRequiredService<ProcessadorDaCaixaDeSaida>();

    public AsyncServiceScope NovoEscopo() => _provedor.CreateAsyncScope();

    /// <summary>Um serviço que o processamento não registra, montado com o que ele registra: a verificação de saúde, por exemplo.</summary>
    public T Montar<T>() => ActivatorUtilities.CreateInstance<T>(_provedor);

    /// <summary>
    /// O que um instrumento do processamento medir daqui em diante — só deste
    /// ambiente, e não de outro que esteja no ar no mesmo processo.
    /// </summary>
    public MetricCollector<T> Metrica<T>(string instrumento)
        where T : struct =>
        new(_provedor.GetRequiredService<IMeterFactory>(), TelemetriaDoProcessamento.Nome, instrumento);

    public ValueTask DisposeAsync() => _provedor.DisposeAsync();
}
