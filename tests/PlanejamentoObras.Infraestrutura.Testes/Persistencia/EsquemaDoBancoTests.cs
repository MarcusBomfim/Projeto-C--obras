using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlanejamentoObras.Infraestrutura.Persistencia;

namespace PlanejamentoObras.Infraestrutura.Testes.Persistencia;

/*
 * As migrações aplicadas num PostgreSQL de verdade, e o banco cumprindo o que
 * prometem: as tabelas, as restrições e a chave estrangeira. As inserções
 * aqui são SQL direto, de propósito — é o banco que está sendo testado, não
 * o código que normalmente escreve nele.
 */
public sealed class EsquemaDoBancoTests(BancoDeTestes banco)
{
    private const string DocumentoVazio = """{"medicoes":[]}""";

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private async Task<int> Executar(string sql, params object[] parametros)
    {
        await using var conexao = new NpgsqlConnection(banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(sql, conexao);

        for (var i = 0; i < parametros.Length; i++)
        {
            comando.Parameters.AddWithValue($"p{i}", parametros[i]);
        }

        return await comando.ExecuteNonQueryAsync(Cancelamento);
    }

    private async Task<PostgresException> Recusado(string sql, params object[] parametros) =>
        await Assert.ThrowsAsync<PostgresException>(() => Executar(sql, parametros));

    private async Task InserirLinhaDeBase(Guid id) =>
        await Executar(
            """
            insert into planejamento.linhas_de_base (id, nome, aprovada_em, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, 'LB', date '2026-10-01', 1, 1, '{"nome":"LB"}', now(), now())
            """,
            id);

    [Fact]
    public async Task As_migracoes_criam_uma_tabela_por_agregado()
    {
        await using var conexao = new NpgsqlConnection(banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(
            "select table_name from information_schema.tables where table_schema = 'planejamento' order by table_name",
            conexao);
        await using var leitor = await comando.ExecuteReaderAsync(Cancelamento);

        var tabelas = new List<string>();

        while (await leitor.ReadAsync(Cancelamento))
        {
            tabelas.Add(leitor.GetString(0));
        }

        Assert.Equal(
            ["acompanhamentos", "bases_de_custos", "cronogramas", "historico_de_migracoes", "linhas_de_base", "orcamentos"],
            tabelas);
    }

    [Fact]
    public async Task Nao_ha_migracao_pendente_no_banco()
    {
        await using var contexto = banco.NovoContexto();

        Assert.Empty(await contexto.Database.GetPendingMigrationsAsync(Cancelamento));
    }

    [Fact]
    public async Task O_banco_recusa_versao_e_formato_que_nao_sao_positivos()
    {
        var versao = await Recusado(
            """
            insert into planejamento.cronogramas (id, nome, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, 'C', 0, 1, '{}', now(), now())
            """,
            Guid.CreateVersion7());

        var formato = await Recusado(
            """
            insert into planejamento.cronogramas (id, nome, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, 'C', 1, 0, '{}', now(), now())
            """,
            Guid.CreateVersion7());

        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_cronogramas_versao"), (versao.SqlState, versao.ConstraintName));
        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_cronogramas_formato"), (formato.SqlState, formato.ConstraintName));
    }

    [Fact]
    public async Task O_banco_recusa_documento_que_nao_e_objeto_json()
    {
        var erro = await Recusado(
            """
            insert into planejamento.orcamentos (id, nome, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, 'O', 1, 1, '[1, 2, 3]', now(), now())
            """,
            Guid.CreateVersion7());

        Assert.Equal("ck_orcamentos_documento", erro.ConstraintName);
    }

    [Fact]
    public async Task Acompanhamento_precisa_de_uma_linha_de_base_que_exista()
    {
        var erro = await Recusado(
            """
            insert into planejamento.acompanhamentos (id, linha_de_base_id, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, @p1, 1, 1, @p2::jsonb, now(), now())
            """,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            DocumentoVazio);

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_acompanhamentos_linhas_de_base"), (erro.SqlState, erro.ConstraintName));
    }

    [Fact]
    public async Task Linha_de_base_em_uso_nao_pode_ser_apagada()
    {
        var linhaDeBase = Guid.CreateVersion7();
        await InserirLinhaDeBase(linhaDeBase);
        await Executar(
            """
            insert into planejamento.acompanhamentos (id, linha_de_base_id, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, @p1, 1, 1, @p2::jsonb, now(), now())
            """,
            Guid.CreateVersion7(),
            linhaDeBase,
            DocumentoVazio);

        var erro = await Recusado("delete from planejamento.linhas_de_base where id = @p0", linhaDeBase);

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, erro.SqlState);
    }

    [Fact]
    public async Task Linha_de_base_sem_acompanhamento_pode_ser_apagada()
    {
        var linhaDeBase = Guid.CreateVersion7();
        await InserirLinhaDeBase(linhaDeBase);

        Assert.Equal(1, await Executar("delete from planejamento.linhas_de_base where id = @p0", linhaDeBase));
    }
}
