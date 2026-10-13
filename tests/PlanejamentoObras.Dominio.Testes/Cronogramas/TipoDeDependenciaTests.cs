using PlanejamentoObras.Dominio.Cronogramas;

namespace PlanejamentoObras.Dominio.Testes.Cronogramas;

public sealed class TipoDeDependenciaTests
{
    [Theory]
    [InlineData(TipoDeDependencia.TerminoInicio, "TI")]
    [InlineData(TipoDeDependencia.InicioInicio, "II")]
    [InlineData(TipoDeDependencia.TerminoTermino, "TT")]
    [InlineData(TipoDeDependencia.InicioTermino, "IT")]
    public void Cada_tipo_tem_a_sigla_do_ms_project(TipoDeDependencia tipo, string sigla)
    {
        Assert.Equal(sigla, tipo.Sigla);
    }

    /*
     * A tabela que o cálculo do caminho crítico inteiro usa. Se uma destas
     * linhas estiver errada, todo vínculo daquele tipo é calculado do ponto
     * errado — por isso ela tem teste próprio, separado do algoritmo.
     */
    [Theory]
    [InlineData(TipoDeDependencia.TerminoInicio, true, false)]
    [InlineData(TipoDeDependencia.InicioInicio, false, false)]
    [InlineData(TipoDeDependencia.TerminoTermino, true, true)]
    [InlineData(TipoDeDependencia.InicioTermino, false, true)]
    public void Cada_tipo_diz_de_que_ponto_parte_e_qual_ponto_restringe(
        TipoDeDependencia tipo,
        bool partindoDoTermino,
        bool restringindoOTermino)
    {
        Assert.Equal(partindoDoTermino, tipo.AncoraNoTerminoDaPredecessora);
        Assert.Equal(restringindoOTermino, tipo.RestringeOTerminoDaSucessora);
    }
}
