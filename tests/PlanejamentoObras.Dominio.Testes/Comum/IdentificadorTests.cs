using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;

namespace PlanejamentoObras.Dominio.Testes.Comum;

public sealed class IdentificadorTests
{
    [Fact]
    public void Recusa_o_guid_vazio()
    {
        Assert.Throws<ExcecaoDeDominio>(() => new CronogramaId(Guid.Empty));
    }

    [Fact]
    public void Ids_novos_sao_guid_versao_7()
    {
        // A versão 7 começa pelo instante da criação: ids em sequência no índice.
        Assert.Equal(7, CronogramaId.Novo().Valor.Version);
    }

    [Fact]
    public void Mesmo_guid_em_tipos_diferentes_nao_e_o_mesmo_id()
    {
        var guid = Guid.CreateVersion7();

        Assert.Equal(new CronogramaId(guid), new CronogramaId(guid));
        Assert.NotEqual<Identificador>(new CronogramaId(guid), new OrcamentoId(guid));
    }

    [Fact]
    public void Escreve_o_guid()
    {
        var guid = Guid.CreateVersion7();

        Assert.Equal(guid.ToString(), new BaseDeCustosId(guid).ToString());
    }

    [Fact]
    public void Todo_agregado_nasce_com_identidade_propria()
    {
        var primeiro = new Cronograma("Torre A");
        var segundo = new Cronograma("Torre A");
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();

        Assert.NotEqual(primeiro.Id, segundo.Id);
        Assert.NotNull(new BaseDeCustos("Base", new DateOnly(2026, 9, 1)).Id);
        Assert.NotNull(new Orcamento("Orçamento", Bdi.Zero).Id);
        Assert.NotNull(linhaDeBase.Id);
        Assert.NotEqual(linhaDeBase.Id.Valor, new Acompanhamento(linhaDeBase).Id.Valor);
    }
}
