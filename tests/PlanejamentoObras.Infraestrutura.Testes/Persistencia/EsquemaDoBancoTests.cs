using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PlanejamentoObras.Infraestrutura.Testes.Persistencia;

/*
 * As migrações aplicadas num PostgreSQL de verdade, e o banco cumprindo o que
 * prometem: as tabelas, as restrições, a chave estrangeira e a segurança por
 * linha. As inserções aqui são SQL direto, com o dono do banco, de propósito
 * — é o banco que está sendo testado, não o código que normalmente escreve
 * nele.
 */
public sealed class EsquemaDoBancoTests(BancoDeTestes banco)
{
    private const string DocumentoVazio = """{"medicoes":[]}""";

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _empresa = Guid.CreateVersion7();

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

    private async Task<List<T>> Consultar<T>(string sql, Func<NpgsqlDataReader, T> linha)
    {
        await using var conexao = new NpgsqlConnection(banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(sql, conexao);
        await using var leitor = await comando.ExecuteReaderAsync(Cancelamento);

        var linhas = new List<T>();

        while (await leitor.ReadAsync(Cancelamento))
        {
            linhas.Add(linha(leitor));
        }

        return linhas;
    }

    private async Task<PostgresException> Recusado(string sql, params object[] parametros) =>
        await Assert.ThrowsAsync<PostgresException>(() => Executar(sql, parametros));

    private async Task InserirLinhaDeBase(Guid empresa, Guid id) =>
        await Executar(
            """
            insert into planejamento.linhas_de_base (id, empresa_id, nome, aprovada_em, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, @p1, 'LB', date '2026-10-01', 1, 1, '{"nome":"LB"}', now(), now())
            """,
            id,
            empresa);

    private async Task InserirAcompanhamento(Guid empresa, Guid linhaDeBase) =>
        await Executar(
            """
            insert into planejamento.acompanhamentos (id, empresa_id, linha_de_base_id, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, @p1, @p2, 1, 1, @p3::jsonb, now(), now())
            """,
            Guid.CreateVersion7(),
            empresa,
            linhaDeBase,
            DocumentoVazio);

    [Fact]
    public async Task As_migracoes_criam_uma_tabela_por_agregado_e_a_das_chaves_de_idempotencia()
    {
        var tabelas = await Consultar(
            "select table_name from information_schema.tables where table_schema = 'planejamento' order by table_name",
            leitor => leitor.GetString(0));

        Assert.Equal(
            ["acompanhamentos", "bases_de_custos", "chaves_de_idempotencia", "cronogramas", "historico_de_migracoes", "linhas_de_base", "orcamentos"],
            tabelas);
    }

    [Fact]
    public async Task Nao_ha_migracao_pendente_no_banco()
    {
        await using var contexto = banco.NovoContextoSemEmpresa();

        Assert.Empty(await contexto.Database.GetPendingMigrationsAsync(Cancelamento));
    }

    [Fact]
    public async Task Toda_tabela_com_dados_de_empresa_tem_seguranca_por_linha_forcada()
    {
        /*
         * Tabela nova que esquecer a política fica aberta para todas as
         * empresas, sem erro nenhum. Este teste é o que lembra.
         */
        var tabelas = await Consultar(
            """
            select c.relname, c.relrowsecurity, c.relforcerowsecurity,
                   exists (select 1 from pg_policies p where p.schemaname = 'planejamento' and p.tablename = c.relname)
            from pg_class c join pg_namespace n on n.oid = c.relnamespace
            where n.nspname = 'planejamento' and c.relkind = 'r' and c.relname <> 'historico_de_migracoes'
            order by c.relname
            """,
            leitor => (Tabela: leitor.GetString(0), Ativada: leitor.GetBoolean(1), Forcada: leitor.GetBoolean(2), TemPolitica: leitor.GetBoolean(3)));

        Assert.Equal(6, tabelas.Count);
        Assert.All(tabelas, tabela => Assert.True(tabela.Ativada && tabela.Forcada && tabela.TemPolitica, $"{tabela.Tabela} sem segurança por linha."));
    }

    [Fact]
    public async Task O_banco_recusa_versao_e_formato_que_nao_sao_positivos()
    {
        var versao = await Recusado(
            """
            insert into planejamento.cronogramas (id, empresa_id, nome, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, @p1, 'C', 0, 1, '{}', now(), now())
            """,
            Guid.CreateVersion7(),
            _empresa);

        var formato = await Recusado(
            """
            insert into planejamento.cronogramas (id, empresa_id, nome, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, @p1, 'C', 1, 0, '{}', now(), now())
            """,
            Guid.CreateVersion7(),
            _empresa);

        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_cronogramas_versao"), (versao.SqlState, versao.ConstraintName));
        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_cronogramas_formato"), (formato.SqlState, formato.ConstraintName));
    }

    [Fact]
    public async Task O_banco_recusa_documento_que_nao_e_objeto_json()
    {
        var erro = await Recusado(
            """
            insert into planejamento.orcamentos (id, empresa_id, nome, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, @p1, 'O', 1, 1, '[1, 2, 3]', now(), now())
            """,
            Guid.CreateVersion7(),
            _empresa);

        Assert.Equal("ck_orcamentos_documento", erro.ConstraintName);
    }

    [Fact]
    public async Task Acompanhamento_precisa_de_uma_linha_de_base_que_exista()
    {
        var erro = await Assert.ThrowsAsync<PostgresException>(() => InserirAcompanhamento(_empresa, Guid.CreateVersion7()));

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_acompanhamentos_linhas_de_base"), (erro.SqlState, erro.ConstraintName));
    }

    [Fact]
    public async Task Acompanhamento_so_mede_contra_linha_de_base_da_mesma_empresa()
    {
        // A chave estrangeira não passa pela segurança por linha: é a chave
        // composta, com a empresa, que impede a referência a outra empresa.
        var linhaDeBaseDeOutra = Guid.CreateVersion7();
        await InserirLinhaDeBase(Guid.CreateVersion7(), linhaDeBaseDeOutra);

        var erro = await Assert.ThrowsAsync<PostgresException>(() => InserirAcompanhamento(_empresa, linhaDeBaseDeOutra));

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_acompanhamentos_linhas_de_base"), (erro.SqlState, erro.ConstraintName));
    }

    [Fact]
    public async Task Linha_de_base_em_uso_nao_pode_ser_apagada()
    {
        var linhaDeBase = Guid.CreateVersion7();
        await InserirLinhaDeBase(_empresa, linhaDeBase);
        await InserirAcompanhamento(_empresa, linhaDeBase);

        var erro = await Recusado("delete from planejamento.linhas_de_base where id = @p0", linhaDeBase);

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, erro.SqlState);
    }

    [Fact]
    public async Task Linha_de_base_sem_acompanhamento_pode_ser_apagada()
    {
        var linhaDeBase = Guid.CreateVersion7();
        await InserirLinhaDeBase(_empresa, linhaDeBase);

        Assert.Equal(1, await Executar("delete from planejamento.linhas_de_base where id = @p0", linhaDeBase));
    }
}
