using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlanejamentoObras.Api.Testes;
using PlanejamentoObras.Infraestrutura.Testes.Persistencia;
using PlanejamentoObras.Processamento;

[assembly: AssemblyFixture(typeof(ApiDeTestes))]

namespace PlanejamentoObras.Api.Testes;

/// <summary>
/// A API inteira, em memória, contra o PostgreSQL dos testes — sobe uma vez
/// para o assembly. Cada teste pede um cliente com o token que quiser: de que
/// empresa, com que papéis. A empresa de cada teste é nova, então os testes
/// não se enxergam, como duas construtoras não se enxergam.
/// </summary>
/// <remarks>
/// Ao lado da API, o processador da caixa de saída, com o papel dele no banco,
/// como o host do processador o monta — parado: o teste que precisa do
/// painel em dia pede para tratar a fila.
/// </remarks>
public sealed class ApiDeTestes : IAsyncLifetime
{
    private readonly BancoDeTestes _banco = new();

    private Fabrica? _fabrica;

    private ServiceProvider? _processamento;

    public BancoDeTestes Banco => _banco;

    public IServiceProvider Servicos => Api.Services;

    private Fabrica Api
    {
        get
        {
            Assert.SkipWhen(_fabrica is null, _banco.MotivoDaIndisponibilidade);

            return _fabrica!;
        }
    }

    public async ValueTask InitializeAsync()
    {
        await _banco.InitializeAsync();

        if (_banco.Disponivel)
        {
            _fabrica = new Fabrica(_banco.ConexaoDaAplicacao);
            _processamento = new ServiceCollection()
                .AddLogging()
                .AdicionarProcessamento(_banco.ConexaoDoProcessador)
                .BuildServiceProvider();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_processamento is not null)
        {
            await _processamento.DisposeAsync();
        }

        if (_fabrica is not null)
        {
            await _fabrica.DisposeAsync();
        }

        await _banco.DisposeAsync();
    }

    /// <summary>
    /// Trata a caixa de saída, como o processador trataria, até a condição
    /// valer. Até, e não uma vez: os testes rodam em paralelo, e outro teste
    /// pode estar tratando, naquele instante, a mensagem que este espera.
    /// </summary>
    public async Task ProcessarAte(Func<Task<bool>> condicao, string descricao)
    {
        ArgumentNullException.ThrowIfNull(condicao);
        Assert.SkipWhen(_processamento is null, _banco.MotivoDaIndisponibilidade);

        var processador = _processamento!.GetRequiredService<ProcessadorDaCaixaDeSaida>();
        var cancelamento = TestContext.Current.CancellationToken;
        var limite = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (true)
        {
            await processador.ProcessarPendentes(cancelamento);

            if (await condicao())
            {
                return;
            }

            Assert.True(DateTime.UtcNow < limite, $"Passaram 30 segundos de fila tratada, e ainda não: {descricao}.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancelamento);
        }
    }

    /// <summary>Um cliente com o token de alguém da empresa, com os papéis dados.</summary>
    public HttpClient Cliente(Guid empresa, params string[] papeis) => ClienteComToken(Tokens.De(empresa, papeis));

    public HttpClient ClienteComToken(string token)
    {
        var cliente = Anonimo();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return cliente;
    }

    public HttpClient Anonimo() => Api.CreateClient();

    /// <summary>
    /// A API com algo a mais — outra configuração, outro serviço —, num host
    /// só dela e contra o mesmo banco. Quem pede, descarta.
    /// </summary>
    public WebApplicationFactory<Program> Variante(Action<IWebHostBuilder> ajuste) => Api.WithWebHostBuilder(ajuste);

    /// <summary>A API configurada para os testes: o banco do contêiner e os tokens emitidos aqui.</summary>
    private sealed class Fabrica(string conexao) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testes");
            builder.UseSetting("ConnectionStrings:Planejamento", conexao);
            builder.ConfigureTestServices(servicos =>
                servicos.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opcoes =>
                {
                    opcoes.TokenValidationParameters.ValidIssuer = Tokens.Emissor;
                    opcoes.TokenValidationParameters.ValidAudience = Tokens.Audiencia;
                    opcoes.TokenValidationParameters.IssuerSigningKey = Tokens.Chave;
                }));
        }
    }
}
