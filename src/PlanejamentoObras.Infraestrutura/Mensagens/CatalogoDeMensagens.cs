using System.Text.Json;
using System.Text.Json.Serialization;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Medicoes;

namespace PlanejamentoObras.Infraestrutura.Mensagens;

/*
 * O formato publicado de cada evento de domínio: o contrato com quem processa
 * a caixa de saída (ADR-0017). Separado do evento como os documentos são
 * separados dos agregados (ADR-0011) — o domínio pode mudar sem mudar o que
 * já está na fila, esperando para ser tratado.
 */

public sealed record MensagemDeAcompanhamentoIniciado(Guid Acompanhamento, Guid LinhaDeBase);

public sealed record MensagemDeMedicaoRegistrada(Guid Acompanhamento, int Numero, DateOnly Mes);

public sealed record MensagemDeMedicaoDesfeita(Guid Acompanhamento, int Numero, DateOnly Mes);

/// <summary>A tradução entre os eventos de domínio e as mensagens da caixa de saída, nos dois sentidos.</summary>
public static class CatalogoDeMensagens
{
    /// <summary>O formato em que as mensagens são escritas hoje.</summary>
    public const short Formato = 1;

    /// <summary>O tipo da mensagem e o conteúdo dela, em JSON.</summary>
    /// <exception cref="InvalidOperationException">O evento ainda não tem mensagem no catálogo.</exception>
    public static (string Tipo, string Conteudo) Escrever(EventoDeDominio evento) => evento switch
    {
        AcompanhamentoIniciado iniciado => (
            "acompanhamento-iniciado",
            JsonSerializer.Serialize(
                new MensagemDeAcompanhamentoIniciado(iniciado.Acompanhamento.Valor, iniciado.LinhaDeBase.Valor),
                ContextoJsonDasMensagens.Default.MensagemDeAcompanhamentoIniciado)),
        MedicaoRegistrada registrada => (
            "medicao-registrada",
            JsonSerializer.Serialize(
                new MensagemDeMedicaoRegistrada(registrada.Acompanhamento.Valor, registrada.Numero, registrada.Mes),
                ContextoJsonDasMensagens.Default.MensagemDeMedicaoRegistrada)),
        MedicaoDesfeita desfeita => (
            "medicao-desfeita",
            JsonSerializer.Serialize(
                new MensagemDeMedicaoDesfeita(desfeita.Acompanhamento.Valor, desfeita.Numero, desfeita.Mes),
                ContextoJsonDasMensagens.Default.MensagemDeMedicaoDesfeita)),
        null => throw new ArgumentNullException(nameof(evento)),
        _ => throw new InvalidOperationException($"O evento {evento.GetType().Name} não tem mensagem no catálogo: acrescente a dele."),
    };

    /// <summary>A mensagem gravada, de volta ao tipo dela.</summary>
    /// <exception cref="InvalidOperationException">Tipo ou formato que esta versão do sistema não conhece.</exception>
    public static object Ler(string tipo, short formato, string conteudo)
    {
        if (formato != Formato)
        {
            throw new InvalidOperationException($"A mensagem {tipo} está no formato {formato}, e esta versão do sistema lê o formato {Formato}.");
        }

        object? mensagem = tipo switch
        {
            "acompanhamento-iniciado" => JsonSerializer.Deserialize(conteudo, ContextoJsonDasMensagens.Default.MensagemDeAcompanhamentoIniciado),
            "medicao-registrada" => JsonSerializer.Deserialize(conteudo, ContextoJsonDasMensagens.Default.MensagemDeMedicaoRegistrada),
            "medicao-desfeita" => JsonSerializer.Deserialize(conteudo, ContextoJsonDasMensagens.Default.MensagemDeMedicaoDesfeita),
            _ => throw new InvalidOperationException($"Tipo de mensagem desconhecido: {tipo}."),
        };

        return mensagem ?? throw new InvalidOperationException($"A mensagem {tipo} está vazia.");
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(MensagemDeAcompanhamentoIniciado))]
[JsonSerializable(typeof(MensagemDeMedicaoRegistrada))]
[JsonSerializable(typeof(MensagemDeMedicaoDesfeita))]
internal sealed partial class ContextoJsonDasMensagens : JsonSerializerContext;
