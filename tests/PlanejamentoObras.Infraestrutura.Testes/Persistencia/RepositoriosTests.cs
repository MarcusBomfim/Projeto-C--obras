using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Dominio.Testes;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;
using PlanejamentoObras.Dominio.Testes.ValorAgregado;
using PlanejamentoObras.Dominio.ValorAgregado;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Infraestrutura.Testes.Persistencia;

/*
 * Os repositórios contra um PostgreSQL de verdade. Cada teste cria os seus
 * agregados — ids novos —, numa empresa só dele, então os testes não se
 * atrapalham nem dependem de ordem.
 */
public sealed class RepositoriosTests(BancoDeTestes banco)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _empresa = Guid.CreateVersion7();

    private readonly RelogioDeTeste _relogio = new();

    private RepositorioDeCronogramas Cronogramas() => new(banco.NovoContexto(_empresa), _relogio);

    private RepositorioDeBasesDeCustos BasesDeCustos() => new(banco.NovoContexto(_empresa), _relogio);

    private RepositorioDeOrcamentos Orcamentos() => new(banco.NovoContexto(_empresa), _relogio);

    private RepositorioDeLinhasDeBase LinhasDeBase() => new(banco.NovoContexto(_empresa), _relogio);

    private RepositorioDeAcompanhamentos Acompanhamentos() => new(banco.NovoContexto(_empresa), _relogio);

    private static Cronograma UmCronograma(string nome = "Casa")
    {
        var cronograma = new Cronograma(nome);
        cronograma.AdicionarAtividade("FUN", "Fundações", 10);
        cronograma.AdicionarAtividade("EST", "Estrutura", 20);
        cronograma.AdicionarDependencia("FUN", "EST");

        return cronograma;
    }

    // ---------- gravar e ler ----------

    [Fact]
    public async Task Todo_agregado_volta_do_banco_como_foi()
    {
        /*
         * Pelo banco de verdade, o documento volta do jsonb sem os espaços e
         * com as chaves reordenadas. O que importa é que o agregado lido
         * calcule o mesmo que o gravado.
         */
        foreach (var semente in Enumerable.Range(0, 20))
        {
            var (cronograma, baseDeCustos, orcamento) = ObrasAleatorias.Agregados(semente);
            var planilha = CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos);

            await Cronogramas().Adicionar(cronograma, Cancelamento);
            await BasesDeCustos().Adicionar(baseDeCustos, Cancelamento);
            await Orcamentos().Adicionar(orcamento, Cancelamento);

            var cronogramaLido = (await Cronogramas().Obter(cronograma.Id, Cancelamento))!.Agregado;
            var baseLida = (await BasesDeCustos().Obter(baseDeCustos.Id, Cancelamento))!.Agregado;
            var orcamentoLido = (await Orcamentos().Obter(orcamento.Id, Cancelamento))!.Agregado;

            Assert.Equal(CalculadoraDeCaminhoCritico.Analisar(cronograma).Atividades, CalculadoraDeCaminhoCritico.Analisar(cronogramaLido).Atividades);
            Assert.Equal(planilha.Linhas, CalculadoraDoOrcamento.Calcular(orcamentoLido, baseLida).Linhas);
            MesmoDocumento(RegistroDeOrcamento.De(orcamento, 1, _relogio.Agora).Documento, RegistroDeOrcamento.De(orcamentoLido, 1, _relogio.Agora).Documento);

            if (planilha.Itens.Count == 0)
            {
                continue;
            }

            var obra = ProgramacaoDoCronograma.Programar(cronograma, new DateOnly(2026, 10, 1).AddDays(semente % 365), ObrasAleatorias.Calendario);
            var linhaDeBase = LinhaDeBase.Estabelecer($"LB {semente}", obra.DataDeInicio, planilha, obra);
            var acompanhamento = new Acompanhamento(linhaDeBase);
            MedicoesAleatorias.Registrar(acompanhamento, semente);

            await LinhasDeBase().Adicionar(linhaDeBase, Cancelamento);
            await Acompanhamentos().Adicionar(acompanhamento, Cancelamento);

            var lido = (await Acompanhamentos().Obter(acompanhamento.Id, Cancelamento))!.Agregado;

            Assert.Equal(linhaDeBase.Id, lido.LinhaDeBase.Id);
            Assert.Equal(acompanhamento.Medicoes.Count, lido.Medicoes.Count);

            if (acompanhamento.Medicoes.Count > 0)
            {
                Assert.Equal(CalculadoraDeValorAgregado.Calcular(acompanhamento).Linhas, CalculadoraDeValorAgregado.Calcular(lido).Linhas);
            }
        }
    }

    [Fact]
    public async Task O_que_nao_esta_gravado_volta_nulo()
    {
        Assert.Null(await Cronogramas().Obter(CronogramaId.Novo(), Cancelamento));
    }

    // ---------- versão e concorrência ----------

    [Fact]
    public async Task A_versao_comeca_em_1_e_sobe_a_cada_gravacao()
    {
        var cronograma = UmCronograma();

        Assert.Equal(1, await Cronogramas().Adicionar(cronograma, Cancelamento));

        var lido = (await Cronogramas().Obter(cronograma.Id, Cancelamento))!;
        lido.Agregado.Renomear("Casa térrea");

        Assert.Equal(2, await Cronogramas().Atualizar(lido.Agregado, lido.Versao, Cancelamento));

        var relido = (await Cronogramas().Obter(cronograma.Id, Cancelamento))!;
        Assert.Equal((2, "Casa térrea"), (relido.Versao, relido.Agregado.Nome));
    }

    [Fact]
    public async Task Quem_grava_por_cima_do_que_outro_gravou_e_recusado()
    {
        /*
         * Ana e Bruno abrem o mesmo cronograma, na versão 1. Ana grava
         * primeiro: versão 2. Bruno tenta gravar a cópia dele, lida na versão
         * 1 — e é recusado, em vez de apagar sem saber o que Ana fez.
         */
        var cronograma = UmCronograma();
        await Cronogramas().Adicionar(cronograma, Cancelamento);

        var deAna = (await Cronogramas().Obter(cronograma.Id, Cancelamento))!;
        var deBruno = (await Cronogramas().Obter(cronograma.Id, Cancelamento))!;

        deAna.Agregado.AlterarDuracaoDaAtividade("FUN", 12);
        await Cronogramas().Atualizar(deAna.Agregado, deAna.Versao, Cancelamento);

        deBruno.Agregado.AlterarDuracaoDaAtividade("EST", 25);
        var erro = await Assert.ThrowsAsync<ConflitoDeConcorrencia>(() =>
            Cronogramas().Atualizar(deBruno.Agregado, deBruno.Versao, Cancelamento));

        Assert.Contains("mudou desde a versão 1: está na versão 2", erro.Message, StringComparison.Ordinal);
        Assert.Equal(2, erro.VersaoAtual);

        var gravado = (await Cronogramas().Obter(cronograma.Id, Cancelamento))!.Agregado;
        Assert.Equal((12, 20), (gravado.Atividade("FUN").DuracaoEmDiasUteis, gravado.Atividade("EST").DuracaoEmDiasUteis));
    }

    [Fact]
    public async Task De_gravacoes_simultaneas_sobre_a_mesma_versao_so_uma_vence()
    {
        // Dez gravações ao mesmo tempo, todas a partir da versão 1, cada uma
        // na sua conexão: o banco deixa passar exatamente uma.
        var cronograma = UmCronograma();
        await Cronogramas().Adicionar(cronograma, Cancelamento);

        var tentativas = Enumerable.Range(1, 10).Select(async duracao =>
        {
            var copia = (await Cronogramas().Obter(cronograma.Id, Cancelamento))!.Agregado;
            copia.AlterarDuracaoDaAtividade("FUN", duracao);

            try
            {
                await Cronogramas().Atualizar(copia, versaoLida: 1, Cancelamento);

                return true;
            }
            catch (ConflitoDeConcorrencia)
            {
                return false;
            }
        });

        var resultados = await Task.WhenAll(tentativas);

        Assert.Equal(1, resultados.Count(venceu => venceu));
        Assert.Equal(2, (await Cronogramas().Obter(cronograma.Id, Cancelamento))!.Versao);
    }

    [Fact]
    public async Task Atualizar_ou_remover_o_que_nao_existe_e_acusado()
    {
        var naoGravado = UmCronograma();

        var atualizar = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => Cronogramas().Atualizar(naoGravado, 1, Cancelamento));
        await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => Cronogramas().Remover(naoGravado.Id, 1, Cancelamento));

        Assert.Equal($"Não existe o cronograma {naoGravado.Id}.", atualizar.Message);
    }

    [Fact]
    public async Task O_mesmo_agregado_nao_entra_duas_vezes()
    {
        var cronograma = UmCronograma();
        await Cronogramas().Adicionar(cronograma, Cancelamento);

        await Assert.ThrowsAsync<AgregadoJaExiste>(() => Cronogramas().Adicionar(cronograma, Cancelamento));
    }

    [Fact]
    public async Task Remover_pede_a_versao_lida()
    {
        var cronograma = UmCronograma();
        await Cronogramas().Adicionar(cronograma, Cancelamento);
        await Cronogramas().Atualizar(cronograma, 1, Cancelamento);

        await Assert.ThrowsAsync<ConflitoDeConcorrencia>(() => Cronogramas().Remover(cronograma.Id, 1, Cancelamento));
        await Cronogramas().Remover(cronograma.Id, 2, Cancelamento);

        Assert.Null(await Cronogramas().Obter(cronograma.Id, Cancelamento));
    }

    // ---------- referências entre agregados ----------

    [Fact]
    public async Task Linha_de_base_em_uso_nao_sai_do_banco()
    {
        var linhasDeBase = LinhasDeBase();
        var acompanhamentos = Acompanhamentos();
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();
        var acompanhamento = new Acompanhamento(linhaDeBase);
        await linhasDeBase.Adicionar(linhaDeBase, Cancelamento);
        await acompanhamentos.Adicionar(acompanhamento, Cancelamento);

        var erro = await Assert.ThrowsAsync<AgregadoEmUso>(() =>
            LinhasDeBase().Remover(linhaDeBase.Id, 1, Cancelamento));

        Assert.Contains("Há acompanhamento medido contra a linha de base", erro.Message, StringComparison.Ordinal);

        await Acompanhamentos().Remover(acompanhamento.Id, 1, Cancelamento);
        await LinhasDeBase().Remover(linhaDeBase.Id, 1, Cancelamento);
    }

    [Fact]
    public async Task Acompanhamento_precisa_da_linha_de_base_gravada_antes()
    {
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());

        var erro = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() =>
            Acompanhamentos().Adicionar(acompanhamento, Cancelamento));

        Assert.Contains("grave-a antes do acompanhamento", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Medicao_nova_grava_por_cima_da_versao_lida()
    {
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();
        var acompanhamento = new Acompanhamento(linhaDeBase);
        await LinhasDeBase().Adicionar(linhaDeBase, Cancelamento);
        await Acompanhamentos().Adicionar(acompanhamento, Cancelamento);

        var lido = (await Acompanhamentos().Obter(acompanhamento.Id, Cancelamento))!;
        lido.Agregado.RegistrarMedicao(new DateOnly(2026, 10, 1), new LancamentoDeMedicao("2.1", 0.6m, 6_200m));
        var versao = await Acompanhamentos().Atualizar(lido.Agregado, lido.Versao, Cancelamento);

        var relido = (await Acompanhamentos().Obter(acompanhamento.Id, Cancelamento))!;
        var listados = await Acompanhamentos().ListarDaLinhaDeBase(linhaDeBase.Id, Cancelamento);

        Assert.Equal(2, versao);
        Assert.Equal(0.6m, Assert.Single(relido.Agregado.Medicoes).Itens[0].Quantidade);
        Assert.Equal(new Versionado<AcompanhamentoId>(acompanhamento.Id, 2), Assert.Single(listados));
    }

    // ---------- colunas ao lado do documento ----------

    [Fact]
    public async Task As_colunas_acompanham_o_documento_a_cada_gravacao()
    {
        // O agregado volta do documento; a listagem, e quem consulta o banco
        // direto, leem as colunas. Gravar muda as duas coisas juntas.
        var cronograma = UmCronograma("Casa");
        var baseDeCustos = new BaseDeCustos("SINAPI", new DateOnly(2026, 9, 1));
        var orcamento = new Orcamento("Casa", Bdi.Zero);
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();
        await Cronogramas().Adicionar(cronograma, Cancelamento);
        await BasesDeCustos().Adicionar(baseDeCustos, Cancelamento);
        await Orcamentos().Adicionar(orcamento, Cancelamento);
        await LinhasDeBase().Adicionar(linhaDeBase, Cancelamento);

        cronograma.Renomear("Casa térrea");
        baseDeCustos.Renomear("SINAPI SP");
        baseDeCustos.AlterarDataBase(new DateOnly(2026, 10, 1));
        orcamento.Renomear("Casa térrea");
        await Cronogramas().Atualizar(cronograma, 1, Cancelamento);
        await BasesDeCustos().Atualizar(baseDeCustos, 1, Cancelamento);
        await Orcamentos().Atualizar(orcamento, 1, Cancelamento);

        await using var contexto = banco.NovoContexto(_empresa);
        var gravadoDoCronograma = await contexto.Cronogramas.AsNoTracking().SingleAsync(registro => registro.Id == cronograma.Id.Valor, Cancelamento);
        var gravadoDaBase = await contexto.BasesDeCustos.AsNoTracking().SingleAsync(registro => registro.Id == baseDeCustos.Id.Valor, Cancelamento);
        var gravadoDoOrcamento = await contexto.Orcamentos.AsNoTracking().SingleAsync(registro => registro.Id == orcamento.Id.Valor, Cancelamento);
        var gravadoDaLinhaDeBase = await contexto.LinhasDeBase.AsNoTracking().SingleAsync(registro => registro.Id == linhaDeBase.Id.Valor, Cancelamento);

        Assert.Equal("Casa térrea", gravadoDoCronograma.Nome);
        Assert.Equal(("SINAPI SP", new DateOnly(2026, 10, 1)), (gravadoDaBase.Nome, gravadoDaBase.DataBase));
        Assert.Equal("Casa térrea", gravadoDoOrcamento.Nome);
        Assert.Equal((linhaDeBase.Nome, linhaDeBase.AprovadaEm), (gravadoDaLinhaDeBase.Nome, gravadoDaLinhaDeBase.AprovadaEm));
    }

    // ---------- leitura de documento estragado ----------

    [Fact]
    public async Task Documento_estragado_no_banco_e_acusado_com_o_id()
    {
        // Alguém editou o banco à mão e fechou um ciclo na rede.
        var cronograma = UmCronograma();
        await Cronogramas().Adicionar(cronograma, Cancelamento);

        await using (var conexao = new NpgsqlConnection(banco.Conexao))
        {
            await conexao.OpenAsync(Cancelamento);
            await using var comando = new NpgsqlCommand(
                """
                update planejamento.cronogramas
                set documento = jsonb_set(documento, '{dependencias}',
                    documento->'dependencias' || '[{"predecessora":"EST","sucessora":"FUN","tipo":"TerminoInicio","defasagemEmDiasUteis":0}]')
                where id = @id
                """,
                conexao);
            comando.Parameters.AddWithValue("id", cronograma.Id.Valor);
            await comando.ExecuteNonQueryAsync(Cancelamento);
        }

        var erro = await Assert.ThrowsAsync<DocumentoInvalido>(() => Cronogramas().Obter(cronograma.Id, Cancelamento));

        Assert.Contains($"O cronograma {cronograma.Id} está gravado de um jeito que não se lê", erro.Message, StringComparison.Ordinal);
        Assert.Contains("tem um ciclo", erro.Message, StringComparison.Ordinal);
    }

    // ---------- listagem e datas ----------

    [Fact]
    public async Task Lista_por_nome_com_versao_e_data_da_ultima_gravacao()
    {
        var marca = Guid.CreateVersion7().ToString("N")[..8];
        var primeiro = new Orcamento(marca, Bdi.Zero);
        var segundo = new Orcamento(marca, Bdi.Zero);

        // Os nomes na ordem contrária à dos ids — e o banco ordena uuid como o
        // Guid se compara —: listar por id em vez de por nome sai invertido.
        var (zeta, alfa) = primeiro.Id.Valor.CompareTo(segundo.Id.Valor) < 0 ? (primeiro, segundo) : (segundo, primeiro);
        zeta.Renomear($"{marca} Zeta");
        alfa.Renomear($"{marca} Alfa");

        _relogio.Agora = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        await Orcamentos().Adicionar(zeta, Cancelamento);
        await Orcamentos().Adicionar(alfa, Cancelamento);

        _relogio.Agora = new DateTimeOffset(2026, 10, 2, 18, 30, 0, TimeSpan.Zero);
        await Orcamentos().Atualizar(zeta, 1, Cancelamento);

        var listados = (await Orcamentos().Listar(Cancelamento))
            .Where(resumo => resumo.Nome.StartsWith(marca, StringComparison.Ordinal))
            .ToList();

        Assert.Equal(
            [
                new ResumoDoAgregado<OrcamentoId>(alfa.Id, $"{marca} Alfa", 1, new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero)),
                new ResumoDoAgregado<OrcamentoId>(zeta.Id, $"{marca} Zeta", 2, new DateTimeOffset(2026, 10, 2, 18, 30, 0, TimeSpan.Zero)),
            ],
            listados);
    }

    private static void MesmoDocumento(string esperado, string lido) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(esperado), JsonNode.Parse(lido)), lido);

    /// <summary>Um relógio que o teste acerta.</summary>
    private sealed class RelogioDeTeste : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
