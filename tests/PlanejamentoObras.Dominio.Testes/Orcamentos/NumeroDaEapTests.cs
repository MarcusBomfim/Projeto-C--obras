using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Dominio.Testes.Orcamentos;

public sealed class NumeroDaEapTests
{
    [Theory]
    [InlineData("2", "2", 1)]
    [InlineData(" 2.1.3 ", "2.1.3", 3)]
    [InlineData("02.01", "2.1", 2)]
    public void Le_o_numero_e_sabe_o_nivel(string texto, string esperado, int nivel)
    {
        var numero = NumeroDaEap.Ler(texto);

        Assert.Equal(esperado, numero.Valor);
        Assert.Equal(nivel, numero.Nivel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("2..1")]
    [InlineData("2.a")]
    [InlineData("-1")]
    [InlineData("2.1.")]
    public void Recusa_o_que_nao_e_numero_de_EAP(string texto)
    {
        Assert.Throws<ExcecaoDeDominio>(() => NumeroDaEap.Ler(texto));
    }

    [Fact]
    public void Devolve_as_posicoes()
    {
        Assert.Equal([2, 1, 3], NumeroDaEap.Ler("2.1.3").Posicoes());
    }

    [Fact]
    public void Numeros_escritos_de_jeitos_diferentes_sao_o_mesmo()
    {
        Assert.Equal(NumeroDaEap.Ler("2.1"), NumeroDaEap.Ler("02.01"));
    }
}
