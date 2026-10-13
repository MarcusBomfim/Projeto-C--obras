using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Custos;

namespace PlanejamentoObras.Dominio.Testes.Custos;

public sealed class CodigoDeCustoTests
{
    [Theory]
    [InlineData(" 00007271 ", "00007271")]
    [InlineData("alv-01", "ALV-01")]
    [InlineData("arg.1_2_8", "ARG.1_2_8")]
    public void Normaliza_para_maiusculas_sem_espacos(string digitado, string esperado)
    {
        Assert.Equal(esperado, new CodigoDeCusto(digitado).Valor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ARGAMASSA-DE-ASSENTAMENTO")]
    [InlineData("ARG 1:2:8")]
    [InlineData("FUNDAÇÃO")]
    public void Recusa_codigo_vazio_longo_ou_com_caractere_invalido(string digitado)
    {
        Assert.Throws<ExcecaoDeDominio>(() => new CodigoDeCusto(digitado));
    }

    [Fact]
    public void Codigos_iguais_em_caixa_diferente_sao_o_mesmo()
    {
        Assert.Equal(new CodigoDeCusto("alv"), new CodigoDeCusto("ALV"));
    }
}
