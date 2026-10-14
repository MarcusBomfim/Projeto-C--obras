using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Dominio.Testes;
using PlanejamentoObras.Dominio.Testes.Cronogramas;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;
using PlanejamentoObras.Dominio.Testes.ValorAgregado;
using PlanejamentoObras.Dominio.ValorAgregado;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;

namespace PlanejamentoObras.Infraestrutura.Testes.Documentos;

/*
 * Gravar e ler de volta, sem banco: agregado → registro → agregado, em 400
 * obras sorteadas, para os cinco tipos de agregado. Duas conferências em
 * cada um:
 *
 *  - escrever de novo o que foi lido dá o mesmo documento, caractere por
 *    caractere — nada se perde na leitura;
 *  - o que se calcula do lido é igual ao que se calculava do original —
 *    nada se perde na escrita. Um campo esquecido no documento (o vínculo de
 *    um item com a atividade, o BDI diferenciado) muda o caminho crítico, a
 *    planilha ou o valor agregado, e aparece aqui.
 */
public sealed class IdaEVoltaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [MemberData(nameof(RedesAleatorias.Lotes), MemberType = typeof(RedesAleatorias))]
    public void Todo_agregado_volta_como_foi(int lote)
    {
        foreach (var semente in RedesAleatorias.SementesDoLote(lote))
        {
            var (cronograma, baseDeCustos, orcamento) = ObrasAleatorias.Agregados(semente);

            var cronogramaLido = IdaEVoltaDoCronograma(cronograma);
            var baseLida = IdaEVoltaDaBase(baseDeCustos);
            IdaEVoltaDoOrcamento(orcamento, baseDeCustos, baseLida);

            var planilha = CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos);

            if (planilha.Itens.Count == 0)
            {
                continue;
            }

            var obra = ProgramacaoDoCronograma.Programar(cronogramaLido, new DateOnly(2026, 10, 1).AddDays(semente % 365), ObrasAleatorias.Calendario);
            var datas = semente % 2 == 0 ? DatasDoCronograma.Cedo : DatasDoCronograma.Tarde;
            var linhaDeBase = LinhaDeBase.Estabelecer($"LB {semente}", obra.DataDeInicio, planilha, obra, datas);
            var linhaDeBaseLida = IdaEVoltaDaLinhaDeBase(linhaDeBase);

            var acompanhamento = new Acompanhamento(linhaDeBase);
            MedicoesAleatorias.Registrar(acompanhamento, semente);
            IdaEVoltaDoAcompanhamento(acompanhamento, linhaDeBaseLida);
        }
    }

    private static Cronograma IdaEVoltaDoCronograma(Cronograma original)
    {
        var registro = RegistroDeCronograma.De(original, versao: 1, Agora);
        var lido = registro.ParaCronograma();

        Assert.Equal(original.Id, lido.Id);
        Assert.Equal(registro.Documento, RegistroDeCronograma.De(lido, 1, Agora).Documento);
        Assert.Equal(CalculadoraDeCaminhoCritico.Analisar(original).Atividades, CalculadoraDeCaminhoCritico.Analisar(lido).Atividades);

        return lido;
    }

    private static BaseDeCustos IdaEVoltaDaBase(BaseDeCustos original)
    {
        var registro = RegistroDeBaseDeCustos.De(original, versao: 1, Agora);
        var lida = registro.ParaBaseDeCustos();

        Assert.Equal(original.Id, lida.Id);
        Assert.Equal(registro.Documento, RegistroDeBaseDeCustos.De(lida, 1, Agora).Documento);

        foreach (var composicao in original.Composicoes)
        {
            var antes = CalculadoraDeCustos.Calcular(original, composicao.Codigo.Valor);
            var depois = CalculadoraDeCustos.Calcular(lida, composicao.Codigo.Valor);

            Assert.Equal(antes.CustoUnitario, depois.CustoUnitario);
            Assert.Equal(antes.Linhas, depois.Linhas);
        }

        return lida;
    }

    private static void IdaEVoltaDoOrcamento(Orcamento original, BaseDeCustos baseOriginal, BaseDeCustos baseLida)
    {
        var registro = RegistroDeOrcamento.De(original, versao: 1, Agora);
        var lido = registro.ParaOrcamento();

        Assert.Equal(original.Id, lido.Id);
        Assert.Equal(registro.Documento, RegistroDeOrcamento.De(lido, 1, Agora).Documento);

        // A planilha do lido, contra a base lida, é a do original contra a original.
        Assert.Equal(
            CalculadoraDoOrcamento.Calcular(original, baseOriginal).Linhas,
            CalculadoraDoOrcamento.Calcular(lido, baseLida).Linhas);
    }

    private static LinhaDeBase IdaEVoltaDaLinhaDeBase(LinhaDeBase original)
    {
        var registro = RegistroDeLinhaDeBase.De(original, versao: 1, Agora);
        var lida = registro.ParaLinhaDeBase();

        Assert.Equal(original.Id, lida.Id);
        Assert.Equal(registro.Documento, RegistroDeLinhaDeBase.De(lida, 1, Agora).Documento);
        Assert.Equal(
            original.Meses.Select(mes => (original.ValorPlanejadoAcumulado(mes), original.CustoPlanejadoAcumulado(mes))),
            lida.Meses.Select(mes => (lida.ValorPlanejadoAcumulado(mes), lida.CustoPlanejadoAcumulado(mes))));

        return lida;
    }

    private static void IdaEVoltaDoAcompanhamento(Acompanhamento original, LinhaDeBase linhaDeBaseLida)
    {
        var registro = RegistroDeAcompanhamento.De(original, versao: 1, Agora);
        var lido = registro.ParaAcompanhamento(linhaDeBaseLida);

        Assert.Equal(original.Id, lido.Id);
        Assert.Equal(original.LinhaDeBase.Id.Valor, registro.LinhaDeBaseId);
        Assert.Equal(registro.Documento, RegistroDeAcompanhamento.De(lido, 1, Agora).Documento);

        if (original.Medicoes.Count == 0)
        {
            return;
        }

        var antes = CalculadoraDeValorAgregado.Calcular(original);
        var depois = CalculadoraDeValorAgregado.Calcular(lido);

        Assert.Equal(antes.Obra, depois.Obra);
        Assert.Equal(antes.Prazo, depois.Prazo);
        Assert.Equal(antes.Linhas, depois.Linhas);
        Assert.Equal(
            EmissaoDoBoletim.Emitir(original, original.Medicoes.Count).Linhas,
            EmissaoDoBoletim.Emitir(lido, lido.Medicoes.Count).Linhas);
    }

    [Fact]
    public void Ida_e_volta_preserva_o_que_o_sorteio_raramente_produz()
    {
        // Os casos de borda, garantidos aqui em vez de esperados do sorteio:
        // BDI pela fórmula com parcelas, BDI diferenciado, item que é insumo,
        // atividade vinculada, glosa e medição sem lançamento.
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada(DatasDoCronograma.Tarde);
        var acompanhamento = new Acompanhamento(linhaDeBase);
        acompanhamento.RegistrarMedicao(new DateOnly(2026, 10, 1), new LancamentoDeMedicao("2.1", 0.6m, 6_200.50m));
        acompanhamento.RegistrarMedicao(new DateOnly(2026, 11, 1));
        acompanhamento.RegistrarMedicao(new DateOnly(2026, 12, 1), new LancamentoDeMedicao("2.1", -0.1m));

        var orcamento = new Orcamento("Bordas", Bdi.PelaFormulaDoTcu(new ParcelasDoBdi(4.00m, 0.40m, 1.27m, 0.40m, 1.23m, 7.40m, 6.15m)));
        orcamento.AdicionarEtapa("Etapa");
        orcamento.AdicionarEtapa("Subetapa", "1");
        orcamento.AdicionarItem("1.1", "ELV", 2m);
        orcamento.DefinirBdiDiferenciado("1.1.1", Bdi.Informado(12m));
        orcamento.VincularAtividade("1.1.1", "ENT");

        var orcamentoLido = RegistroDeOrcamento.De(orcamento, 1, Agora).ParaOrcamento();
        var lido = RegistroDeAcompanhamento.De(acompanhamento, 1, Agora)
            .ParaAcompanhamento(RegistroDeLinhaDeBase.De(linhaDeBase, 1, Agora).ParaLinhaDeBase());

        Assert.Equal(orcamento.Bdi, orcamentoLido.Bdi);
        Assert.Equal(Bdi.Informado(12m), orcamentoLido.Item("1.1.1").BdiDiferenciado);
        Assert.Equal("ENT", orcamentoLido.Item("1.1.1").Atividade?.Valor);
        Assert.Equal(DatasDoCronograma.Tarde, lido.LinhaDeBase.Datas);
        Assert.Equal([1, 0, 1], lido.Medicoes.Select(medicao => medicao.Itens.Count));
        Assert.Equal(-0.1m, lido.Medicoes[2].Itens[0].Quantidade);
        Assert.Equal(Dinheiro.Reais(6_200.50m), lido.Medicoes[0].Itens[0].CustoReal);
    }
}
