using PlanejamentoObras.Dominio.Calendarios;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;

/// <summary>
/// Uma obra pequena, com orçamento e cronograma vinculados: a referência dos
/// testes do físico-financeiro, da linha de base e do valor agregado.
/// </summary>
/// <remarks>
/// <code>
/// Obra a partir da segunda 19/10/2026, de segunda a sexta, com os feriados
/// de 02/11 (Finados) e 20/11 (Consciência Negra).
///
///   SER  serviços preliminares, 5 dias   19/10 a 23/10          outubro: 5
///   PLA  placa da obra, 2 dias, solta    cedo 19/10 a 20/10     outubro: 2
///                                        tarde 16/11 a 17/11    novembro: 2
///   FUN  fundações, 6 dias, após SER     26/10 a 30/10 e 03/11  outubro: 5, novembro: 1
///   ALV  alvenaria, 10 dias, após FUN    04/11 a 17/11          novembro: 10
///   ENT  entrega, marco, após ALV        17/11                  novembro
///
/// Orçamento com BDI de 25 %, um item por atividade:
///
///   1.1  SER  10.000,00   outubro
///   1.2  PLA   1.000,00   outubro (cedo) ou novembro (tarde)
///   2.1  FUN  10.000,00   5/6 e 1/6: 8.333,333… e 1.666,666… → 8.333,33 e 1.666,67
///   2.2  ALV  20.000,00   novembro
///   2.3  ENT     500,00   novembro, no dia do marco
///
///   cedo:   outubro 19.333,33   novembro 22.166,67   total 41.500,00
///   tarde:  outubro 18.333,33   novembro 23.166,67
/// </code>
/// </remarks>
internal static class CasaTerrea
{
    public static readonly CalendarioDeTrabalho Calendario =
        CalendarioDeTrabalho.DeSegundaASexta([new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 20)]);

    public static CronogramaProgramado Obra()
    {
        var cronograma = new Cronograma("Casa térrea");
        cronograma.AdicionarAtividade("SER", "Serviços preliminares", 5);
        cronograma.AdicionarAtividade("PLA", "Placa da obra", 2);
        cronograma.AdicionarAtividade("FUN", "Fundações", 6);
        cronograma.AdicionarAtividade("ALV", "Alvenaria", 10);
        cronograma.AdicionarAtividade("ENT", "Entrega", 0);
        cronograma.AdicionarDependencia("SER", "FUN");
        cronograma.AdicionarDependencia("FUN", "ALV");
        cronograma.AdicionarDependencia("ALV", "ENT");

        return ProgramacaoDoCronograma.Programar(cronograma, new DateOnly(2026, 10, 19), Calendario);
    }

    /// <summary>A linha de base da obra, aprovada em 15/10/2026.</summary>
    public static LinhaDeBase LinhaDeBaseAprovada(DatasDoCronograma datas = DatasDoCronograma.Cedo) =>
        LinhaDeBase.Estabelecer("Linha de base 1", new DateOnly(2026, 10, 15), Planilha(), Obra(), datas);

    /// <summary>A planilha da obra; <paramref name="ajustar"/> mexe no orçamento antes do cálculo.</summary>
    public static OrcamentoCalculado Planilha(Action<Orcamento>? ajustar = null)
    {
        var baseDeCustos = new BaseDeCustos("Verbas", new DateOnly(2026, 9, 1));
        baseDeCustos.AdicionarInsumo("SERV", "Serviços preliminares", "vb", TipoDeInsumo.Material, 8_000m);
        baseDeCustos.AdicionarInsumo("PLAC", "Placa da obra", "un", TipoDeInsumo.Material, 800m);
        baseDeCustos.AdicionarInsumo("FUND", "Fundações", "vb", TipoDeInsumo.Material, 8_000m);
        baseDeCustos.AdicionarInsumo("ALVE", "Alvenaria", "vb", TipoDeInsumo.Material, 16_000m);
        baseDeCustos.AdicionarInsumo("LIMP", "Limpeza e entrega", "vb", TipoDeInsumo.Material, 400m);

        var orcamento = new Orcamento("Casa térrea", Bdi.Informado(25m));
        orcamento.AdicionarEtapa("Serviços iniciais");
        orcamento.AdicionarItem("1", "SERV", 1m);
        orcamento.AdicionarItem("1", "PLAC", 1m);
        orcamento.AdicionarEtapa("Obra");
        orcamento.AdicionarItem("2", "FUND", 1m);
        orcamento.AdicionarItem("2", "ALVE", 1m);
        orcamento.AdicionarItem("2", "LIMP", 1m);

        orcamento.VincularAtividade("1.1", "SER");
        orcamento.VincularAtividade("1.2", "PLA");
        orcamento.VincularAtividade("2.1", "FUN");
        orcamento.VincularAtividade("2.2", "ALV");
        orcamento.VincularAtividade("2.3", "ENT");

        ajustar?.Invoke(orcamento);

        return CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos);
    }
}
