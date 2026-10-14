using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;

namespace PlanejamentoObras.Dominio.Testes.Cronogramas;

/*
 * A reconstituição é o caminho pelo qual a persistência refaz um cronograma
 * gravado (ADR-0010). Tem que devolver o mesmo cronograma — e recusar o que
 * a escrita normal recusaria, mesmo que tenha vindo gravado.
 */
public sealed class ReconstituicaoDoCronogramaTests
{
    private static Cronograma Reconstituir(
        CronogramaId id,
        string nome,
        (string, string, int)[] atividades,
        (string, string, TipoDeDependencia, int)[] dependencias) =>
        Cronograma.Reconstituir(id, nome, atividades, dependencias);

    private static Cronograma Copia(Cronograma original) =>
        Cronograma.Reconstituir(
            original.Id,
            original.Nome,
            original.Atividades.Select(atividade => (atividade.Codigo.Valor, atividade.Descricao, atividade.DuracaoEmDiasUteis)),
            original.Dependencias.Select(vinculo => (vinculo.Predecessora.Valor, vinculo.Sucessora.Valor, vinculo.Tipo, vinculo.DefasagemEmDiasUteis)));

    [Fact]
    public void Devolve_o_mesmo_cronograma_na_mesma_ordem()
    {
        foreach (var semente in Enumerable.Range(0, 50))
        {
            var original = RedesAleatorias.Gerar(semente);
            var copia = Copia(original);

            Assert.Equal(original.Id, copia.Id);
            Assert.Equal(original.Nome, copia.Nome);
            Assert.Equal(
                original.Atividades.Select(atividade => (atividade.Codigo, atividade.Descricao, atividade.DuracaoEmDiasUteis)),
                copia.Atividades.Select(atividade => (atividade.Codigo, atividade.Descricao, atividade.DuracaoEmDiasUteis)));
            Assert.Equal(original.Dependencias, copia.Dependencias);

            // O que importa no fim: o cálculo dá o mesmo resultado, desempates inclusive.
            Assert.Equal(
                CalculadoraDeCaminhoCritico.Analisar(original).Atividades,
                CalculadoraDeCaminhoCritico.Analisar(copia).Atividades);
        }
    }

    [Fact]
    public void Recusa_ciclo_que_veio_gravado()
    {
        var id = CronogramaId.Novo();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Reconstituir(
            id,
            "Corrompido",
            [("A", "A", 1), ("B", "B", 1), ("C", "C", 1), ("D", "D", 1)],
            [
                ("A", "B", TipoDeDependencia.TerminoInicio, 0),
                ("B", "C", TipoDeDependencia.TerminoInicio, 0),
                ("C", "B", TipoDeDependencia.InicioInicio, 0),
                ("C", "D", TipoDeDependencia.TerminoInicio, 0),
            ]));

        // B e C estão no laço; D depende dele. A, antes, não é citada.
        Assert.Equal(
            "A rede do cronograma Corrompido tem um ciclo: as atividades B, C, D estão num laço ou depois dele.",
            erro.Message);
    }

    [Fact]
    public void Recusa_o_que_a_escrita_normal_recusaria()
    {
        var id = CronogramaId.Novo();
        (string, string, int)[] duas = [("A", "A", 1), ("B", "B", 1)];

        Assert.Throws<ExcecaoDeDominio>(() => Reconstituir(id, "X", [("A", "A", 1), ("a", "Outra", 1)], []));
        Assert.Throws<ExcecaoDeDominio>(() => Reconstituir(id, "X", [("A", "A", -1)], []));
        Assert.Throws<ExcecaoDeDominio>(() => Reconstituir(id, "X", duas, [("A", "Z", TipoDeDependencia.TerminoInicio, 0)]));
        Assert.Throws<ExcecaoDeDominio>(() => Reconstituir(id, "X", duas, [("A", "A", TipoDeDependencia.TerminoInicio, 0)]));
        Assert.Throws<ExcecaoDeDominio>(() => Reconstituir(
            id,
            "X",
            duas,
            [("A", "B", TipoDeDependencia.TerminoInicio, 0), ("A", "B", TipoDeDependencia.InicioInicio, 2)]));
        Assert.Throws<ExcecaoDeDominio>(() => Reconstituir(id, " ", duas, []));
    }

    [Fact]
    public void Depois_de_reconstituido_continua_guardando_as_regras()
    {
        var cronograma = Reconstituir(
            CronogramaId.Novo(),
            "Refeito",
            [("A", "A", 1), ("B", "B", 1), ("C", "C", 1)],
            [("A", "B", TipoDeDependencia.TerminoInicio, 0), ("B", "C", TipoDeDependencia.TerminoInicio, 0)]);

        var ciclo = Assert.Throws<ExcecaoDeDominio>(() => cronograma.AdicionarDependencia("C", "A"));
        Assert.Contains("fecharia um ciclo: A → B → C → A", ciclo.Message, StringComparison.Ordinal);

        Assert.Throws<ExcecaoDeDominio>(() => cronograma.AdicionarDependencia("A", "B"));

        cronograma.RemoverDependencia("A", "B");
        cronograma.AdicionarDependencia("A", "B", TipoDeDependencia.InicioInicio, 1);
        Assert.Equal(TipoDeDependencia.InicioInicio, cronograma.Dependencias[^1].Tipo);
    }
}
