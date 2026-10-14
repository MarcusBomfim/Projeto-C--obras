using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Infraestrutura.Testes.Persistencia;

/*
 * Duas construtoras no mesmo banco. Cada uma só enxerga o que é dela: pelo
 * filtro do Entity Framework e, por baixo dele, pela segurança por linha do
 * PostgreSQL, que vale mesmo para SQL escrito à mão (ADR-0013).
 */
public sealed class IsolamentoEntreEmpresasTests(BancoDeTestes banco)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _alfa = Guid.CreateVersion7();

    private readonly Guid _beta = Guid.CreateVersion7();

    private RepositorioDeCronogramas Cronogramas(Guid empresa) => new(banco.NovoContexto(empresa), TimeProvider.System);

    private static Cronograma UmCronograma()
    {
        var cronograma = new Cronograma("Casa");
        cronograma.AdicionarAtividade("FUN", "Fundações", 10);

        return cronograma;
    }

    private async Task<Cronograma> CronogramaDaAlfa()
    {
        var cronograma = UmCronograma();
        await Cronogramas(_alfa).Adicionar(cronograma, Cancelamento);

        return cronograma;
    }

    [Fact]
    public async Task Cada_empresa_so_enxerga_o_que_e_dela()
    {
        var daAlfa = await CronogramaDaAlfa();

        Assert.NotNull(await Cronogramas(_alfa).Obter(daAlfa.Id, Cancelamento));
        Assert.Null(await Cronogramas(_beta).Obter(daAlfa.Id, Cancelamento));
        Assert.Contains(await Cronogramas(_alfa).Listar(Cancelamento), resumo => resumo.Id == daAlfa.Id);
        Assert.Empty(await Cronogramas(_beta).Listar(Cancelamento));
    }

    [Fact]
    public async Task Uma_empresa_nao_altera_nem_apaga_o_que_e_de_outra()
    {
        var daAlfa = await CronogramaDaAlfa();
        daAlfa.Renomear("Invadido");

        // Para a outra empresa, o cronograma não existe — nem para dizer que existe.
        var alterar = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => Cronogramas(_beta).Atualizar(daAlfa, 1, Cancelamento));
        await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => Cronogramas(_beta).Remover(daAlfa.Id, 1, Cancelamento));

        var gravado = (await Cronogramas(_alfa).Obter(daAlfa.Id, Cancelamento))!;

        Assert.Equal($"Não existe o cronograma {daAlfa.Id}.", alterar.Message);
        Assert.Equal((1, "Casa"), (gravado.Versao, gravado.Agregado.Nome));
    }

    [Fact]
    public async Task Acompanhamento_so_mede_contra_linha_de_base_da_propria_empresa()
    {
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();
        await new RepositorioDeLinhasDeBase(banco.NovoContexto(_alfa), TimeProvider.System).Adicionar(linhaDeBase, Cancelamento);

        var erro = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() =>
            new RepositorioDeAcompanhamentos(banco.NovoContexto(_beta), TimeProvider.System)
                .Adicionar(new Acompanhamento(linhaDeBase), Cancelamento));

        Assert.Contains("grave-a antes do acompanhamento", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_banco_isola_mesmo_sem_o_filtro_do_codigo()
    {
        var daAlfa = await CronogramaDaAlfa();

        await using var daBeta = banco.NovoContexto(_beta);
        var paraABeta = await daBeta.Cronogramas.IgnoreQueryFilters().CountAsync(registro => registro.Id == daAlfa.Id.Valor, Cancelamento);

        // O dono do banco é superusuário e passa por cima: a linha está lá.
        await using var doDono = ContextoDoBanco.Criar(banco.Conexao, EmpresaFixa.Nenhuma);
        var paraODono = await doDono.Cronogramas.IgnoreQueryFilters().CountAsync(registro => registro.Id == daAlfa.Id.Valor, Cancelamento);

        Assert.Equal((0, 1), (paraABeta, paraODono));
    }

    [Fact]
    public async Task O_filtro_do_codigo_isola_mesmo_sem_a_seguranca_do_banco()
    {
        // Conectado como superusuário — um engano de configuração —, o banco
        // não filtra nada. O filtro do código continua filtrando.
        var daAlfa = await CronogramaDaAlfa();
        var daBetaComoDono = new RepositorioDeCronogramas(ContextoDoBanco.Criar(banco.Conexao, new EmpresaFixa(_beta)), TimeProvider.System);

        Assert.Null(await daBetaComoDono.Obter(daAlfa.Id, Cancelamento));
        Assert.DoesNotContain(await daBetaComoDono.Listar(Cancelamento), resumo => resumo.Id == daAlfa.Id);
    }

    [Fact]
    public async Task O_banco_recusa_gravar_em_nome_de_outra_empresa()
    {
        // SQL à mão, com o papel da aplicação: declara uma empresa e tenta gravar em nome de outra.
        await using var conexao = new NpgsqlConnection(banco.ConexaoDaAplicacao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(
            """
            select set_config('planejamento.empresa_id', @alfa, false);
            insert into planejamento.cronogramas (id, empresa_id, nome, versao, formato, documento, criado_em, atualizado_em)
            values (@id, @beta, 'Casa', 1, 1, '{}', now(), now());
            """,
            conexao);
        comando.Parameters.AddWithValue("alfa", _alfa.ToString());
        comando.Parameters.AddWithValue("beta", _beta);
        comando.Parameters.AddWithValue("id", Guid.CreateVersion7());

        var erro = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(Cancelamento));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, erro.SqlState);
    }

    [Fact]
    public async Task Sem_empresa_nada_se_le_nem_se_grava()
    {
        var daAlfa = await CronogramaDaAlfa();
        var semEmpresa = new RepositorioDeCronogramas(banco.NovoContextoSemEmpresa(), TimeProvider.System);

        Assert.Null(await semEmpresa.Obter(daAlfa.Id, Cancelamento));
        Assert.Empty(await semEmpresa.Listar(Cancelamento));
        await Assert.ThrowsAsync<InvalidOperationException>(() => semEmpresa.Adicionar(UmCronograma(), Cancelamento));
    }

    [Fact]
    public async Task A_empresa_de_um_pedido_nao_passa_ao_seguinte_pela_conexao_reaproveitada()
    {
        /*
         * O pool entrega a mesma conexão física ao contexto seguinte. O Npgsql
         * limpa a sessão ao devolvê-la — a não ser com No Reset On Close, que
         * se liga por desempenho. Aqui ela está ligada: sem o filtro do código
         * e sem a limpeza, só o parâmetro da empresa, redefinido a cada
         * abertura, separa um pedido do outro.
         */
        var daAlfa = await CronogramaDaAlfa();
        var semLimpeza = new NpgsqlConnectionStringBuilder(banco.ConexaoDaAplicacao) { NoResetOnClose = true }.ConnectionString;

        for (var vez = 0; vez < 5; vez++)
        {
            await using (var daAlfaDeNovo = ContextoDoBanco.Criar(semLimpeza, new EmpresaFixa(_alfa)))
            {
                Assert.Equal(1, await daAlfaDeNovo.Cronogramas.IgnoreQueryFilters().CountAsync(registro => registro.Id == daAlfa.Id.Valor, Cancelamento));
            }

            await using (var semEmpresa = ContextoDoBanco.Criar(semLimpeza, EmpresaFixa.Nenhuma))
            {
                Assert.Equal(0, await semEmpresa.Cronogramas.IgnoreQueryFilters().CountAsync(registro => registro.Id == daAlfa.Id.Valor, Cancelamento));
            }
        }
    }
}
