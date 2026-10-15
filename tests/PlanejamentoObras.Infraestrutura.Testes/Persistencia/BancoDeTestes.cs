using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlanejamentoObras.Infraestrutura.Persistencia;
using Testcontainers.PostgreSql;

namespace PlanejamentoObras.Infraestrutura.Testes.Persistencia;

/// <summary>
/// Um PostgreSQL de verdade, num contêiner, para o assembly de testes
/// inteiro: sobe uma vez, recebe as migrações, e cada teste trabalha com
/// dados próprios — empresa e ids novos —, sem precisar limpar o banco.
/// </summary>
/// <remarks>
/// <para>
/// PostgreSQL de verdade, e não SQLite nem o provedor em memória: o que está
/// sendo testado é justamente o que só o PostgreSQL faz — <c>jsonb</c>,
/// restrições de verificação, chave estrangeira, segurança por linha, os
/// códigos de erro que ele devolve (ADR-0011).
/// </para>
/// <para>
/// Três conexões. A do dono do banco, superusuário, aplica as migrações e
/// serve aos testes que examinam o banco por dentro. A da aplicação usa um
/// papel comum, como em produção: é ela que a segurança por linha alcança
/// (ADR-0013). A do processador também é comum, e é a única que executa as
/// funções que atravessam empresas — reservar mensagens, limpar o que
/// venceu (ADR-0017).
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

    /// <summary>O papel da aplicação: entra, lê e grava, mas não é superusuário nem passa por cima da segurança por linha.</summary>
    public const string PapelDaAplicacao = "planejamento_aplicacao";

    private const string SenhaDaAplicacao = "aplicacao-nos-testes";

    /// <summary>O papel do processador: como o da aplicação, e mais as funções da fila e da limpeza.</summary>
    public const string PapelDoProcessador = "planejamento_processador";

    private const string SenhaDoProcessador = "processador-nos-testes";

    private PostgreSqlContainer? _conteiner;

    private string? _conexao;

    private string? _conexaoDaAplicacao;

    private string? _conexaoDoProcessador;

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

        await using (var contexto = ContextoDoBanco.Criar(_conexao, EmpresaFixa.Nenhuma))
        {
            await contexto.Database.MigrateAsync();
        }

        await CriarPapeis(_conexao);

        _conexaoDoProcessador = ConexaoComo(PapelDoProcessador, SenhaDoProcessador);
        _conexaoDaAplicacao = ConexaoComo(PapelDaAplicacao, SenhaDaAplicacao);
    }

    public async ValueTask DisposeAsync()
    {
        if (_conteiner is not null)
        {
            await _conteiner.DisposeAsync();
        }
    }

    /// <summary>Se o banco subiu. Sem Docker fora do CI, não sobe.</summary>
    public bool Disponivel => _conexaoDaAplicacao is not null;

    /// <summary>Por que o banco não subiu — o motivo de pular os testes que precisam dele.</summary>
    public string MotivoDaIndisponibilidade => _indisponivel ?? "O banco de testes não subiu.";

    /// <summary>A conexão do dono do banco — superusuário, que passa por cima da segurança por linha. Pula o teste se não houver banco.</summary>
    public string Conexao
    {
        get
        {
            Assert.SkipWhen(_conexao is null, _indisponivel ?? "O banco de testes não subiu.");

            return _conexao!;
        }
    }

    /// <summary>A conexão da aplicação, com o papel comum que a segurança por linha alcança.</summary>
    public string ConexaoDaAplicacao
    {
        get
        {
            Assert.SkipWhen(_conexaoDaAplicacao is null, _indisponivel ?? "O banco de testes não subiu.");

            return _conexaoDaAplicacao!;
        }
    }

    /// <summary>A conexão do processador: a da aplicação, e mais as funções que atravessam empresas.</summary>
    public string ConexaoDoProcessador
    {
        get
        {
            Assert.SkipWhen(_conexaoDoProcessador is null, _indisponivel ?? "O banco de testes não subiu.");

            return _conexaoDoProcessador!;
        }
    }

    /// <summary>Um contexto novo em nome da empresa, como cada requisição da aplicação terá o seu.</summary>
    public ContextoDoBanco NovoContexto(Guid empresa) => ContextoDoBanco.Criar(ConexaoDaAplicacao, new EmpresaFixa(empresa));

    /// <summary>Um contexto em nome de nenhuma empresa: não lê nem grava agregado.</summary>
    public ContextoDoBanco NovoContextoSemEmpresa() => ContextoDoBanco.Criar(ConexaoDaAplicacao, EmpresaFixa.Nenhuma);

    private string ConexaoComo(string papel, string senha) =>
        new NpgsqlConnectionStringBuilder(_conexao) { Username = papel, Password = senha }.ConnectionString;

    /// <summary>Os papéis como quem implanta os criaria: as tabelas para os dois, as funções só para o processador.</summary>
    private static async Task CriarPapeis(string conexaoDoDono)
    {
        await using var conexao = new NpgsqlConnection(conexaoDoDono);
        await conexao.OpenAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            create role {PapelDaAplicacao} login password '{SenhaDaAplicacao}';
            create role {PapelDoProcessador} login password '{SenhaDoProcessador}';
            grant usage on schema planejamento to {PapelDaAplicacao}, {PapelDoProcessador};
            grant select, insert, update, delete on all tables in schema planejamento to {PapelDaAplicacao}, {PapelDoProcessador};
            grant execute on function
                planejamento.reservar_mensagens(integer, timestamptz),
                planejamento.apagar_mensagens_tratadas_antes_de(timestamptz),
                planejamento.apagar_chaves_de_idempotencia_antes_de(timestamptz)
                to {PapelDoProcessador};
            """,
            conexao);
        await comando.ExecuteNonQueryAsync();
    }
}
