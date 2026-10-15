using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlanejamentoObras.Infraestrutura.Mensagens;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Processamento.Hospedagem;
using PlanejamentoObras.Processamento.Rotinas;
using PlanejamentoObras.Processamento.Tratadores;

namespace PlanejamentoObras.Processamento.Testes;

public sealed class ServicosDeProcessamentoTests
{
    private const string Conexao = "Host=localhost;Database=planejamento_obras";

    [Fact]
    public async Task Registra_os_tratadores_as_rotinas_e_os_servicos_que_os_rodam()
    {
        // Montar não abre conexão: dá para conferir sem banco.
        await using var provedor = new ServiceCollection()
            .AddLogging()
            .AdicionarProcessamento(Conexao)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var escopo = provedor.CreateAsyncScope();
        var servicos = escopo.ServiceProvider;

        Assert.IsType<AtualizacaoDoPainel>(Assert.Single(servicos.GetServices<ITratadorDe<MensagemDeAcompanhamentoIniciado>>()));
        Assert.IsType<AtualizacaoDoPainel>(Assert.Single(servicos.GetServices<ITratadorDe<MensagemDeMedicaoRegistrada>>()));
        Assert.IsType<AtualizacaoDoPainel>(Assert.Single(servicos.GetServices<ITratadorDe<MensagemDeMedicaoDesfeita>>()));
        Assert.Equal(
            [typeof(LimpezaDaCaixaDeSaida), typeof(LimpezaDasChavesDeIdempotencia)],
            provedor.GetServices<IRotina>().Select(rotina => rotina.GetType()).OrderBy(tipo => tipo.Name));
        Assert.Equal(
            [typeof(ServicoDaCaixaDeSaida), typeof(ServicoDasRotinas)],
            provedor.GetServices<IHostedService>().Select(servico => servico.GetType()));
    }

    [Fact]
    public async Task Cada_escopo_comeca_sem_empresa_e_assume_uma_so()
    {
        await using var provedor = new ServiceCollection().AddLogging().AdicionarProcessamento(Conexao).BuildServiceProvider();
        await using var um = provedor.CreateAsyncScope();
        await using var outro = provedor.CreateAsyncScope();
        var empresa = Guid.CreateVersion7();

        var assumida = um.ServiceProvider.GetRequiredService<EmpresaAssumida>();
        assumida.Assumir(empresa);

        Assert.Same(assumida, um.ServiceProvider.GetRequiredService<IEmpresaAtual>());
        Assert.Null(outro.ServiceProvider.GetRequiredService<IEmpresaAtual>().Id);
    }
}
