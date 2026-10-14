using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Npgsql;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;

namespace PlanejamentoObras.Infraestrutura.Persistencia;

/// <summary>
/// O que todo repositório faz com a sua tabela: ler, inserir, atualizar com
/// concorrência otimista, apagar — e traduzir o que o PostgreSQL responde para
/// as exceções da persistência.
/// </summary>
/// <remarks>
/// <para>
/// Atualizar e apagar são um comando SQL só, com a versão no <c>where</c>:
/// <c>update ... where id = @id and versao = @lida</c>. Se outro gravou no
/// meio, nenhuma linha muda, e aí uma consulta separa "mudou" de "sumiu". Não
/// há janela entre conferir a versão e gravar (ADR-0012).
/// </para>
/// <para>
/// Nada fica no rastreador de mudanças do Entity Framework: leitura sem
/// rastreio, atualização e remoção direto no banco. O contexto não guarda
/// estado entre uma operação e outra.
/// </para>
/// </remarks>
/// <param name="agregado">Como o agregado se chama numa frase: "o cronograma", "a linha de base".</param>
internal sealed class TabelaDeAgregados<TRegistro>(ContextoDoBanco contexto, TimeProvider relogio, string agregado)
    where TRegistro : RegistroDeAgregado
{
    private DbSet<TRegistro> Registros => contexto.Set<TRegistro>();

    public DateTimeOffset Agora => relogio.GetUtcNow();

    public Task<TRegistro?> Ler(Guid id, CancellationToken cancelamento) =>
        Registros.AsNoTracking().SingleOrDefaultAsync(registro => registro.Id == id, cancelamento);

    /// <summary>Refaz o agregado; documento que não se lê vira <see cref="DocumentoInvalido"/>, com o id.</summary>
    public T Abrir<T>(TRegistro registro, Func<TRegistro, T> leitura)
    {
        try
        {
            return leitura(registro);
        }
        catch (Exception erro) when (erro is JsonException or ExcecaoDeDominio or InvalidOperationException)
        {
            throw new DocumentoInvalido($"{Maiuscula(agregado)} {registro.Id} está gravado de um jeito que não se lê: {erro.Message}", erro);
        }
    }

    /// <param name="semReferencia">A mensagem, se o registro apontar para um agregado que não está gravado.</param>
    public async Task<int> Inserir(TRegistro registro, CancellationToken cancelamento, string? semReferencia = null)
    {
        Registros.Add(registro);

        try
        {
            await contexto.SaveChangesAsync(cancelamento);
        }
        catch (DbUpdateException erro) when (Violacao(erro) == PostgresErrorCodes.UniqueViolation)
        {
            throw new AgregadoJaExiste($"Já existe {agregado} {registro.Id}.", erro);
        }
        catch (DbUpdateException erro) when (Violacao(erro) == PostgresErrorCodes.ForeignKeyViolation && semReferencia is not null)
        {
            throw new AgregadoNaoEncontrado(semReferencia, erro);
        }
        finally
        {
            contexto.ChangeTracker.Clear();
        }

        return registro.Versao;
    }

    /// <param name="outrasColunas">As colunas próprias da tabela, além das que toda tabela de agregado tem.</param>
    public async Task<int> Atualizar(
        TRegistro novo,
        int versaoLida,
        Action<UpdateSettersBuilder<TRegistro>>? outrasColunas,
        CancellationToken cancelamento)
    {
        var alteradas = await Registros
            .Where(registro => registro.Id == novo.Id && registro.Versao == versaoLida)
            .ExecuteUpdateAsync(
                colunas =>
                {
                    colunas
                        .SetProperty(registro => registro.Versao, novo.Versao)
                        .SetProperty(registro => registro.Formato, novo.Formato)
                        .SetProperty(registro => registro.Documento, novo.Documento)
                        .SetProperty(registro => registro.AtualizadoEm, novo.AtualizadoEm);

                    outrasColunas?.Invoke(colunas);
                },
                cancelamento);

        if (alteradas == 0)
        {
            await AcusarConflitoOuAusencia(novo.Id, versaoLida, cancelamento);
        }

        return novo.Versao;
    }

    /// <param name="emUso">A mensagem, se outro agregado depender deste.</param>
    public async Task Apagar(Guid id, int versaoLida, CancellationToken cancelamento, string? emUso = null)
    {
        int apagadas;

        try
        {
            apagadas = await Registros
                .Where(registro => registro.Id == id && registro.Versao == versaoLida)
                .ExecuteDeleteAsync(cancelamento);
        }
        catch (Exception erro) when (Violacao(erro) == PostgresErrorCodes.ForeignKeyViolation && emUso is not null)
        {
            throw new AgregadoEmUso(emUso, erro);
        }

        if (apagadas == 0)
        {
            await AcusarConflitoOuAusencia(id, versaoLida, cancelamento);
        }
    }

    private async Task AcusarConflitoOuAusencia(Guid id, int versaoLida, CancellationToken cancelamento)
    {
        var atual = await Registros
            .Where(registro => registro.Id == id)
            .Select(registro => (int?)registro.Versao)
            .SingleOrDefaultAsync(cancelamento);

        throw atual is null
            ? new AgregadoNaoEncontrado($"Não existe {agregado} {id}.")
            : new ConflitoDeConcorrencia(
                $"{Maiuscula(agregado)} {id} mudou desde a versão {versaoLida}: está na versão {atual}. Leia de novo e refaça a alteração.");
    }

    /// <summary>O código de erro do PostgreSQL por trás de uma exceção, se houver.</summary>
    private static string? Violacao(Exception erro) =>
        (erro as PostgresException ?? erro.InnerException as PostgresException)?.SqlState;

    private static string Maiuscula(string texto) => string.Concat(char.ToUpperInvariant(texto[0]).ToString(), texto[1..]);
}

/// <summary>A listagem das tabelas que têm nome: em ordem de nome, sem abrir documento.</summary>
internal static class ListagemPorNome
{
    public static async Task<IReadOnlyList<ResumoDoAgregado<TId>>> Listar<TRegistro, TId>(
        this DbSet<TRegistro> registros,
        Func<Guid, TId> id,
        CancellationToken cancelamento)
        where TRegistro : RegistroComNome
        where TId : Identificador
    {
        var linhas = await registros
            .AsNoTracking()
            .OrderBy(registro => registro.Nome)
            .ThenBy(registro => registro.Id)
            .Select(registro => new { registro.Id, registro.Nome, registro.Versao, registro.AtualizadoEm })
            .ToListAsync(cancelamento);

        return [.. linhas.Select(linha => new ResumoDoAgregado<TId>(id(linha.Id), linha.Nome, linha.Versao, linha.AtualizadoEm))];
    }
}
