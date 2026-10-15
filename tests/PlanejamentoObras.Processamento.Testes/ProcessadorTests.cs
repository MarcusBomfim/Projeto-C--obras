using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Infraestrutura.Mensagens;
using PlanejamentoObras.Infraestrutura.Persistencia.CaixaDeSaida;
using PlanejamentoObras.Infraestrutura.Testes.Persistencia;
using PlanejamentoObras.Processamento.Testes.Apoio;

namespace PlanejamentoObras.Processamento.Testes;

/*
 * O processador da caixa de saída contra o banco de verdade: cada mensagem
 * tratada uma vez, em nome da empresa dela; a falha desfaz o que os
 * tratadores gravaram e a mensagem volta mais tarde, com espera crescente,
 * até a fila desistir; dois processadores dividem a fila sem tratar nada duas
 * vezes (ADR-0017).
 */
public sealed class ProcessadorTests(BancoDeTestes banco) : TesteComFila(banco)
{
    private readonly Anotacoes _anotacoes = new();

    private AmbienteDoProcessador ProcessamentoQueAnota(Action<IServiceCollection>? registrar = null) =>
        Processamento(servicos =>
        {
            servicos.AddSingleton(_anotacoes).TratarTambemCom<TratadorQueAnota>();
            registrar?.Invoke(servicos);
        });

    private async Task<MensagemNoBanco> UnicaMensagemDe(Acompanhamento acompanhamento) =>
        Assert.Single(await CaixaNoBanco.DoAgregado(Banco, acompanhamento.Id.Valor));

    private static Guid AcompanhamentoDa(Anotacao anotacao) =>
        Assert.IsType<MensagemDeAcompanhamentoIniciado>(anotacao.Mensagem).Acompanhamento;

    [Fact]
    public async Task Cada_mensagem_e_tratada_uma_vez_e_marcada_como_tratada()
    {
        var acompanhamento = await Obras().Gravar(medir: novo => novo.RegistrarMedicao(Outubro, new LancamentoDeMedicao("2.1", 0.5m, 4_600m)));
        await using var processamento = ProcessamentoQueAnota();

        Assert.Equal(2, await processamento.Processador.ProcessarPendentes(Cancelamento));
        Assert.Equal(0, await processamento.Processador.ProcessarPendentes(Cancelamento));

        Assert.Equal(
            new object[]
            {
                new MensagemDeAcompanhamentoIniciado(acompanhamento.Id.Valor, acompanhamento.LinhaDeBase.Id.Valor),
                new MensagemDeMedicaoRegistrada(acompanhamento.Id.Valor, 1, Outubro),
            },
            _anotacoes.Todas.Select(anotacao => anotacao.Mensagem));
        Assert.All(
            await CaixaNoBanco.DoAgregado(Banco, acompanhamento.Id.Valor),
            mensagem => Assert.Equal(((DateTimeOffset?)Relogio.Agora, 0, (string?)null), (mensagem.TratadaEm, mensagem.Tentativas, mensagem.UltimoErro)));
    }

    [Fact]
    public async Task Cada_mensagem_e_tratada_em_nome_da_empresa_dela()
    {
        var alfa = Guid.CreateVersion7();
        var beta = Guid.CreateVersion7();
        var daAlfa = await Obras(alfa).Gravar();
        var daBeta = await Obras(beta).Gravar();
        await using var processamento = ProcessamentoQueAnota();

        await processamento.Processador.ProcessarPendentes(Cancelamento);

        // No código e no banco: o filtro e a segurança por linha valem para o
        // tratamento como valem para um pedido.
        Assert.Equal(
            [(daAlfa.Id.Valor, alfa, alfa.ToString()), (daBeta.Id.Valor, beta, beta.ToString())],
            _anotacoes.Todas.Select(anotacao => (AcompanhamentoDa(anotacao), anotacao.Empresa!.Value, anotacao.EmpresaNoBanco)));
    }

    [Fact]
    public async Task Falha_desfaz_o_que_os_tratadores_gravaram_e_a_mensagem_volta_depois_da_espera()
    {
        var acompanhamento = await Obras().Gravar();
        await using var processamento = Processamento(servicos => servicos.AddSingleton(new Falhas(vezes: 1)).TratarTambemCom<TratadorQueFalha>());

        // O tratador do painel grava a linha; o que falha vem depois dele.
        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        var mensagem = await UnicaMensagemDe(acompanhamento);
        Assert.Null(await Obras().Painel(acompanhamento.Id));
        Assert.Equal(
            (1, Relogio.Agora + TimeSpan.FromSeconds(30), (DateTimeOffset?)null, "InvalidOperationException: Falha de propósito."),
            (mensagem.Tentativas, mensagem.ProximaTentativaEm, mensagem.TratadaEm, mensagem.UltimoErro));

        // Antes da hora a mensagem não volta; na hora, volta — e passa.
        Assert.Equal(0, await processamento.Processador.ProcessarPendentes(Cancelamento));
        Relogio.Adiantar(TimeSpan.FromSeconds(30));
        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        Assert.NotNull(await Obras().Painel(acompanhamento.Id));
        Assert.Equal(Relogio.Agora, (await UnicaMensagemDe(acompanhamento)).TratadaEm);
    }

