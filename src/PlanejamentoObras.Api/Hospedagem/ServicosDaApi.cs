using System.Text.Json.Serialization;
using PlanejamentoObras.Api.Http;
using PlanejamentoObras.Api.Seguranca;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Observabilidade;

namespace PlanejamentoObras.Api.Hospedagem;

/// <summary>Os serviços da API: observabilidade, persistência, segurança, erros, JSON e documentação.</summary>
internal static class ServicosDaApi
{
    public static WebApplicationBuilder AdicionarApi(this WebApplicationBuilder builder)
    {
        var conexao = builder.Configuration.GetConnectionString("Planejamento")
            ?? throw new InvalidOperationException("Falta a cadeia de conexão ConnectionStrings:Planejamento.");

        builder.AdicionarObservabilidade("planejamento-obras-api");
        builder.Services.AddHealthChecks().VerificarBanco(conexao);

        builder.Services.AddHttpContextAccessor();
        builder.Services.AdicionarPersistencia<EmpresaDoUsuario>(conexao);
        builder.Services.AdicionarSeguranca();

        builder.Services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = Problemas.Completar);
        builder.Services.AddExceptionHandler<TradutorDeExcecoes>();

        // JSON estrito, como a leitura dos documentos (ADR-0011): campo
        // obrigatório que falta, nulo onde não cabe, campo que o contrato não
        // tem, enum em número e número em texto são recusados — e não viram
        // zero em silêncio. Com isso o contrato publicado diz de cada número
        // que ele é um número, e os tipos gerados dele também. O
        // discriminador de tipo, nos objetos que têm um, pode vir em qualquer
        // posição: quem monta o JSON nem sempre controla a ordem das chaves.
        builder.Services.ConfigureHttpJsonOptions(opcoes =>
        {
            var json = opcoes.SerializerOptions;
            json.RespectNullableAnnotations = true;
            json.RespectRequiredConstructorParameters = true;
            json.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            json.NumberHandling = JsonNumberHandling.Strict;
            json.AllowOutOfOrderMetadataProperties = true;
            json.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        });

        // Pedido malformado lança, e o tradutor de exceções explica o que
        // estava errado. Sem isso, seria um 400 mudo.
        builder.Services.Configure<RouteHandlerOptions>(opcoes => opcoes.ThrowOnBadRequest = true);

        builder.Services.AddOpenApi(opcoes =>
        {
            opcoes.AddDocumentTransformer<DocumentacaoDaApi>();
            opcoes.AddOperationTransformer<DocumentacaoDaApi>();
        });

        return builder;
    }
}
