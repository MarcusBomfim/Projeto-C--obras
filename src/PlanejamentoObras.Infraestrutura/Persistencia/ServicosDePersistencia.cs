using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Infraestrutura.Persistencia;

/// <summary>O registro da persistência na injeção de dependência, para a API da Parte 5.</summary>
public static class ServicosDePersistencia
{
    /// <summary>
    /// O contexto e os repositórios, um por requisição; o relógio do sistema,
    /// se ninguém tiver registrado outro.
    /// </summary>
    public static IServiceCollection AdicionarPersistencia(this IServiceCollection servicos, string conexao)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        ArgumentException.ThrowIfNullOrWhiteSpace(conexao);

        servicos.AddDbContext<ContextoDoBanco>(opcoes => ContextoDoBanco.Configurar(opcoes, conexao));
        servicos.TryAddSingleton(TimeProvider.System);

        servicos.AddScoped<RepositorioDeCronogramas>();
        servicos.AddScoped<RepositorioDeBasesDeCustos>();
        servicos.AddScoped<RepositorioDeOrcamentos>();
        servicos.AddScoped<RepositorioDeLinhasDeBase>();
        servicos.AddScoped<RepositorioDeAcompanhamentos>();

        return servicos;
    }
}
