using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;
using PlanejamentoObras.Dominio.ValorAgregado;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Painel;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Infraestrutura.Testes.Persistencia;

/*
 * O painel das obras gravado e lido direto, sem a fila: a linha é refeita do
 * acompanhamento como ele está, e a de uma versão mais velha nunca grava por
 * cima da de uma mais nova (ADR-0017).
 */
public sealed class PainelTests(BancoDeTestes banco)
{
    private static readonly DateOnly Outubro = new(2026, 10, 1);

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _empresa = Guid.CreateVersion7();

    private readonly RelogioDeTeste _relogio = new();

    private RepositorioDeAcompanhamentos Acompanhamentos() => new(banco.NovoContexto(_empresa), _relogio);

    private ProjecaoDoPainel Projecao() => new(banco.NovoContexto(_empresa), _relogio);

    private ConsultaDoPainel Consulta(Guid? empresa = null) => new(banco.NovoContexto(empresa ?? _empresa));

    private async Task<Acompanhamento> UmAcompanhamentoGravado(LinhaDeBase? linhaDeBase = null, Action<Acompanhamento>? medir = null)
    {
        linhaDeBase ??= CasaTerrea.LinhaDeBaseAprovada();
        var acompanhamento = new Acompanhamento(linhaDeBase);
        medir?.Invoke(acompanhamento);
        await new RepositorioDeLinhasDeBase(banco.NovoContexto(_empresa), _relogio).Adicionar(linhaDeBase, Cancelamento);
        await Acompanhamentos().Adicionar(acompanhamento, Cancelamento);

        return acompanhamento;
    }

    private async Task<Versionado<Acompanhamento>> Ler(Acompanhamento acompanhamento) =>
        (await Acompanhamentos().Obter(acompanhamento.Id, Cancelamento))!;

    private async Task Projetar(Acompanhamento acompanhamento) => await Projecao().Atualizar(await Ler(acompanhamento), Cancelamento);

    private static void MedirOutubro(Acompanhamento acompanhamento) =>
        acompanhamento.RegistrarMedicao(
            Outubro,
            new LancamentoDeMedicao("1.1", 1m, 7_900m),
            new LancamentoDeMedicao("2.1", 0.5m, 4_600m));

    [Fact]
    public async Task Obra_sem_medicao_aparece_sem_indicadores()
    {
        var acompanhamento = await UmAcompanhamentoGravado();
        var linhaDeBase = acompanhamento.LinhaDeBase;

        await Projetar(acompanhamento);

        Assert.Equal(
            new PainelDaObra(
                acompanhamento.Id.Valor,
                linhaDeBase.Id.Valor,
                linhaDeBase.Nome,
                SituacaoDaObra.SemMedicao,
                Medicao: null,
                Mes: null,
                linhaDeBase.CustoTotal.Valor,
                ValorPlanejado: null,
                ValorAgregado: null,
                CustoReal: null,
                IndiceDeDesempenhoDePrazo: null,
                IndiceDeDesempenhoDeCusto: null,
                EstimativaNoTermino: null,
                VariacaoNoTermino: null,
                linhaDeBase.Meses.Count,
                PrazoAgregadoEmMeses: null,
                DuracaoEstimadaEmMeses: null,
                VersaoDoAcompanhamento: 1,
                _relogio.Agora),
            await Consulta().Obter(acompanhamento.Id.Valor, Cancelamento));
    }

    [Fact]
    public async Task A_linha_traz_os_indicadores_da_ultima_medicao()
    {
        var acompanhamento = await UmAcompanhamentoGravado(medir: MedirOutubro);
        var linhaDeBase = acompanhamento.LinhaDeBase;
        var analise = CalculadoraDeValorAgregado.Calcular(acompanhamento);
        var obra = analise.Obra;

        await Projetar(acompanhamento);

        // Cada coluna com o seu número: os valores do cenário são todos
        // diferentes, então uma coluna trocada não passa.
        Assert.Equal(
            new PainelDaObra(
                acompanhamento.Id.Valor,
                linhaDeBase.Id.Valor,
                linhaDeBase.Nome,
                SituacaoDaObra.Critica,
                Medicao: 1,
                Mes: Outubro,
                obra.OrcamentoNoTermino.Valor,
                obra.ValorPlanejado.Valor,
                obra.ValorAgregado.Valor,
                obra.CustoReal.Valor,
                obra.IndiceDeDesempenhoDePrazo,
                obra.IndiceDeDesempenhoDeCusto,
                obra.EstimativaNoTermino?.Valor,
                obra.VariacaoNoTermino?.Valor,
                linhaDeBase.Meses.Count,
                analise.Prazo.PrazoAgregadoEmMeses,
                analise.Prazo.DuracaoEstimadaEmMeses,
                VersaoDoAcompanhamento: 1,
                _relogio.Agora),
            await Consulta().Obter(acompanhamento.Id.Valor, Cancelamento));
        Assert.Equal(SituacaoDaObra.Critica, ClassificacaoDaObra.Classificar(obra));
    }

