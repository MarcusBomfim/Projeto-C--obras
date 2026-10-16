using PlanejamentoObras.Observabilidade;
using PlanejamentoObras.Processamento;
using PlanejamentoObras.Processamento.Hospedagem;

// Um host web só para as verificações de saúde: o trabalho é da fila, nos
// serviços em segundo plano.
var builder = WebApplication.CreateBuilder(args);

var conexao = builder.Configuration.GetConnectionString("Planejamento")
    ?? throw new InvalidOperationException("Falta a cadeia de conexão ConnectionStrings:Planejamento.");

builder.AdicionarObservabilidade("planejamento-obras-processador");
builder.Services.AdicionarProcessamento(conexao);
builder.Services.AddHealthChecks()
    .VerificarBanco(conexao)
    .AddCheck<FilaGirando>("fila", tags: [Saude.Viva]);

var app = builder.Build();
app.MapearSaude();

await app.RunAsync();
