using Microsoft.Extensions.DependencyInjection;
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

    public AmbienteDoProcessador(BancoDeTestes banco, TimeProvider relogio, Action<IServiceCollection>? registrar = null)
    {
        ArgumentNullException.ThrowIfNull(banco);

        var servicos = new ServiceCollection()
            .AddLogging()
            .AddSingleton(relogio);

        servicos.AdicionarProcessamento(banco.ConexaoDoProcessador);
        registrar?.Invoke(servicos);

        _provedor = servicos.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public ProcessadorDaCaixaDeSaida Processador => _provedor.GetRequiredService<ProcessadorDaCaixaDeSaida>();

    public AsyncServiceScope NovoEscopo() => _provedor.CreateAsyncScope();

    public ValueTask DisposeAsync() => _provedor.DisposeAsync();
}