    [Fact]
    public async Task Depois_de_dez_falhas_a_fila_desiste_da_mensagem()
    {
        var acompanhamento = await Obras().Gravar();
        using var registro = new RegistroEmMemoria();
        await using var processamento = Processamento(servicos => servicos
            .AddLogging(log => log.AddProvider(registro))
            .AddSingleton(new Falhas(vezes: int.MaxValue))
            .TratarTambemCom<TratadorQueFalha>());
        var esperas = new List<int>();

        for (var tentativa = 1; tentativa <= FilaDaCaixaDeSaida.TentativasAntesDeDesistir; tentativa++)
        {
            Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

            var mensagem = await UnicaMensagemDe(acompanhamento);
            esperas.Add((int)(mensagem.ProximaTentativaEm - Relogio.Agora).TotalSeconds);
            Relogio.Agora = mensagem.ProximaTentativaEm;
        }

        var abandonada = await UnicaMensagemDe(acompanhamento);
        Assert.Equal([30, 60, 120, 240, 480, 960, 1_920, 3_600, 3_600, 3_600], esperas);
        Assert.Equal((10, (DateTimeOffset?)null), (abandonada.Tentativas, abandonada.TratadaEm));
        Assert.NotNull(abandonada.AbandonadaEm);

        // Abandonada, não volta mais — nem um dia depois.
        Relogio.Adiantar(TimeSpan.FromDays(1));
        Assert.Equal(0, await processamento.Processador.ProcessarPendentes(Cancelamento));

        var doProcessador = registro.Entradas.Where(entrada => entrada.Categoria == typeof(ProcessadorDaCaixaDeSaida).FullName).ToList();
        Assert.Equal(9, doProcessador.Count(entrada => entrada.Nivel == LogLevel.Warning));
        Assert.Contains("foi abandonada", Assert.Single(doProcessador, entrada => entrada.Nivel == LogLevel.Error).Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mensagem_de_tipo_desconhecido_espera_uma_versao_que_a_conheca()
    {
        // Numa implantação aos poucos, a API nova pode gravar um fato novo
        // antes de o processador novo subir: o velho não descarta — tenta de
        // novo mais tarde.
        var id = await CaixaNoBanco.GravarCrua(Banco, Empresa, "medicao-reaberta", Relogio.Agora);
        await using var processamento = Processamento();

        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        var mensagem = Assert.Single(await CaixaNoBanco.Todas(Banco));
        Assert.Equal(
            (id, 1, (DateTimeOffset?)null, (DateTimeOffset?)null, "InvalidOperationException: Tipo de mensagem desconhecido: medicao-reaberta."),
            (mensagem.Id, mensagem.Tentativas, mensagem.TratadaEm, mensagem.AbandonadaEm, mensagem.UltimoErro));
    }

    [Fact]
    public async Task Dois_processadores_nunca_tratam_a_mesma_mensagem()
    {
        const int Quantas = 12;
        var gravados = new List<Guid>();

        for (var i = 0; i < Quantas; i++)
        {
            gravados.Add((await Obras().Gravar()).Id.Valor);
        }

        await using var um = ProcessamentoQueAnota(servicos => servicos.TratarTambemCom<TratadorDemorado>());
        await using var outro = ProcessamentoQueAnota(servicos => servicos.TratarTambemCom<TratadorDemorado>());

        var tratadas = await Task.WhenAll(
            um.Processador.ProcessarPendentes(Cancelamento),
            outro.Processador.ProcessarPendentes(Cancelamento));

        Assert.Equal(Quantas, tratadas.Sum());
        Assert.Equal(gravados.Order(), _anotacoes.Todas.Select(AcompanhamentoDa).Order());
    }

    [Fact]
    public async Task Tempo_esgotado_dentro_do_tratador_e_falha_dele_e_nao_parada_do_host()
    {
        var acompanhamento = await Obras().Gravar();
        await using var processamento = Processamento(servicos => servicos.TratarTambemCom<TratadorQueEsgotaOTempo>());

        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        var mensagem = await UnicaMensagemDe(acompanhamento);
        Assert.Equal(
            (1, "TaskCanceledException: O serviço externo não respondeu a tempo."),
            (mensagem.Tentativas, mensagem.UltimoErro));
    }

    [Fact]
    public async Task Parar_no_meio_devolve_a_mensagem_a_fila_sem_contar_falha()
    {
        var acompanhamento = await Obras().Gravar();
        using var parar = new CancellationTokenSource();

        await using (var parando = Processamento(servicos => servicos.AddSingleton(parar).TratarTambemCom<TratadorQueCancela>()))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => parando.Processador.ProcessarPendentes(parar.Token));
        }

        var devolvida = await UnicaMensagemDe(acompanhamento);
        Assert.Equal((0, (DateTimeOffset?)null, (string?)null), (devolvida.Tentativas, devolvida.TratadaEm, devolvida.UltimoErro));
        Assert.Null(await Obras().Painel(acompanhamento.Id));

        // O próximo processador a encontra, pronta.
        await using var processamento = Processamento();
        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));
    }
}
