using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;

namespace PlanejamentoObras.Dominio.Testes.Medicoes;

public sealed class ReconstituicaoDoAcompanhamentoTests
{
    [Fact]
    public void Refeito_com_as_medicoes_gravadas_da_o_mesmo_boletim()
    {
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();
        var original = new Acompanhamento(linhaDeBase);
        original.RegistrarMedicao(new DateOnly(2026, 10, 1), new LancamentoDeMedicao("2.1", 0.6m, 6_200m));
        original.RegistrarMedicao(new DateOnly(2026, 11, 1), new LancamentoDeMedicao("2.1", -0.1m));

        // A persistência refaz o acompanhamento e registra cada medição de novo.
        var copia = new Acompanhamento(original.Id, linhaDeBase);

        foreach (var medicao in original.Medicoes)
        {
            copia.RegistrarMedicao(
                medicao.Mes,
                medicao.Itens.Select(item => new LancamentoDeMedicao(item.Item.Valor, item.Quantidade, item.CustoReal.Valor)));
        }

        Assert.Equal(original.Id, copia.Id);
        Assert.Equal(
            EmissaoDoBoletim.Emitir(original, 2).Linhas,
            EmissaoDoBoletim.Emitir(copia, 2).Linhas);
    }

    [Fact]
    public void Medicao_gravada_que_passa_do_contratado_e_recusada_na_leitura()
    {
        // As medições voltam pelas regras de sempre: o que a escrita recusaria,
        // a leitura também recusa.
        var copia = new Acompanhamento(AcompanhamentoId.Novo(), CasaTerrea.LinhaDeBaseAprovada());

        Assert.Throws<ExcecaoDeDominio>(() =>
            copia.RegistrarMedicao(new DateOnly(2026, 10, 1), new LancamentoDeMedicao("2.1", 1.5m)));
    }
}
