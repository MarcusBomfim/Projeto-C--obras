using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Infraestrutura.Mensagens;

namespace PlanejamentoObras.Infraestrutura.Persistencia.CaixaDeSaida;

/// <summary>
/// Uma mensagem da caixa de saída: um evento de domínio publicado, gravado
/// na mesma transação do agregado que o registrou, esperando ser tratado
/// (ADR-0017).
/// </summary>
internal sealed class RegistroDeMensagem
{
    public Guid Id { get; set; }

    /// <summary>A ordem de chegada na caixa — a fila trata primeiro o que chegou primeiro.</summary>
    public long Sequencia { get; set; }

    public Guid EmpresaId { get; set; }

    /// <summary>O nome do fato: <c>medicao-registrada</c>, <c>acompanhamento-iniciado</c>.</summary>
    public string Tipo { get; set; } = string.Empty;

    public short Formato { get; set; }

    /// <summary>A mensagem em JSON — coluna <c>jsonb</c>.</summary>
    public string Conteudo { get; set; } = string.Empty;

    public Guid AgregadoId { get; set; }

    /// <summary>A versão em que o agregado ficou com o fato.</summary>
    public int VersaoDoAgregado { get; set; }

    public DateTimeOffset OcorridaEm { get; set; }

    public int Tentativas { get; set; }

    public DateTimeOffset ProximaTentativaEm { get; set; }

    public DateTimeOffset? TratadaEm { get; set; }

    /// <summary>Quando a fila desistiu da mensagem, depois de falhar tentativas demais. Fica para alguém olhar.</summary>
    public DateTimeOffset? AbandonadaEm { get; set; }

    public string? UltimoErro { get; set; }

    /// <summary>
    /// O rastro de quem publicou a mensagem, no formato <c>traceparent</c> do
    /// W3C: o tratamento continua dele. Nulo se ninguém estava rastreando.
    /// </summary>
    public string? ContextoDoRastreio { get; set; }

    public static RegistroDeMensagem De(EventoDeDominio evento, Guid empresa, int versaoDoAgregado, DateTimeOffset agora)
    {
        var (tipo, conteudo) = CatalogoDeMensagens.Escrever(evento);

        return new RegistroDeMensagem
        {
            Id = Guid.CreateVersion7(agora),
            EmpresaId = empresa,
            Tipo = tipo,
            Formato = CatalogoDeMensagens.Formato,
            Conteudo = conteudo,
            AgregadoId = evento.Agregado.Valor,
            VersaoDoAgregado = versaoDoAgregado,
            OcorridaEm = agora,
            ProximaTentativaEm = agora,
        };
    }
}
