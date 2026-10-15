using PlanejamentoObras.Processamento;

var builder = Host.CreateApplicationBuilder(args);

var conexao = builder.Configuration.GetConnectionString("Planejamento")
    ?? throw new InvalidOperationException("Falta a cadeia de conexão ConnectionStrings:Planejamento.");

builder.Services.AdicionarProcessamento(conexao);

await builder.Build().RunAsync();
