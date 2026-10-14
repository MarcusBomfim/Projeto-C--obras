using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlanejamentoObras.Api.Testes;
using PlanejamentoObras.Infraestrutura.Testes.Persistencia;

[assembly: AssemblyFixture(typeof(ApiDeTestes))]

namespace PlanejamentoObras.Api.Testes;

/// <summary>
/// A API inteira, em memória, contra o PostgreSQL dos testes — sobe uma vez
/// para o assembly. Cada teste pede um cliente com o token que quiser: de que
/// empresa, com que papéis. A empresa de cada teste é nova, então os testes
/// não se enxergam, como duas construtoras não se enxergam.
/// </summary>
public sealed class ApiDeTestes : IAsyncLifetime
{
    private readonly BancoDeTestes _banco = new();

    private Fabrica? _fabrica;

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
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_fabrica is not null)
        {
            await _fabrica.DisposeAsync();
        }

        await _banco.DisposeAsync();
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
