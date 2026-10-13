using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;

namespace PlanejamentoObras.Dominio.Testes.Cronogramas;

public sealed class CronogramaTests
{
    private static Cronograma ComTresAtividades()
    {
        var cronograma = new Cronograma("Residencial Vista Serra");
        cronograma.AdicionarAtividade("FUN", "Fundações", 10);
        cronograma.AdicionarAtividade("EST", "Estrutura", 20);
        cronograma.AdicionarAtividade("ALV", "Alvenaria", 15);

        return cronograma;
    }

    // ---------- o próprio cronograma ----------

    [Fact]
    public void Nasce_vazio_e_com_o_nome_aparado()
    {
        var cronograma = new Cronograma("  Torre A  ");

        Assert.Equal("Torre A", cronograma.Nome);
        Assert.Empty(cronograma.Atividades);
        Assert.Empty(cronograma.Dependencias);
    }

    [Fact]
    public void Recusa_cronograma_sem_nome()
    {
        Assert.Throws<ExcecaoDeDominio>(() => new Cronograma(" "));
    }

    // ---------- atividades ----------

    [Fact]
    public void Adiciona_atividade_e_a_encontra_pelo_codigo_em_qualquer_caixa()
    {
        var cronograma = ComTresAtividades();

        var estrutura = cronograma.Atividade("est");

        Assert.Equal("Estrutura", estrutura.Descricao);
        Assert.Equal(20, estrutura.DuracaoEmDiasUteis);
        Assert.True(cronograma.ContemAtividade("Fun"));
        Assert.False(cronograma.ContemAtividade("PIN"));
    }

    [Fact]
    public void Mantem_a_ordem_em_que_as_atividades_foram_cadastradas()
    {
        var codigos = ComTresAtividades().Atividades.Select(atividade => atividade.Codigo.Valor);

        Assert.Equal(["FUN", "EST", "ALV"], codigos);
    }

    [Fact]
    public void Recusa_dois_codigos_iguais_mesmo_com_caixa_diferente()
    {
        var cronograma = ComTresAtividades();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => cronograma.AdicionarAtividade("fun", "Outra", 3));

        Assert.Contains("Já existe a atividade FUN", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Duracao_zero_e_um_marco()
    {
        var cronograma = ComTresAtividades();

        var entrega = cronograma.AdicionarAtividade("ENT", "Entrega das chaves", 0);

        Assert.True(entrega.EhMarco);
        Assert.False(cronograma.Atividade("FUN").EhMarco);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5001)]
    public void Recusa_duracao_negativa_ou_absurda(int duracao)
    {
        Assert.Throws<ExcecaoDeDominio>(() => ComTresAtividades().AdicionarAtividade("X", "Qualquer", duracao));
    }

