using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;
using PlanejamentoObras.Infraestrutura.Mensagens;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.CaixaDeSaida;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Infraestrutura.Testes.Persistencia;

/*
 * A caixa de saída do lado de quem grava: os fatos de um agregado entram na
 * mesma transação que ele — ou nenhum dos dois entra —, na empresa de quem
 * gravou (ADR-0017). Quem reserva e trata as mensagens é testado com o
 * processador.
 */
public sealed class CaixaDeSaidaTests(BancoDeTestes banco)
{
    private static readonly DateOnly Outubro = new(2026, 10, 1);

    private static readonly DateOnly Novembro = new(2026, 11, 1);

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _empresa = Guid.CreateVersion7();

    private readonly RelogioDeTeste _relogio = new();

    private RepositorioDeLinhasDeBase LinhasDeBase() => new(banco.NovoContexto(_empresa), _relogio);

    private RepositorioDeAcompanhamentos Acompanhamentos() => new(banco.NovoContexto(_empresa), _relogio);

    /// <summary>As mensagens de um acompanhamento, em ordem de chegada, como a empresa as vê.</summary>
    private async Task<List<RegistroDeMensagem>> MensagensDe(AcompanhamentoId acompanhamento, Guid? empresa = null)
    {
        await using var contexto = banco.NovoContexto(empresa ?? _empresa);

        return await contexto.CaixaDeSaida
            .AsNoTracking()
            .Where(mensagem => mensagem.AgregadoId == acompanhamento.Valor)
            .OrderBy(mensagem => mensagem.Sequencia)
            .ToListAsync(Cancelamento);
    }

    private async Task<Acompanhamento> UmAcompanhamentoGravado()
    {
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();
        var acompanhamento = new Acompanhamento(linhaDeBase);
        await LinhasDeBase().Adicionar(linhaDeBase, Cancelamento);
        await Acompanhamentos().Adicionar(acompanhamento, Cancelamento);

        return acompanhamento;
    }

    private static LancamentoDeMedicao Fundacoes(decimal quantidade) => new("2.1", quantidade, CustoReal: 10_000m * quantidade);

    /// <summary>
    /// Faz o banco recusar as mensagens de um agregado — só as dele, para não
    /// atrapalhar os outros testes. A recusa vem depois de o agregado ter
    /// sido gravado: é a transação que tem de desfazê-lo.
    /// </summary>
    private async Task RecusarMensagensDe(Guid agregado) =>
        await ComoDono(
            $"""
            create function planejamento.recusar_{agregado:N}() returns trigger language plpgsql as $$
            begin
                if new.agregado_id = '{agregado}' then
                    raise exception 'O teste recusou a mensagem.';
                end if;
                return new;
            end
            $$;
            create trigger recusar_{agregado:N} before insert on planejamento.caixa_de_saida
                for each row execute function planejamento.recusar_{agregado:N}();
            """);

    private async Task AceitarMensagensDe(Guid agregado) =>
        await ComoDono(
            $"""
            drop trigger recusar_{agregado:N} on planejamento.caixa_de_saida;
            drop function planejamento.recusar_{agregado:N}();
            """);

