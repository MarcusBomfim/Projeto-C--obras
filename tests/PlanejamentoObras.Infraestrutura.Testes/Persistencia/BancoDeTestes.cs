using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Testes.Persistencia;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(BancoDeTestes))]

namespace PlanejamentoObras.Infraestrutura.Testes.Persistencia;

/// <summary>
/// Um PostgreSQL de verdade, num contêiner, para o assembly de testes
/// inteiro: sobe uma vez, recebe as migrações, e cada teste trabalha com
/// dados próprios — ids novos —, sem precisar limpar o banco.
/// </summary>
/// <remarks>
/// <para>
/// PostgreSQL de verdade, e não SQLite nem o provedor em memória: o que está
/// sendo testado é justamente o que só o PostgreSQL faz — <c>jsonb</c>,
/// restrições de verificação, chave estrangeira, os códigos de erro que ele
/// devolve (ADR-0011).
/// </para>
/// <para>
/// Sem Docker na máquina de quem desenvolve, os testes que precisam do banco
/// são pulados, com o motivo. No CI não: lá, Docker indisponível é falha —
/// teste de integração que some sem ninguém ver é pior do que teste que
/// falha.
/// </para>
/// </remarks>
public sealed class BancoDeTestes : IAsyncLifetime
{
    /// <summary>Versão fixa: o banco dos testes não muda sozinho de uma execução para outra.</summary>
    public const string Imagem = "postgres:17.6-alpine";

    private PostgreSqlContainer? _conteiner;

    private string? _conexao;

    private string? _indisponivel;

    private static bool EmIntegracaoContinua =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    public async ValueTask InitializeAsync()
    {
        try
        {
            _conteiner = new PostgreSqlBuilder(Imagem).Build();
            await _conteiner.StartAsync();
        }
        catch (DockerUnavailableException erro) when (!EmIntegracaoContinua)
        {
            _indisponivel = $"Docker indisponível nesta máquina; os testes com PostgreSQL foram pulados. ({erro.Message})";

            return;
        }

        _conexao = _conteiner.GetConnectionString();

        await using var contexto = NovoContexto();
        await contexto.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_conteiner is not null)
        {
            await _conteiner.DisposeAsync();
        }
    }

    /// <summary>A cadeia de conexão do banco dos testes; pula o teste se não houver banco.</summary>
    public string Conexao
    {
        get
        {
            Assert.SkipWhen(_conexao is null, _indisponivel ?? "O banco de testes não subiu.");

            return _conexao!;
        }
    }

    /// <summary>Um contexto novo, como cada requisição da aplicação terá o seu.</summary>
    public ContextoDoBanco NovoContexto() => ContextoDoBanco.Criar(Conexao);
}