    [Fact]
    public async Task A_linha_de_uma_versao_mais_velha_nao_grava_por_cima()
    {
        var acompanhamento = await UmAcompanhamentoGravado();
        var primeira = await Ler(acompanhamento);
        var segunda = await Ler(acompanhamento);
        MedirOutubro(segunda.Agregado);
        await Acompanhamentos().Atualizar(segunda.Agregado, segunda.Versao, Cancelamento);
        await Projetar(acompanhamento);

        // Dois processadores, cada um com uma versão: o que leu a mais velha
        // chega depois.
        _relogio.Agora += TimeSpan.FromMinutes(1);
        await Projecao().Atualizar(primeira, Cancelamento);

        var linha = (await Consulta().Obter(acompanhamento.Id.Valor, Cancelamento))!;

        Assert.Equal((2, 1, _relogio.Agora - TimeSpan.FromMinutes(1)), (linha.VersaoDoAcompanhamento, linha.Medicao, linha.AtualizadoEm));
    }

    [Fact]
    public async Task Refazer_a_mesma_versao_da_a_mesma_linha()
    {
        // A entrega é ao menos uma vez: a mesma mensagem pode ser tratada de novo.
        var acompanhamento = await UmAcompanhamentoGravado(medir: MedirOutubro);
        await Projetar(acompanhamento);
        var antes = (await Consulta().Obter(acompanhamento.Id.Valor, Cancelamento))!;

        _relogio.Agora += TimeSpan.FromMinutes(1);
        await Projetar(acompanhamento);
        var depois = (await Consulta().Obter(acompanhamento.Id.Valor, Cancelamento))!;

        Assert.Equal(antes with { AtualizadoEm = _relogio.Agora }, depois);
    }

    [Fact]
    public async Task Cada_empresa_so_ve_as_proprias_obras_em_ordem_de_nome()
    {
        var zeta = await UmAcompanhamentoGravado(LinhaDeBase.Estabelecer("Zeta", Outubro, CasaTerrea.Planilha(), CasaTerrea.Obra()));
        var alfa = await UmAcompanhamentoGravado(LinhaDeBase.Estabelecer("Alfa", Outubro, CasaTerrea.Planilha(), CasaTerrea.Obra()));
        await Projetar(zeta);
        await Projetar(alfa);

        Assert.Equal(["Alfa", "Zeta"], (await Consulta().Listar(Cancelamento)).Select(linha => linha.Obra));
        Assert.Empty(await Consulta(Guid.CreateVersion7()).Listar(Cancelamento));
        Assert.Null(await Consulta(Guid.CreateVersion7()).Obter(alfa.Id.Valor, Cancelamento));

        // Conectado como o dono, que passa por cima do banco, o filtro do código ainda separa.
        var outraComoDono = new ConsultaDoPainel(ContextoDoBanco.Criar(banco.Conexao, new EmpresaFixa(Guid.CreateVersion7())));
        Assert.Null(await outraComoDono.Obter(alfa.Id.Valor, Cancelamento));
    }

    [Fact]
    public async Task Acompanhamento_removido_sai_do_painel()
    {
        var acompanhamento = await UmAcompanhamentoGravado();
        await Projetar(acompanhamento);

        await Acompanhamentos().Remover(acompanhamento.Id, 1, Cancelamento);

        Assert.Null(await Consulta().Obter(acompanhamento.Id.Valor, Cancelamento));
    }

    /// <summary>Um relógio que o teste acerta.</summary>
    private sealed class RelogioDeTeste : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