    private async Task ComoDono(string sql)
    {
        await using var conexao = new NpgsqlConnection(banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(sql, conexao);
        await comando.ExecuteNonQueryAsync(Cancelamento);
    }

    [Fact]
    public async Task Os_fatos_entram_na_caixa_junto_com_o_agregado()
    {
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();
        var acompanhamento = new Acompanhamento(linhaDeBase);
        acompanhamento.RegistrarMedicao(Outubro, Fundacoes(0.6m));
        await LinhasDeBase().Adicionar(linhaDeBase, Cancelamento);

        await Acompanhamentos().Adicionar(acompanhamento, Cancelamento);

        var mensagens = await MensagensDe(acompanhamento.Id);

        Assert.Equal(["acompanhamento-iniciado", "medicao-registrada"], mensagens.Select(mensagem => mensagem.Tipo));
        Assert.All(mensagens, mensagem =>
        {
            Assert.Equal((_empresa, 1, CatalogoDeMensagens.Formato), (mensagem.EmpresaId, mensagem.VersaoDoAgregado, mensagem.Formato));
            Assert.Equal((_relogio.Agora, _relogio.Agora, 0), (mensagem.OcorridaEm, mensagem.ProximaTentativaEm, mensagem.Tentativas));
            Assert.Null(mensagem.TratadaEm);
            Assert.Null(mensagem.AbandonadaEm);
        });
        Assert.Equal(
            new MensagemDeMedicaoRegistrada(acompanhamento.Id.Valor, 1, Outubro),
            CatalogoDeMensagens.Ler(mensagens[1].Tipo, mensagens[1].Formato, mensagens[1].Conteudo));

        // Publicados, os fatos saem do agregado: gravar de novo não os repete.
        Assert.Empty(acompanhamento.Eventos);
    }

    [Fact]
    public async Task A_caixa_guarda_os_fatos_na_ordem_em_que_aconteceram()
    {
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();
        var acompanhamento = new Acompanhamento(linhaDeBase);
        acompanhamento.RegistrarMedicao(Outubro, Fundacoes(0.2m));
        acompanhamento.DesfazerUltimaMedicao();
        acompanhamento.RegistrarMedicao(Outubro, Fundacoes(0.3m));
        acompanhamento.RegistrarMedicao(Novembro, Fundacoes(0.4m));
        await LinhasDeBase().Adicionar(linhaDeBase, Cancelamento);

        await Acompanhamentos().Adicionar(acompanhamento, Cancelamento);

        var id = acompanhamento.Id.Valor;
        Assert.Equal(
            new object[]
            {
                new MensagemDeAcompanhamentoIniciado(id, linhaDeBase.Id.Valor),
                new MensagemDeMedicaoRegistrada(id, 1, Outubro),
                new MensagemDeMedicaoDesfeita(id, 1, Outubro),
                new MensagemDeMedicaoRegistrada(id, 1, Outubro),
                new MensagemDeMedicaoRegistrada(id, 2, Novembro),
            },
            (await MensagensDe(acompanhamento.Id)).Select(mensagem => CatalogoDeMensagens.Ler(mensagem.Tipo, mensagem.Formato, mensagem.Conteudo)));
    }

    [Fact]
    public async Task A_atualizacao_poe_na_caixa_so_os_fatos_novos_com_a_versao_nova()
    {
        var acompanhamento = await UmAcompanhamentoGravado();
        var lido = (await Acompanhamentos().Obter(acompanhamento.Id, Cancelamento))!;
        lido.Agregado.RegistrarMedicao(Outubro, Fundacoes(0.6m));
        lido.Agregado.DesfazerUltimaMedicao();

        await Acompanhamentos().Atualizar(lido.Agregado, lido.Versao, Cancelamento);

        Assert.Equal(
            [("acompanhamento-iniciado", 1), ("medicao-registrada", 2), ("medicao-desfeita", 2)],
            (await MensagensDe(acompanhamento.Id)).Select(mensagem => (mensagem.Tipo, mensagem.VersaoDoAgregado)));
        Assert.Empty(lido.Agregado.Eventos);
    }

    [Fact]
    public async Task Gravar_sem_fato_novo_nao_poe_nada_na_caixa()
    {
        var acompanhamento = await UmAcompanhamentoGravado();
        var lido = (await Acompanhamentos().Obter(acompanhamento.Id, Cancelamento))!;

        Assert.Equal(2, await Acompanhamentos().Atualizar(lido.Agregado, lido.Versao, Cancelamento));

        Assert.Equal(["acompanhamento-iniciado"], (await MensagensDe(acompanhamento.Id)).Select(mensagem => mensagem.Tipo));
    }

    [Fact]
    public async Task Conflito_de_versao_nao_grava_nem_o_agregado_nem_os_fatos_dele()
    {
        var acompanhamento = await UmAcompanhamentoGravado();
        var deAna = (await Acompanhamentos().Obter(acompanhamento.Id, Cancelamento))!;
        var deBruno = (await Acompanhamentos().Obter(acompanhamento.Id, Cancelamento))!;
        deAna.Agregado.RegistrarMedicao(Outubro, Fundacoes(0.6m));
        deBruno.Agregado.RegistrarMedicao(Outubro, Fundacoes(0.4m));
        await Acompanhamentos().Atualizar(deAna.Agregado, deAna.Versao, Cancelamento);

        await Assert.ThrowsAsync<ConflitoDeConcorrencia>(() => Acompanhamentos().Atualizar(deBruno.Agregado, deBruno.Versao, Cancelamento));

        Assert.Equal(
            ["acompanhamento-iniciado", "medicao-registrada"],
            (await MensagensDe(acompanhamento.Id)).Select(mensagem => mensagem.Tipo));

        // Nada foi gravado: os fatos de Bruno continuam com ele.
        Assert.IsType<MedicaoRegistrada>(Assert.Single(deBruno.Agregado.Eventos));
    }

    [Fact]
    public async Task Mensagem_recusada_desfaz_o_agregado_inserido()
    {
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();
        await LinhasDeBase().Adicionar(linhaDeBase, Cancelamento);
        var acompanhamento = new Acompanhamento(linhaDeBase);
        await RecusarMensagensDe(acompanhamento.Id.Valor);

        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Acompanhamentos().Adicionar(acompanhamento, Cancelamento));
        }
        finally
        {
            await AceitarMensagensDe(acompanhamento.Id.Valor);
        }

