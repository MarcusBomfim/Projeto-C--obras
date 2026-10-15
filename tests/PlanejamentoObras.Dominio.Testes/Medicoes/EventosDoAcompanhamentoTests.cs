using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;

namespace PlanejamentoObras.Dominio.Testes.Medicoes;

/*
 * Os fatos que o acompanhamento registra: o começo, e cada medição
 * registrada ou desfeita — na ordem em que aconteceram, e só os que
 * aconteceram (ADR-0016).
 */
public sealed class EventosDoAcompanhamentoTests
{
    private static readonly DateOnly Outubro = new(2026, 10, 30);

    private static readonly DateOnly Novembro = new(2026, 11, 30);

    [Fact]
    public void Comecar_a_acompanhar_a_obra_e_um_fato()
    {
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();
        var acompanhamento = new Acompanhamento(linhaDeBase);

        Assert.Equal(new AcompanhamentoIniciado(acompanhamento.Id, linhaDeBase.Id), Assert.Single(acompanhamento.Eventos));
    }

    [Fact]
    public void Cada_medicao_registrada_ou_desfeita_e_um_fato_na_ordem_em_que_aconteceu()
    {
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());

        acompanhamento.RegistrarMedicao(Outubro, new LancamentoDeMedicao("2.1", 0.5m, 1_000m));
        acompanhamento.RegistrarMedicao(Novembro);
        acompanhamento.DesfazerUltimaMedicao();

        Assert.Equal(
            new EventoDeDominio[]
            {
                new AcompanhamentoIniciado(acompanhamento.Id, acompanhamento.LinhaDeBase.Id),
                new MedicaoRegistrada(acompanhamento.Id, 1, new DateOnly(2026, 10, 1)),
                new MedicaoRegistrada(acompanhamento.Id, 2, new DateOnly(2026, 11, 1)),
                new MedicaoDesfeita(acompanhamento.Id, 2, new DateOnly(2026, 11, 1)),
            },
            acompanhamento.Eventos);
        Assert.All(acompanhamento.Eventos, evento => Assert.Equal<Identificador>(acompanhamento.Id, evento.Agregado));
    }

    [Fact]
    public void O_que_a_regra_recusa_nao_aconteceu()
    {
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());

        Assert.Throws<ExcecaoDeDominio>(() => acompanhamento.RegistrarMedicao(Outubro, new LancamentoDeMedicao("2.1", 99m)));

        Assert.IsType<AcompanhamentoIniciado>(Assert.Single(acompanhamento.Eventos));
    }

    [Fact]
    public void Descartados_os_eventos_sobram_so_os_que_vierem_depois()
    {
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());
        acompanhamento.DescartarEventos();

        acompanhamento.RegistrarMedicao(Outubro);

        Assert.Equal(new MedicaoRegistrada(acompanhamento.Id, 1, new DateOnly(2026, 10, 1)), Assert.Single(acompanhamento.Eventos));
    }
}
