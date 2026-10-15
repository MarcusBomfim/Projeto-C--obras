using Microsoft.Extensions.DependencyInjection;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Idempotencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Painel;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Infraestrutura.Testes.Persistencia;

public sealed class ServicosDePersistenciaTests
{
    private const string Conexao = "Host=localhost;Database=planejamento_obras";

    [Fact]
    public void Registra_o_contexto_os_repositorios_as_chaves_e_o_painel_um_por_escopo()
    {
        // Montar não abre conexão: dá para conferir sem banco.
        using var provedor = new ServiceCollection()
            .AdicionarPersistencia<EmpresaDoTeste>(Conexao)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        using var escopo = provedor.CreateScope();
        var servicos = escopo.ServiceProvider;

        Assert.NotNull(servicos.GetRequiredService<RepositorioDeCronogramas>());
        Assert.NotNull(servicos.GetRequiredService<RepositorioDeBasesDeCustos>());
        Assert.NotNull(servicos.GetRequiredService<RepositorioDeOrcamentos>());
        Assert.NotNull(servicos.GetRequiredService<RepositorioDeLinhasDeBase>());
        Assert.NotNull(servicos.GetRequiredService<RepositorioDeAcompanhamentos>());
        Assert.NotNull(servicos.GetRequiredService<ChavesDeIdempotencia>());
        Assert.NotNull(servicos.GetRequiredService<ConsultaDoPainel>());
        Assert.Same(servicos.GetRequiredService<ContextoDoBanco>(), servicos.GetRequiredService<ContextoDoBanco>());
        Assert.Same(TimeProvider.System, servicos.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void O_contexto_trabalha_em_nome_da_empresa_de_cada_escopo()
    {
        using var provedor = new ServiceCollection().AdicionarPersistencia<EmpresaDoTeste>(Conexao).BuildServiceProvider();
        using var escopo = provedor.CreateScope();

        Assert.Equal(EmpresaDoTeste.Fixa, escopo.ServiceProvider.GetRequiredService<ContextoDoBanco>().EmpresaId);
    }

    [Fact]
    public void Relogio_registrado_antes_e_respeitado()
    {
        var relogio = new RelogioParado();

        using var provedor = new ServiceCollection()
            .AddSingleton<TimeProvider>(relogio)
            .AdicionarPersistencia<EmpresaDoTeste>(Conexao)
            .BuildServiceProvider();

        Assert.Same(relogio, provedor.GetRequiredService<TimeProvider>());
    }

    private sealed class RelogioParado : TimeProvider;

    private sealed class EmpresaDoTeste : IEmpresaAtual
    {
        public static readonly Guid Fixa = Guid.Parse("01992b6e-5d1c-7a3e-9f00-0000000000a1");

        public Guid? Id => Fixa;
    }
}
