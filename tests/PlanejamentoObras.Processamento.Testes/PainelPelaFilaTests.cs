using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.ValorAgregado;
using PlanejamentoObras.Infraestrutura.Persistencia.Painel;
using PlanejamentoObras.Infraestrutura.Testes.Persistencia;
using PlanejamentoObras.Processamento.Testes.Apoio;

namespace PlanejamentoObras.Processamento.Testes;

/*
 * O painel das obras mantido pela fila, de ponta a ponta: a obra gravada
 * como a API grava, as mensagens tratadas pelo processador, e o painel lido
 * como a API lê.
 */
public sealed class PainelPelaFilaTests(BancoDeTestes banco) : TesteComFila(banco)
{
    private async Task Processar()
    {
        await using var processamento = Processamento();
        await processamento.Processador.ProcessarPendentes(Cancelamento);
    }

    private static (SituacaoDaObra, int?, int) Resumo(PainelDaObra? linha)
    {
        Assert.NotNull(linha);

        return (linha.Situacao, linha.Medicao, linha.VersaoDoAcompanhamento);
    }

    [Fact]
    public async Task O_painel_acompanha_cada_medicao_e_cada_medicao_desfeita()
    {
        var obras = Obras();
        var acompanhamento = await obras.Gravar();
        await Processar();

        Assert.Equal((SituacaoDaObra.SemMedicao, null, 1), Resumo(await obras.Painel(acompanhamento.Id)));

        // Outubro atrasado; novembro quase em dia, e abaixo do custo.
        await obras.Medir(acompanhamento.Id, Outubro, new LancamentoDeMedicao("1.1", 1m, 7_900m), new LancamentoDeMedicao("2.1", 0.5m, 4_600m));
        await obras.Medir(acompanhamento.Id, Novembro, new LancamentoDeMedicao("2.1", 0.5m, 4_100m), new LancamentoDeMedicao("2.2", 1m, 15_000m));
        await Processar();

        Assert.Equal((SituacaoDaObra.EmDia, 2, 3), Resumo(await obras.Painel(acompanhamento.Id)));

        await obras.DesfazerUltimaMedicao(acompanhamento.Id);
        await Processar();

        Assert.Equal((SituacaoDaObra.Critica, 1, 4), Resumo(await obras.Painel(acompanhamento.Id)));
    }

    [Fact]
    public async Task Acompanhamento_removido_antes_do_tratamento_nao_volta_ao_painel()
    {
        var obras = Obras();
        var acompanhamento = await obras.Gravar();
        await obras.Remover(acompanhamento.Id);

        await Processar();

        Assert.Null(await obras.Painel(acompanhamento.Id));
        Assert.NotNull(Assert.Single(await CaixaNoBanco.DoAgregado(Banco, acompanhamento.Id.Valor)).TratadaEm);
    }
}
