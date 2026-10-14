using System.Text.Json;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;

namespace PlanejamentoObras.Infraestrutura.Testes.Documentos;

/*
 * Documento que não é exatamente o formato esperado não vira, em silêncio,
 * um agregado com um campo zerado: a leitura falha, e diz por quê (ADR-0011).
 */
public sealed class LeituraEstritaTests
{
    private static RegistroDeCronograma Cronograma(string documento, short formato = 1) => new()
    {
        Id = Guid.CreateVersion7(),
        Nome = "Gravado",
        Versao = 1,
        Formato = formato,
        Documento = documento,
    };

    private const string Valido =
        """{"nome":"Casa","atividades":[{"codigo":"A","descricao":"A","duracaoEmDiasUteis":1}],"dependencias":[]}""";

    [Fact]
    public void Le_o_documento_valido()
    {
        Assert.Equal("Casa", Cronograma(Valido).ParaCronograma().Nome);
    }

    [Theory]
    [InlineData("""{"nome":"Casa","atividades":[],"dependencias":[],"cor":"azul"}""")] // campo que o formato não tem
    [InlineData("""{"nome":"Casa","atividades":[]}""")] // campo obrigatório que falta
    [InlineData("""{"nome":null,"atividades":[],"dependencias":[]}""")] // nulo onde não cabe nulo
    [InlineData("""{"nome":"Casa","atividades":[],"dependencias":[{"predecessora":"A","sucessora":"B","tipo":"Paralelo","defasagemEmDiasUteis":0}]}""")] // tipo de vínculo que não existe
    [InlineData("null")]
    public void Recusa_o_que_nao_e_exatamente_o_formato(string documento)
    {
        Assert.ThrowsAny<JsonException>(() => Cronograma(documento).ParaCronograma());
    }

    [Fact]
    public void Recusa_formato_de_outra_versao()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => Cronograma(Valido, formato: 2).ParaCronograma());

        Assert.Contains("está gravado no formato 2", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_o_que_o_dominio_recusaria()
    {
        // JSON perfeito, rede impossível: o ciclo é pego pela reconstituição.
        var comCiclo = Cronograma(
            """{"nome":"Casa","atividades":[{"codigo":"A","descricao":"A","duracaoEmDiasUteis":1},{"codigo":"B","descricao":"B","duracaoEmDiasUteis":1}],"dependencias":[{"predecessora":"A","sucessora":"B","tipo":"TerminoInicio","defasagemEmDiasUteis":0},{"predecessora":"B","sucessora":"A","tipo":"TerminoInicio","defasagemEmDiasUteis":0}]}""");

        Assert.Throws<ExcecaoDeDominio>(() => comCiclo.ParaCronograma());
    }

    [Fact]
    public void Recusa_linha_de_orcamento_de_tipo_desconhecido()
    {
        var registro = new RegistroDeOrcamento
        {
            Id = Guid.CreateVersion7(),
            Nome = "O",
            Versao = 1,
            Formato = 1,
            Documento = """{"nome":"O","bdi":{"percentual":25},"etapas":[{"titulo":"E","filhos":[{"tipo":"marco","codigo":"X"}]}]}""",
        };

        Assert.ThrowsAny<JsonException>(() => registro.ParaOrcamento());
    }
}
