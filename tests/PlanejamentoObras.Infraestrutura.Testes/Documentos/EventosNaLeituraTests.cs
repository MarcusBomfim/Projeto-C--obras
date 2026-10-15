using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;

namespace PlanejamentoObras.Infraestrutura.Testes.Documentos;

/*
 * Ler um acompanhamento do banco registra as medições de novo, uma a uma,
 * pelas regras de sempre (ADR-0010). Mas esses fatos já tinham acontecido —
 * e sido publicados: o agregado lido não traz evento nenhum (ADR-0016).
 */
public sealed class EventosNaLeituraTests
{
    [Fact]
    public void O_acompanhamento_lido_nao_traz_de_novo_os_fatos_de_quando_foi_gravado()
    {
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());
        acompanhamento.RegistrarMedicao(new DateOnly(2026, 10, 1), new LancamentoDeMedicao("2.1", 0.6m, 6_200m));

        var lido = RegistroDeAcompanhamento.De(acompanhamento, versao: 2, DateTimeOffset.UnixEpoch)
            .ParaAcompanhamento(acompanhamento.LinhaDeBase);

        Assert.Single(lido.Medicoes);
        Assert.Empty(lido.Eventos);
    }
}
