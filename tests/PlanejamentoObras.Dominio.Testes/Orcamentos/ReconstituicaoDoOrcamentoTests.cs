using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Testes.Orcamentos;

public sealed class ReconstituicaoDoOrcamentoTests
{
    private static ParcelasDoBdi ParcelasDeEdificio() => new(4.00m, 0.40m, 1.27m, 0.40m, 1.23m, 7.40m, 6.15m);

    [Fact]
    public void O_BDI_volta_como_foi_gravado_sem_recalcular()
    {
        /*
         * Pela fórmula, estas parcelas dão 22,88 %. Gravado com 22,87 % — por
         * uma regra de arredondamento antiga, digamos —, é 22,87 % que volta:
         * o orçamento fechado não muda de preço na leitura.
         */
        var bdi = Bdi.Reconstituir(22.87m, ParcelasDeEdificio());

        Assert.Equal(22.87m, bdi.Percentual);
        Assert.Equal(ParcelasDeEdificio(), bdi.Parcelas);
        Assert.Equal(22.88m, Bdi.PelaFormulaDoTcu(ParcelasDeEdificio()).Percentual);
    }

    [Fact]
    public void O_BDI_gravado_continua_preso_as_regras_de_faixa_e_casas()
    {
        Assert.Throws<ExcecaoDeDominio>(() => Bdi.Reconstituir(120m, parcelas: null));
        Assert.Throws<ExcecaoDeDominio>(() => Bdi.Reconstituir(22.885m, parcelas: null));
        Assert.Null(Bdi.Reconstituir(25m, parcelas: null).Parcelas);
    }

    [Fact]
    public void Orcamento_reconstituido_guarda_a_identidade_gravada()
    {
        var id = OrcamentoId.Novo();

        var orcamento = new Orcamento(id, "Gravado", Bdi.Informado(25m));

        Assert.Equal(id, orcamento.Id);
        Assert.Empty(orcamento.Etapas);
    }
}
