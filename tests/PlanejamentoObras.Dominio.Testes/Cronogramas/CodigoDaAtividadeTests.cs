using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;

namespace PlanejamentoObras.Dominio.Testes.Cronogramas;

public sealed class CodigoDaAtividadeTests
{
    [Fact]
    public void Normaliza_para_maiusculas_sem_espacos_nas_pontas()
    {
        Assert.Equal("FUN-010", new CodigoDaAtividade("  fun-010 ").Valor);
    }

    [Fact]
    public void Dois_codigos_com_o_mesmo_valor_sao_iguais_em_qualquer_caixa()
    {
        Assert.Equal(new CodigoDaAtividade("est.02"), new CodigoDaAtividade("EST.02"));
    }

    [Theory]
    [InlineData("A1")]
    [InlineData("FUN-010")]
    [InlineData("EST.02.A")]
    [InlineData("ALV_PAV1")]
    public void Aceita_letras_numeros_ponto_hifen_e_sublinhado(string codigo)
    {
        Assert.Equal(codigo, new CodigoDaAtividade(codigo).Valor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Recusa_codigo_vazio(string codigo)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new CodigoDaAtividade(codigo));

        Assert.Contains("obrigatório", erro.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("FUN 010")]
    [InlineData("EST/02")]
    [InlineData("FUNDAÇÃO")]
    public void Recusa_espaco_barra_e_acento(string codigo)
    {
        Assert.Throws<ExcecaoDeDominio>(() => new CodigoDaAtividade(codigo));
    }

    [Fact]
    public void Recusa_codigo_maior_que_o_limite()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new CodigoDaAtividade(new string('A', 21)));

        Assert.Contains("no máximo 20", erro.Message, StringComparison.Ordinal);
    }
}
