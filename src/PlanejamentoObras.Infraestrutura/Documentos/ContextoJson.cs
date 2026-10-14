using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PlanejamentoObras.Infraestrutura.Documentos;

/// <summary>
/// A serialização dos documentos, gerada em tempo de compilação.
/// </summary>
/// <remarks>
/// <para>
/// Geração de código em vez de reflexão: o compilador vê todos os tipos que
/// vão para o banco, a serialização não depende de reflexão em tempo de
/// execução, e um tipo que o serializador não sabe tratar é erro de build.
/// </para>
/// <para>
/// A leitura é estrita de propósito (ADR-0011): propriedade obrigatória que
/// falta, nulo onde não cabe nulo ou propriedade que o documento não conhece
/// fazem a leitura falhar. Um documento que não é exatamente o formato
/// esperado não vira, em silêncio, um agregado com um campo zerado.
/// </para>
/// <para>
/// O discriminador de tipo (<c>"tipo"</c>) pode vir em qualquer posição: o
/// <c>jsonb</c> não guarda a ordem das chaves — ele as ordena —, e o
/// documento que volta do banco não é o texto que foi.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    RespectNullableAnnotations = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowOutOfOrderMetadataProperties = true)]
[JsonSerializable(typeof(DocumentoDoCronograma))]
[JsonSerializable(typeof(DocumentoDaBaseDeCustos))]
[JsonSerializable(typeof(DocumentoDoOrcamento))]
[JsonSerializable(typeof(DocumentoDaLinhaDeBase))]
[JsonSerializable(typeof(DocumentoDoAcompanhamento))]
internal sealed partial class ContextoJson : JsonSerializerContext;

/// <summary>Escreve e lê documentos pelo <see cref="ContextoJson"/>.</summary>
internal static class Json
{
    public static string Escrever<T>(T documento, JsonTypeInfo<T> tipo) => JsonSerializer.Serialize(documento, tipo);

    public static T Ler<T>(string json, JsonTypeInfo<T> tipo) =>
        JsonSerializer.Deserialize(json, tipo) ?? throw new JsonException("O documento gravado é nulo.");
}
