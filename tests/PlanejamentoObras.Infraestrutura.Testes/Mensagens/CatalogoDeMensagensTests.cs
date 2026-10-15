using System.Text.Json;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;
using PlanejamentoObras.Infraestrutura.Mensagens;

namespace PlanejamentoObras.Infraestrutura.Testes.Mensagens;

/*
 * O catálogo é o contrato com quem processa a caixa de saída: todo evento do
 * domínio tem a sua mensagem, o que se escreve se lê de volta igual, e o
 * formato publicado não muda por acaso — há mensagens gravadas esperando
 * tratamento, escritas pela versão anterior do sistema. O que esta versão não
 * conhece, ela recusa em vez de adivinhar (ADR-0017).
 */
public sealed class CatalogoDeMensagensTests
{
    private static readonly DateOnly Outubro = new(2026, 10, 1);

    /// <summary>Um fato de cada tipo, registrados pelo domínio como numa obra de verdade.</summary>
    private static List<EventoDeDominio> UmFatoDeCadaTipo()
    {
        var acompanhamento = new Acompanhamento(CasaTerrea.LinhaDeBaseAprovada());
        acompanhamento.RegistrarMedicao(Outubro, new LancamentoDeMedicao("2.1", 0.6m, 6_200m));
        acompanhamento.DesfazerUltimaMedicao();

        return [.. acompanhamento.Eventos];
    }

    private static object IdaEVolta(EventoDeDominio fato)
    {
        var (tipo, conteudo) = CatalogoDeMensagens.Escrever(fato);

        return CatalogoDeMensagens.Ler(tipo, CatalogoDeMensagens.Formato, conteudo);
    }

    [Fact]
    public void Todo_evento_do_dominio_tem_mensagem_no_catalogo()
    {
        // Evento novo no domínio falha aqui até ganhar mensagem no catálogo e
        // um lugar no cenário acima.
        var doDominio = typeof(EventoDeDominio).Assembly
            .GetTypes()
            .Where(tipo => tipo.IsSubclassOf(typeof(EventoDeDominio)) && !tipo.IsAbstract)
            .Select(tipo => tipo.Name)
            .Order();

        var fatos = UmFatoDeCadaTipo();

        Assert.Equal(doDominio, fatos.Select(fato => fato.GetType().Name).Order());
        Assert.All(fatos, fato => Assert.NotNull(IdaEVolta(fato)));
    }

    [Fact]
    public void A_mensagem_lida_diz_o_que_o_fato_dizia()
    {
        var fatos = UmFatoDeCadaTipo();
        var iniciado = Assert.IsType<AcompanhamentoIniciado>(fatos[0]);
        var acompanhamento = iniciado.Acompanhamento.Valor;

        Assert.Equal(
            new object[]
            {
                new MensagemDeAcompanhamentoIniciado(acompanhamento, iniciado.LinhaDeBase.Valor),
                new MensagemDeMedicaoRegistrada(acompanhamento, 1, Outubro),
                new MensagemDeMedicaoDesfeita(acompanhamento, 1, Outubro),
            },
            fatos.Select(IdaEVolta));
    }

    [Fact]
    public void O_formato_publicado_e_este()
    {
        // Mudar o nome de um campo quebra as mensagens que já estão na fila:
        // quem muda o formato muda também o número dele.
        var acompanhamento = AcompanhamentoId.Novo();

        var (tipo, conteudo) = CatalogoDeMensagens.Escrever(new MedicaoRegistrada(acompanhamento, 2, new DateOnly(2026, 11, 1)));

        Assert.Equal((short)1, CatalogoDeMensagens.Formato);
        Assert.Equal("medicao-registrada", tipo);
        Assert.Equal($$"""{"acompanhamento":"{{acompanhamento.Valor}}","numero":2,"mes":"2026-11-01"}""", conteudo);
    }

    [Theory]
    [InlineData("medicao-registrada", (short)2)]
    [InlineData("medicao-reaberta", (short)1)]
    public void Tipo_ou_formato_que_esta_versao_nao_conhece_e_recusado(string tipo, short formato)
    {
        var conteudo = $$"""{"acompanhamento":"{{Guid.CreateVersion7()}}","numero":1,"mes":"2026-10-01"}""";

        Assert.Throws<InvalidOperationException>(() => CatalogoDeMensagens.Ler(tipo, formato, conteudo));
    }

    [Theory]
    [InlineData("""{"acompanhamento":"0199a3c0-0000-7000-8000-000000000001","numero":1,"mes":"2026-10-01","motivo":"x"}""")]
    [InlineData("""{"acompanhamento":"0199a3c0-0000-7000-8000-000000000001","numero":1}""")]
    [InlineData("null")]
    public void Conteudo_que_nao_e_o_da_mensagem_nao_se_le(string conteudo)
    {
        var erro = Record.Exception(() => CatalogoDeMensagens.Ler("medicao-registrada", CatalogoDeMensagens.Formato, conteudo));

        Assert.True(erro is JsonException or InvalidOperationException, erro?.ToString() ?? "Leu sem reclamar.");
    }

    [Fact]
    public void Fato_sem_mensagem_no_catalogo_nao_se_publica()
    {
        var erro = Assert.Throws<InvalidOperationException>(() =>
            CatalogoDeMensagens.Escrever(new FatoQueOCatalogoNaoConhece(AcompanhamentoId.Novo())));

        Assert.Contains(nameof(FatoQueOCatalogoNaoConhece), erro.Message, StringComparison.Ordinal);
    }

    private sealed record FatoQueOCatalogoNaoConhece(AcompanhamentoId Acompanhamento) : EventoDeDominio
    {
        public override Identificador Agregado => Acompanhamento;
    }
}
