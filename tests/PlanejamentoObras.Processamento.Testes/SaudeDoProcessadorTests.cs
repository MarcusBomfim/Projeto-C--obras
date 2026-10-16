using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlanejamentoObras.Infraestrutura.Testes.Persistencia;
using PlanejamentoObras.Processamento.Hospedagem;
using PlanejamentoObras.Processamento.Testes.Apoio;

namespace PlanejamentoObras.Processamento.Testes;

/*
 * O host do processador como o orquestrador o vê (ADR-0019): vivo enquanto a
 * fila gira, pronto enquanto, além disso, alcança o banco.
 */
public sealed class SaudeDoProcessadorTests(BancoDeTestes banco) : TesteComFila(banco)
{
    [Fact]
    public async Task Com_a_fila_girando_e_o_banco_no_ar_o_processador_esta_vivo_e_pronto()
    {
        await using var host = Host(Banco.ConexaoDoProcessador);
        using var cliente = host.CreateClient();

        Assert.Equal(
            (HttpStatusCode.OK, """{"situacao":"Healthy","verificacoes":{"fila":{"situacao":"Healthy"}}}"""),
            await Perguntar(cliente, "/saude/viva"));
        Assert.Equal(
            (HttpStatusCode.OK, """{"situacao":"Healthy","verificacoes":{"banco":{"situacao":"Healthy"},"fila":{"situacao":"Healthy"}}}"""),
            await Perguntar(cliente, "/saude/pronta"));
    }

    [Fact]
    public async Task A_fila_parada_tira_o_processador_de_vivo_e_de_pronto()
    {
        await using var host = Host(Banco.ConexaoDoProcessador);
        using var cliente = host.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await Perguntar(cliente, "/saude/viva")).Status);

        // Sem a fila rodando, ninguém mais bate o pulso: é a fila travada.
        Relogio.Adiantar(TimeSpan.FromMinutes(3));

        Assert.Equal(
            (HttpStatusCode.ServiceUnavailable, """{"situacao":"Unhealthy","verificacoes":{"fila":{"situacao":"Unhealthy","descricao":"A fila não dá sinal de vida há 180 segundos."}}}"""),
            await Perguntar(cliente, "/saude/viva"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Perguntar(cliente, "/saude/pronta")).Status);
    }

    [Fact]
    public async Task Sem_o_banco_o_processador_continua_vivo_mas_nao_esta_pronto()
    {
        await using var host = Host(EsperaPorMensagensTests.BancoForaDoAr);
        using var cliente = host.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await Perguntar(cliente, "/saude/viva")).Status);
        Assert.Equal(
            (HttpStatusCode.ServiceUnavailable, """{"situacao":"Unhealthy","verificacoes":{"banco":{"situacao":"Unhealthy","descricao":"O banco não respondeu."},"fila":{"situacao":"Healthy"}}}"""),
            await Perguntar(cliente, "/saude/pronta"));
    }

    /// <summary>
    /// O host como o Program.cs o monta, com o relógio do teste e sem a fila
    /// nem as rotinas rodando: o pulso só bate quando o teste quer.
    /// </summary>
    private WebApplicationFactory<Program> Host(string conexao) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Testes");
            web.UseSetting("ConnectionStrings:Planejamento", conexao);
            web.ConfigureTestServices(servicos =>
            {
                foreach (var emSegundoPlano in servicos.Where(Do<ServicoDaCaixaDeSaida>).Concat(servicos.Where(Do<ServicoDasRotinas>)).ToList())
                {
                    servicos.Remove(emSegundoPlano);
                }

                servicos.AddSingleton<TimeProvider>(Relogio);
            });
        });

    private static bool Do<TServico>(ServiceDescriptor registro) =>
        registro.ServiceType == typeof(IHostedService) && registro.ImplementationType == typeof(TServico);

    private static async Task<(HttpStatusCode Status, string Corpo)> Perguntar(HttpClient cliente, string caminho)
    {
        var resposta = await cliente.GetAsync(caminho, Cancelamento);

        return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync(Cancelamento));
    }
}