        Assert.Null(await Acompanhamentos().Obter(acompanhamento.Id, Cancelamento));
    }

    [Fact]
    public async Task Mensagem_recusada_desfaz_a_atualizacao_do_agregado()
    {
        var acompanhamento = await UmAcompanhamentoGravado();
        var lido = (await Acompanhamentos().Obter(acompanhamento.Id, Cancelamento))!;
        lido.Agregado.RegistrarMedicao(Outubro, Fundacoes(0.6m));
        await RecusarMensagensDe(acompanhamento.Id.Valor);

        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Acompanhamentos().Atualizar(lido.Agregado, lido.Versao, Cancelamento));
        }
        finally
        {
            await AceitarMensagensDe(acompanhamento.Id.Valor);
        }

        var relido = (await Acompanhamentos().Obter(acompanhamento.Id, Cancelamento))!;
        Assert.Equal((1, 0), (relido.Versao, relido.Agregado.Medicoes.Count));
    }

    [Fact]
    public async Task Agregado_que_nao_entra_no_banco_nao_deixa_fato_na_caixa()
    {
        // A linha de base não foi gravada: a chave estrangeira recusa o acompanhamento.
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());

        await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => Acompanhamentos().Adicionar(acompanhamento, Cancelamento));

        Assert.Empty(await MensagensDe(acompanhamento.Id));
        Assert.IsType<AcompanhamentoIniciado>(Assert.Single(acompanhamento.Eventos));
    }

    [Fact]
    public async Task Cada_empresa_so_enxerga_as_proprias_mensagens()
    {
        var acompanhamento = await UmAcompanhamentoGravado();
        var outra = Guid.CreateVersion7();

        Assert.Empty(await MensagensDe(acompanhamento.Id, outra));

        // Nem passando por cima do filtro do código: a segurança por linha segura.
        await using var contexto = banco.NovoContexto(outra);
        Assert.Empty(await contexto.CaixaDeSaida
            .IgnoreQueryFilters()
            .Where(mensagem => mensagem.AgregadoId == acompanhamento.Id.Valor)
            .ToListAsync(Cancelamento));

        // Nem conectado como o dono, que passa por cima do banco: o filtro do código segura.
        await using var doDono = ContextoDoBanco.Criar(banco.Conexao, new EmpresaFixa(outra));
        Assert.Empty(await doDono.CaixaDeSaida
            .Where(mensagem => mensagem.AgregadoId == acompanhamento.Id.Valor)
            .ToListAsync(Cancelamento));
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(7, 1_920)]
    [InlineData(8, 3_600)]
    [InlineData(10, 3_600)]
    [InlineData(1_000, 3_600)]
    public void A_espera_dobra_a_cada_falha_ate_uma_hora(int tentativas, int segundos)
    {
        Assert.Equal(TimeSpan.FromSeconds(segundos), FilaDaCaixaDeSaida.Espera(tentativas));
    }

    /// <summary>Um relógio parado: a hora que a caixa de saída grava é a dele.</summary>
    private sealed class RelogioDeTeste : TimeProvider
    {
        public DateTimeOffset Agora { get; } = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
