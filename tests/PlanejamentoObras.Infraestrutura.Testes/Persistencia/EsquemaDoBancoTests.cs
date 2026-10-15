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

    private async Task<List<T>> Consultar<T>(string sql, Func<NpgsqlDataReader, T> linha, params object[] parametros)
    {
        await using var conexao = new NpgsqlConnection(banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(sql, conexao);

        for (var i = 0; i < parametros.Length; i++)
        {
            comando.Parameters.AddWithValue($"p{i}", parametros[i]);
        }

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

    private async Task InserirAcompanhamento(Guid empresa, Guid linhaDeBase, Guid? id = null) =>
        await Executar(
            """
            insert into planejamento.acompanhamentos (id, empresa_id, linha_de_base_id, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, @p1, @p2, 1, 1, @p3::jsonb, now(), now())
            """,
            id ?? Guid.CreateVersion7(),
            empresa,
            linhaDeBase,
            DocumentoVazio);

    private async Task InserirNoPainel(Guid empresa, Guid acompanhamento, Guid linhaDeBase, string situacao) =>
        await Executar(
            """
            insert into planejamento.painel_das_obras (
                acompanhamento_id, empresa_id, linha_de_base_id, obra, situacao,
                orcamento_no_termino, duracao_planejada_em_meses, versao_do_acompanhamento, atualizado_em)
            values (@p0, @p1, @p2, 'LB', @p3, 1000, 2, 1, now())
            """,
            acompanhamento,
            empresa,
            linhaDeBase,
            situacao);

    /// <summary>Uma linha de base e um acompanhamento medido contra ela, da empresa do teste.</summary>
    private async Task<(Guid LinhaDeBase, Guid Acompanhamento)> UmaObraAcompanhada()
    {
        var linhaDeBase = Guid.CreateVersion7();
        var acompanhamento = Guid.CreateVersion7();
        await InserirLinhaDeBase(_empresa, linhaDeBase);
        await InserirAcompanhamento(_empresa, linhaDeBase, acompanhamento);

        return (linhaDeBase, acompanhamento);
    }

    [Fact]
    public async Task As_migracoes_criam_uma_tabela_por_agregado_e_as_de_apoio()
    {
        var tabelas = await Consultar(
            "select table_name from information_schema.tables where table_schema = 'planejamento' order by table_name",
            leitor => leitor.GetString(0));

        Assert.Equal(
            [
                "acompanhamentos", "bases_de_custos", "caixa_de_saida", "chaves_de_idempotencia", "cronogramas",
                "historico_de_migracoes", "linhas_de_base", "orcamentos", "painel_das_obras",
            ],
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

        Assert.Equal(8, tabelas.Count);
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

    [Fact]
    public async Task O_banco_recusa_mensagem_que_nao_e_objeto_json()
    {
        var erro = await Recusado(
            """
            insert into planejamento.caixa_de_saida (id, empresa_id, tipo, formato, conteudo, agregado_id, versao_do_agregado, ocorrida_em, tentativas, proxima_tentativa_em)
            values (@p0, @p1, 'medicao-registrada', 1, '"texto"', @p2, 1, now(), 0, now())
            """,
            Guid.CreateVersion7(),
            _empresa,
            Guid.CreateVersion7());

        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_caixa_de_saida_conteudo"), (erro.SqlState, erro.ConstraintName));
    }

    [Fact]
    public async Task O_painel_so_aceita_as_situacoes_conhecidas()
    {
        var (linhaDeBase, acompanhamento) = await UmaObraAcompanhada();

        var erro = await Assert.ThrowsAsync<PostgresException>(() => InserirNoPainel(_empresa, acompanhamento, linhaDeBase, "Otima"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_painel_das_obras_situacao"), (erro.SqlState, erro.ConstraintName));
    }

    [Fact]
    public async Task O_painel_so_mostra_acompanhamento_da_mesma_empresa()
    {
        var (linhaDeBase, acompanhamento) = await UmaObraAcompanhada();

        var erro = await Assert.ThrowsAsync<PostgresException>(() =>
            InserirNoPainel(Guid.CreateVersion7(), acompanhamento, linhaDeBase, "EmDia"));

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_painel_das_obras_acompanhamentos"), (erro.SqlState, erro.ConstraintName));
    }

    [Fact]
    public async Task Saiu_o_acompanhamento_sai_a_linha_dele_no_painel()
    {
        var (linhaDeBase, acompanhamento) = await UmaObraAcompanhada();
        await InserirNoPainel(_empresa, acompanhamento, linhaDeBase, "EmDia");

        await Executar("delete from planejamento.acompanhamentos where id = @p0", acompanhamento);

        Assert.Empty(await Consultar(
            "select 1 from planejamento.painel_das_obras where acompanhamento_id = @p0",
            leitor => leitor.GetInt32(0),
            acompanhamento));
    }

    // ---------- as funções que atravessam empresas (ADR-0017) ----------

    private static readonly string[] FuncoesQueAtravessamEmpresas =
    [
        "planejamento.reservar_mensagens(integer, timestamptz)",
        "planejamento.apagar_mensagens_tratadas_antes_de(timestamptz)",
        "planejamento.apagar_chaves_de_idempotencia_antes_de(timestamptz)",
    ];

    [Fact]
    public async Task As_funcoes_que_atravessam_empresas_sao_so_do_processador()
    {
        var privilegios = await Consultar(
            """
            select f.assinatura,
                   has_function_privilege(@p1, f.assinatura, 'execute'),
                   has_function_privilege(@p2, f.assinatura, 'execute'),
                   has_function_privilege('public', f.assinatura, 'execute')
            from unnest(@p0) as f(assinatura)
            """,
            leitor => (Funcao: leitor.GetString(0), Aplicacao: leitor.GetBoolean(1), Processador: leitor.GetBoolean(2), Publico: leitor.GetBoolean(3)),
            FuncoesQueAtravessamEmpresas,
            BancoDeTestes.PapelDaAplicacao,
            BancoDeTestes.PapelDoProcessador);

        Assert.Equal(FuncoesQueAtravessamEmpresas.Length, privilegios.Count);
        Assert.All(privilegios, funcao => Assert.Equal((false, true, false), (funcao.Aplicacao, funcao.Processador, funcao.Publico)));
    }

    [Fact]
    public async Task A_aplicacao_nao_reserva_mensagens()
    {
        await using var conexao = new NpgsqlConnection(banco.ConexaoDaAplicacao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand("select * from planejamento.reservar_mensagens(1, now())", conexao);

        var erro = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(Cancelamento));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, erro.SqlState);
    }

    [Fact]
    public async Task So_as_tres_rodam_como_o_dono_e_com_o_search_path_fixo()
    {
        var funcoes = await Consultar(
            """
            select p.proname, coalesce(array_to_string(p.proconfig, ';'), '')
            from pg_proc p join pg_namespace n on n.oid = p.pronamespace
            where n.nspname = 'planejamento' and p.prosecdef
            order by p.proname
            """,
            leitor => (Nome: leitor.GetString(0), Configuracao: leitor.GetString(1)));

        // Só as três: função nova que passe por cima da segurança por linha
        // tem de entrar nesta lista de propósito.
        Assert.Equal(
            ["apagar_chaves_de_idempotencia_antes_de", "apagar_mensagens_tratadas_antes_de", "reservar_mensagens"],
            funcoes.Select(funcao => funcao.Nome));
        Assert.All(funcoes, funcao => Assert.Equal("search_path=planejamento, pg_temp", funcao.Configuracao));
    }

    [Fact]
    public async Task A_reserva_entrega_so_o_id_e_a_empresa_da_mensagem()
    {
        // O conteúdo, quem reservou lê depois, em nome da empresa da mensagem.
        var resultado = await Consultar(
            "select pg_get_function_result('planejamento.reservar_mensagens(integer, timestamptz)'::regprocedure)",
            leitor => leitor.GetString(0));

        Assert.Equal(["TABLE(id uuid, empresa_id uuid)"], resultado);
    }
}
