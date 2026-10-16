using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.CaixaDeSaida;
using PlanejamentoObras.Infraestrutura.Persistencia.Painel;
using PlanejamentoObras.Processamento.Hospedagem;
using PlanejamentoObras.Processamento.Rotinas;

namespace PlanejamentoObras.Processamento;

/// <summary>O registro do processamento em segundo plano na injeção de dependência.</summary>
public static class ServicosDeProcessamento
{
    /// <summary>
    /// A persistência em nome da empresa de cada mensagem, o processador da
    /// caixa de saída, os tratadores e as rotinas — e os serviços que os rodam
    /// enquanto o host estiver no ar. A verificação de que a fila está girando,
    /// <see cref="FilaGirando"/>, fica para o host registrar, com as outras
    /// verificações de saúde dele.
    /// </summary>
    /// <remarks>
    /// Tratadores e rotinas são encontrados no assembly, como os recursos da
    /// API: um tratador novo entra sem mexer neste registro.
    /// </remarks>
    public static IServiceCollection AdicionarProcessamento(
        this IServiceCollection servicos,
        string conexao,
        Action<OpcoesDoProcessamento>? configurar = null)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        ArgumentException.ThrowIfNullOrWhiteSpace(conexao);

        // Cada escopo começa sem empresa e assume a da mensagem que reservou.
        servicos.AdicionarPersistencia<EmpresaAssumida>(conexao);
        servicos.AddScoped(provedor => (EmpresaAssumida)provedor.GetRequiredService<IEmpresaAtual>());

        servicos.AddScoped<FilaDaCaixaDeSaida>();
        servicos.AddScoped<ProjecaoDoPainel>();
        servicos.AddScoped<ManutencaoDoBanco>();
        servicos.AddSingleton<ProcessadorDaCaixaDeSaida>();

        // As métricas da fila e o pulso que diz se ela está girando (ADR-0019).
        servicos.AddMetrics();
        servicos.AddSingleton<MetricasDaCaixaDeSaida>();
        servicos.AddSingleton<PulsoDaFila>();
        servicos.AddSingleton(provedor => new EsperaPorMensagens(conexao, provedor.GetRequiredService<ILogger<EsperaPorMensagens>>()));

        foreach (var tipo in typeof(ServicosDeProcessamento).Assembly.GetTypes().Where(tipo => tipo is { IsClass: true, IsAbstract: false }))
        {
            foreach (var tratador in tipo.GetInterfaces().Where(EhTratador))
            {
                servicos.AddScoped(tratador, tipo);
            }

            if (tipo.IsAssignableTo(typeof(IRotina)))
            {
                servicos.AddSingleton(typeof(IRotina), tipo);
            }
        }

        servicos.AddOptions<OpcoesDoProcessamento>().Configure(configurar ?? (_ => { }));
        servicos.AddHostedService<ServicoDaCaixaDeSaida>();
        servicos.AddHostedService<ServicoDasRotinas>();

        return servicos;
    }

    private static bool EhTratador(Type contrato) =>
        contrato.IsGenericType && contrato.GetGenericTypeDefinition() == typeof(ITratadorDe<>);
}
