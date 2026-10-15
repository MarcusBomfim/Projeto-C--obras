using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlanejamentoObras.Infraestrutura.Testes.Persistencia;
using PlanejamentoObras.Processamento.Testes.Apoio;

namespace PlanejamentoObras.Processamento.Testes;

/*
 * O processamento num host de verdade, como no PlanejamentoObras.Processador:
 * os serviços em segundo plano rodando, acordando com a mensagem nova, e
 * sobrevivendo ao banco fora do ar.
 */
public sealed class HostDoProcessadorTests(BancoDeTestes banco) : TesteComFila(banco)
{
    private static IHost NovoHost(string conexao, RegistroEmMemoria registro, TimeSpan intervaloDaFila)
    {
        var construtor = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        construtor.Services.AddLogging(log => log.AddProvider(registro).SetMinimumLevel(LogLevel.Debug));
        construtor.Services.AdicionarProcessamento(conexao, opcoes => opcoes.IntervaloDaFila = intervaloDaFila);

        return construtor.Build();
    }

    [Fact]
    public async Task O_host_trata_a_mensagem_assim_que_ela_e_gravada()
    {
        using var registro = new RegistroEmMemoria();
        using var host = NovoHost(Banco.ConexaoDoProcessador, registro, intervaloDaFila: TimeSpan.FromMinutes(10));
        await host.StartAsync(Cancelamento);

        await Ate(
            () => Task.FromResult(registro.Quantas("Fila em dia") > 0),
            TimeSpan.FromSeconds(15),
            "o processador terminar a primeira volta");

        // O host usa o relógio do sistema; a obra também, para a mensagem
        // estar na hora dele.
        var obras = new ObrasDaEmpresa(Banco, Empresa, TimeProvider.System);
        var acompanhamento = await obras.Gravar();

        // A volta seguinte é daqui a dez minutos: se a linha aparece antes,
        // foi o aviso de mensagem nova que acordou o processador.
        await Ate(
            async () => await obras.Painel(acompanhamento.Id) is not null,
            TimeSpan.FromSeconds(15),
            "a obra aparecer no painel");

        await host.StopAsync(Cancelamento);
    }

    [Fact]
    public async Task Banco_fora_do_ar_nao_derruba_o_host()
    {
        using var registro = new RegistroEmMemoria();
        using var host = NovoHost(EsperaPorMensagensTests.BancoForaDoAr, registro, intervaloDaFila: TimeSpan.FromMilliseconds(100));
        await host.StartAsync(Cancelamento);

        await Ate(
            () => Task.FromResult(registro.Quantas("A volta do processador") >= 2),
            TimeSpan.FromSeconds(30),
            "o processador tentar de novo depois de falhar");

        Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested);
        Assert.Equal(2, registro.Quantas("A rotina"));

        await host.StopAsync(Cancelamento);
    }
}
