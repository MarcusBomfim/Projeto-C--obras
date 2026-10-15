using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlanejamentoObras.Infraestrutura.Persistencia.Idempotencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Painel;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Infraestrutura.Persistencia;

/// <summary>O registro da persistência na injeção de dependência.</summary>
public static class ServicosDePersistencia
{
    /// <summary>
    /// O contexto, os repositórios, as chaves de idempotência e a leitura do
    /// painel, um por requisição; o relógio do sistema, se ninguém tiver
    /// registrado outro.
    /// </summary>
    /// <typeparam name="TEmpresa">
    /// De onde vem a empresa de cada requisição — na API, do token de quem
    /// pediu. Obrigatório: sem ele não há contexto (ADR-0013).
    /// </typeparam>
    public static IServiceCollection AdicionarPersistencia<TEmpresa>(this IServiceCollection servicos, string conexao)
        where TEmpresa : class, IEmpresaAtual
    {
        ArgumentNullException.ThrowIfNull(servicos);
        ArgumentException.ThrowIfNullOrWhiteSpace(conexao);

        servicos.AddScoped<IEmpresaAtual, TEmpresa>();
        servicos.AddDbContext<ContextoDoBanco>(opcoes => ContextoDoBanco.Configurar(opcoes, conexao));
        servicos.TryAddSingleton(TimeProvider.System);

        servicos.AddScoped<RepositorioDeCronogramas>();
        servicos.AddScoped<RepositorioDeBasesDeCustos>();
        servicos.AddScoped<RepositorioDeOrcamentos>();
        servicos.AddScoped<RepositorioDeLinhasDeBase>();
        servicos.AddScoped<RepositorioDeAcompanhamentos>();
        servicos.AddScoped<ChavesDeIdempotencia>();
        servicos.AddScoped<ConsultaDoPainel>();

        return servicos;
    }
}
