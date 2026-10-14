using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Infraestrutura.Documentos;

namespace PlanejamentoObras.Infraestrutura.Persistencia.Registros;

/*
 * Um registro por agregado. Cada um sabe virar do agregado — serializando o
 * documento — e voltar para ele, conferindo o formato e passando pela
 * reconstituição do domínio. Os repositórios só movem registros entre a
 * memória e o banco.
 */

internal sealed class RegistroDeCronograma : RegistroComNome
{
    public static RegistroDeCronograma De(Cronograma cronograma, int versao, DateTimeOffset agora)
    {
        var registro = new RegistroDeCronograma { Nome = cronograma.Nome };
        var documento = Json.Escrever(DocumentoDoCronograma.De(cronograma), ContextoJson.Default.DocumentoDoCronograma);

        return registro.Preencher<RegistroDeCronograma>(cronograma.Id.Valor, versao, DocumentoDoCronograma.Formato, documento, agora);
    }

    public Cronograma ParaCronograma()
    {
        ConferirFormato(DocumentoDoCronograma.Formato, "O cronograma");

        return Json.Ler(Documento, ContextoJson.Default.DocumentoDoCronograma).ParaCronograma(new CronogramaId(Id));
    }
}

internal sealed class RegistroDeBaseDeCustos : RegistroComNome
{
    public DateOnly DataBase { get; set; }

    public static RegistroDeBaseDeCustos De(BaseDeCustos baseDeCustos, int versao, DateTimeOffset agora)
    {
        var registro = new RegistroDeBaseDeCustos { Nome = baseDeCustos.Nome, DataBase = baseDeCustos.DataBase };
        var documento = Json.Escrever(DocumentoDaBaseDeCustos.De(baseDeCustos), ContextoJson.Default.DocumentoDaBaseDeCustos);

        return registro.Preencher<RegistroDeBaseDeCustos>(baseDeCustos.Id.Valor, versao, DocumentoDaBaseDeCustos.Formato, documento, agora);
    }

    public BaseDeCustos ParaBaseDeCustos()
    {
        ConferirFormato(DocumentoDaBaseDeCustos.Formato, "A base de custos");

        return Json.Ler(Documento, ContextoJson.Default.DocumentoDaBaseDeCustos).ParaBaseDeCustos(new BaseDeCustosId(Id));
    }
}

internal sealed class RegistroDeOrcamento : RegistroComNome
{
    public static RegistroDeOrcamento De(Orcamento orcamento, int versao, DateTimeOffset agora)
    {
        var registro = new RegistroDeOrcamento { Nome = orcamento.Nome };
        var documento = Json.Escrever(DocumentoDoOrcamento.De(orcamento), ContextoJson.Default.DocumentoDoOrcamento);

        return registro.Preencher<RegistroDeOrcamento>(orcamento.Id.Valor, versao, DocumentoDoOrcamento.Formato, documento, agora);
    }

    public Orcamento ParaOrcamento()
    {
        ConferirFormato(DocumentoDoOrcamento.Formato, "O orçamento");

        return Json.Ler(Documento, ContextoJson.Default.DocumentoDoOrcamento).ParaOrcamento(new OrcamentoId(Id));
    }
}

internal sealed class RegistroDeLinhaDeBase : RegistroComNome
{
    public DateOnly AprovadaEm { get; set; }

    public static RegistroDeLinhaDeBase De(LinhaDeBase linhaDeBase, int versao, DateTimeOffset agora)
    {
        var registro = new RegistroDeLinhaDeBase { Nome = linhaDeBase.Nome, AprovadaEm = linhaDeBase.AprovadaEm };
        var documento = Json.Escrever(DocumentoDaLinhaDeBase.De(linhaDeBase), ContextoJson.Default.DocumentoDaLinhaDeBase);

        return registro.Preencher<RegistroDeLinhaDeBase>(linhaDeBase.Id.Valor, versao, DocumentoDaLinhaDeBase.Formato, documento, agora);
    }

    public LinhaDeBase ParaLinhaDeBase()
    {
        ConferirFormato(DocumentoDaLinhaDeBase.Formato, "A linha de base");

        return Json.Ler(Documento, ContextoJson.Default.DocumentoDaLinhaDeBase).ParaLinhaDeBase(new LinhaDeBaseId(Id));
    }
}

internal sealed class RegistroDeAcompanhamento : RegistroDeAgregado
{
    /// <summary>A linha de base contra a qual a obra é medida — chave estrangeira.</summary>
    public Guid LinhaDeBaseId { get; set; }

    public static RegistroDeAcompanhamento De(Acompanhamento acompanhamento, int versao, DateTimeOffset agora)
    {
        var registro = new RegistroDeAcompanhamento { LinhaDeBaseId = acompanhamento.LinhaDeBase.Id.Valor };
        var documento = Json.Escrever(DocumentoDoAcompanhamento.De(acompanhamento), ContextoJson.Default.DocumentoDoAcompanhamento);

        return registro.Preencher<RegistroDeAcompanhamento>(acompanhamento.Id.Valor, versao, DocumentoDoAcompanhamento.Formato, documento, agora);
    }

    /// <param name="linhaDeBase">A linha de base gravada em <see cref="LinhaDeBaseId"/>, já lida.</param>
    public Acompanhamento ParaAcompanhamento(LinhaDeBase linhaDeBase)
    {
        ArgumentNullException.ThrowIfNull(linhaDeBase);
        ConferirFormato(DocumentoDoAcompanhamento.Formato, "O acompanhamento");

        if (linhaDeBase.Id.Valor != LinhaDeBaseId)
        {
            throw new InvalidOperationException(
                $"O acompanhamento {Id} é medido contra a linha de base {LinhaDeBaseId}, e não contra {linhaDeBase.Id}.");
        }

        return Json.Ler(Documento, ContextoJson.Default.DocumentoDoAcompanhamento)
            .ParaAcompanhamento(new AcompanhamentoId(Id), linhaDeBase);
    }
}
