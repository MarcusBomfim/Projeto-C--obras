using Microsoft.Extensions.DependencyInjection;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Infraestrutura.Testes.Persistencia;

public sealed class ServicosDePersistenciaTests
{
    [Fact]
    public void Registra_o_contexto_e_os_repositorios_um_por_escopo()
    {
        // Montar não abre conexão: dá para conferir sem banco.
        using var provedor = new ServiceCollection()
            .AdicionarPersistencia("Host=localhost;Database=planejamento_obras")
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        using var escopo = provedor.CreateScope();
        var servicos = escopo.ServiceProvider;

        Assert.NotNull(servicos.GetRequiredService<RepositorioDeCronogramas>());
        Assert.NotNull(servicos.GetRequiredService<RepositorioDeBasesDeCustos>());
        Assert.NotNull(servicos.GetRequiredService<RepositorioDeOrcamentos>());
        Assert.NotNull(servicos.GetRequiredService<RepositorioDeLinhasDeBase>());
        Assert.NotNull(servicos.GetRequiredService<RepositorioDeAcompanhamentos>());
        Assert.Same(servicos.GetRequiredService<ContextoDoBanco>(), servicos.GetRequiredService<ContextoDoBanco>());
        Assert.Same(TimeProvider.System, servicos.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void Relogio_registrado_antes_e_respeitado()
    {
        var relogio = new RelogioParado();

        using var provedor = new ServiceCollection()
            .AddSingleton<TimeProvider>(relogio)
            .AdicionarPersistencia("Host=localhost;Database=planejamento_obras")
            .BuildServiceProvider();

        Assert.Same(relogio, provedor.GetRequiredService<TimeProvider>());
    }

    private sealed class RelogioParado : TimeProvider;
}