    [Fact]
    public void Recusa_atividade_sem_descricao()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => ComTresAtividades().AdicionarAtividade("X", "  ", 3));

        Assert.Contains("X precisa de uma descrição", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Altera_duracao_e_descricao_pela_raiz()
    {
        var cronograma = ComTresAtividades();

        cronograma.AlterarDuracaoDaAtividade("ALV", 18);
        cronograma.AlterarDescricaoDaAtividade("ALV", "Alvenaria de vedação");

        Assert.Equal(18, cronograma.Atividade("ALV").DuracaoEmDiasUteis);
        Assert.Equal("Alvenaria de vedação", cronograma.Atividade("ALV").Descricao);
    }

    [Fact]
    public void A_alteracao_tambem_valida()
    {
        Assert.Throws<ExcecaoDeDominio>(() => ComTresAtividades().AlterarDuracaoDaAtividade("ALV", -5));
    }

    [Fact]
    public void Acusa_atividade_inexistente_pelo_codigo()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => ComTresAtividades().Atividade("PIN"));

        Assert.Contains("Não existe a atividade PIN", erro.Message, StringComparison.Ordinal);
    }

    // ---------- vínculos ----------

    [Fact]
    public void Vinculo_padrao_e_termino_inicio_sem_defasagem()
    {
        var cronograma = ComTresAtividades();

        var vinculo = cronograma.AdicionarDependencia("FUN", "EST");

        Assert.Equal(TipoDeDependencia.TerminoInicio, vinculo.Tipo);
        Assert.Equal(0, vinculo.DefasagemEmDiasUteis);
        Assert.Equal("FUN → EST (TI)", vinculo.ToString());
    }

    [Fact]
    public void Aceita_outros_tipos_e_defasagem_negativa()
    {
        var cronograma = ComTresAtividades();

        var vinculo = cronograma.AdicionarDependencia("EST", "ALV", TipoDeDependencia.InicioInicio, -2);

        Assert.Equal("EST → ALV (II-2)", vinculo.ToString());
        Assert.Equal("FUN → EST (TI+3)", cronograma.AdicionarDependencia("FUN", "EST", defasagemEmDiasUteis: 3).ToString());
    }

    [Fact]
    public void Recusa_vinculo_com_atividade_que_nao_existe()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => ComTresAtividades().AdicionarDependencia("FUN", "PIN"));

        Assert.Contains("PIN", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_atividade_que_depende_de_si_mesma()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => ComTresAtividades().AdicionarDependencia("EST", "est"));

        Assert.Contains("não pode depender de si mesma", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_segundo_vinculo_no_mesmo_par()
    {
        var cronograma = ComTresAtividades();
        cronograma.AdicionarDependencia("FUN", "EST");

        var erro = Assert.Throws<ExcecaoDeDominio>(() =>
            cronograma.AdicionarDependencia("FUN", "EST", TipoDeDependencia.InicioInicio));

        Assert.Contains("remova o atual e crie outro", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_defasagem_absurda()
    {
        Assert.Throws<ExcecaoDeDominio>(() =>
            ComTresAtividades().AdicionarDependencia("FUN", "EST", defasagemEmDiasUteis: 5001));
    }

    [Fact]
    public void Recusa_tipo_de_vinculo_fora_do_enum()
    {
        Assert.Throws<ExcecaoDeDominio>(() =>
            ComTresAtividades().AdicionarDependencia("FUN", "EST", (TipoDeDependencia)99));
    }

    // ---------- ciclos ----------

    [Fact]
    public void Recusa_ciclo_entre_duas_atividades_e_mostra_qual_e()
    {
        var cronograma = ComTresAtividades();
        cronograma.AdicionarDependencia("FUN", "EST");

        var erro = Assert.Throws<ExcecaoDeDominio>(() => cronograma.AdicionarDependencia("EST", "FUN"));

        Assert.Contains("fecharia um ciclo: FUN → EST → FUN", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_ciclo_longo_e_mostra_o_caminho_inteiro()
    {
        var cronograma = ComTresAtividades();
        cronograma.AdicionarDependencia("FUN", "EST");
        cronograma.AdicionarDependencia("EST", "ALV");

        var erro = Assert.Throws<ExcecaoDeDominio>(() => cronograma.AdicionarDependencia("ALV", "FUN"));

        Assert.Contains("FUN → EST → ALV → FUN", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ciclo_e_recusado_qualquer_que_seja_o_tipo_do_vinculo()
    {
        /*
         * Em teoria, um "ciclo" só de vínculos início-início poderia ser
         * satisfeito. Na prática, todo software de planejamento recusa
         * relação circular — e o cálculo depende de uma ordem topológica, que
         * só existe sem ciclo. Ver ADR-0002.
         */
        var cronograma = ComTresAtividades();
        cronograma.AdicionarDependencia("FUN", "EST", TipoDeDependencia.InicioInicio);

        Assert.Throws<ExcecaoDeDominio>(() =>
            cronograma.AdicionarDependencia("EST", "FUN", TipoDeDependencia.InicioInicio));
    }

    [Fact]
    public void Dois_caminhos_que_convergem_nao_sao_ciclo()
    {
        // Losango: FUN abre EST e INS em paralelo, e os dois fecham em ALV.
        var cronograma = ComTresAtividades();
        cronograma.AdicionarAtividade("INS", "Instalações", 12);
        cronograma.AdicionarDependencia("FUN", "EST");
        cronograma.AdicionarDependencia("FUN", "INS");
        cronograma.AdicionarDependencia("EST", "ALV");
        cronograma.AdicionarDependencia("INS", "ALV");

        Assert.Equal(4, cronograma.Dependencias.Count);
    }

    [Fact]
    public void Ciclo_recusado_nao_deixa_rastro_na_rede()
    {
        var cronograma = ComTresAtividades();
        cronograma.AdicionarDependencia("FUN", "EST");

        Assert.Throws<ExcecaoDeDominio>(() => cronograma.AdicionarDependencia("EST", "FUN"));

        Assert.Single(cronograma.Dependencias);
    }

    // ---------- remoções ----------

    [Fact]
    public void Remover_atividade_remove_os_vinculos_dela()
    {
        var cronograma = ComTresAtividades();
        cronograma.AdicionarDependencia("FUN", "EST");
        cronograma.AdicionarDependencia("EST", "ALV");
        cronograma.AdicionarDependencia("FUN", "ALV");

        cronograma.RemoverAtividade("EST");

        Assert.False(cronograma.ContemAtividade("EST"));
        Assert.Equal("FUN → ALV (TI)", Assert.Single(cronograma.Dependencias).ToString());
    }

    [Fact]
    public void Remove_um_vinculo()
    {
        var cronograma = ComTresAtividades();
        cronograma.AdicionarDependencia("FUN", "EST");

        cronograma.RemoverDependencia("fun", "est");

        Assert.Empty(cronograma.Dependencias);
    }

    [Fact]
    public void Acusa_remocao_de_vinculo_que_nao_existe()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => ComTresAtividades().RemoverDependencia("FUN", "EST"));

        Assert.Contains("Não existe vínculo de FUN para EST", erro.Message, StringComparison.Ordinal);
    }

    // ---------- encapsulamento ----------

    [Fact]
    public void A_lista_de_vinculos_exposta_nao_aceita_alteracao_por_fora()
    {
        var cronograma = ComTresAtividades();
        cronograma.AdicionarDependencia("FUN", "EST");

        /*
         * Se a lista interna escapasse, bastaria convertê-la de volta e
         * limpar — ou acrescentar um vínculo que fecha ciclo — sem passar por
         * regra nenhuma.
         */
        var exposta = (ICollection<Dependencia>)cronograma.Dependencias;

        Assert.Throws<NotSupportedException>(exposta.Clear);
        Assert.Single(cronograma.Dependencias);
    }
}
