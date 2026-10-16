using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PlanejamentoObras.Api.Seguranca;
using PlanejamentoObras.Processamento;

namespace PlanejamentoObras.Api.Testes;

/*
 * A API vista de fora (ADR-0019): as verificações de saúde que o orquestrador
 * consulta, e o rastro de cada pedido — que continua o de quem pediu, e que
 * a caixa de saída leva até o tratamento em segundo plano.
 */
public sealed partial class ObservabilidadeTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _empresa = Guid.CreateVersion7();

    [Fact]
    public async Task Viva_e_pronta_respondem_sem_token_com_a_situacao_de_cada_verificacao()
    {
        using var anonimo = api.Anonimo();

        var viva = await anonimo.GetAsync("/saude/viva", Cancelamento);
        var pronta = await anonimo.GetAsync("/saude/pronta", Cancelamento);

        Assert.Equal(
            (HttpStatusCode.OK, """{"situacao":"Healthy","verificacoes":{}}"""),
            (viva.StatusCode, await viva.Content.ReadAsStringAsync(Cancelamento)));
        Assert.Equal(
            (HttpStatusCode.OK, """{"situacao":"Healthy","verificacoes":{"banco":{"situacao":"Healthy"}}}"""),
            (pronta.StatusCode, await pronta.Content.ReadAsStringAsync(Cancelamento)));
    }

    [Fact]
    public async Task Sem_o_banco_a_api_continua_viva_mas_nao_esta_pronta()
    {
        await using var semBanco = api.Variante(web => web.UseSetting(
            "ConnectionStrings:Planejamento",
            "Host=127.0.0.1;Port=1;Database=nenhum;Username=ninguem;Password=nada;Timeout=2"));
        using var anonimo = semBanco.CreateClient();

        var viva = await anonimo.GetAsync("/saude/viva", Cancelamento);
        var pronta = await anonimo.GetAsync("/saude/pronta", Cancelamento);

        // Reiniciar não põe o banco no ar: o orquestrador só para de mandar
        // pedidos até ele voltar. E a resposta não diz onde o banco está.
        Assert.Equal(HttpStatusCode.OK, viva.StatusCode);
        Assert.Equal(
            (HttpStatusCode.ServiceUnavailable, """{"situacao":"Unhealthy","verificacoes":{"banco":{"situacao":"Unhealthy","descricao":"O banco não respondeu."}}}"""),
            (pronta.StatusCode, await pronta.Content.ReadAsStringAsync(Cancelamento)));
    }

    [Fact]
    public async Task Cada_pedido_vira_um_rastro_com_a_rota_e_o_servico_que_atendeu()
    {
        var exportados = new RastrosExportados();
        await using var rastreada = Rastreada(exportados);
        var (rastro, cliente) = ClienteNoRastro(rastreada, Papeis.Engenheiro);

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/painel", Cancelamento)).StatusCode);

        var doRastro = await exportados.Esperar(rastro, "GET /api/painel/");
        var pedido = Assert.Single(doRastro, atividade => atividade.Kind == ActivityKind.Server);
        Assert.Equal(("/api/painel/", 200), (pedido.GetTagItem("http.route"), pedido.GetTagItem("http.response.status_code")));

        // E o que o pedido fez no banco está dentro dele.
        Assert.Contains(doRastro, atividade => atividade.Source.Name == "Npgsql" && atividade.ParentSpanId == pedido.SpanId);

        var servico = rastreada.Services.GetRequiredService<TracerProvider>().GetResource().Attributes.ToDictionary();
        Assert.Equal(
            ("planejamento-obras-api", "planejamento-obras", "Testes"),
            (servico["service.name"], servico["service.namespace"], servico["deployment.environment.name"]));
    }

    [Fact]
    public async Task A_verificacao_de_saude_nao_vira_rastro()
    {
        var exportados = new RastrosExportados();
        await using var rastreada = Rastreada(exportados);
        var (rastro, cliente) = ClienteNoRastro(rastreada, Papeis.Engenheiro);

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/saude/viva", Cancelamento)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/saude/pronta", Cancelamento)).StatusCode);

        // Um pedido de verdade depois, no mesmo rastro: quando ele chega, o
        // que as verificações tivessem deixado já teria chegado antes.
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/painel", Cancelamento)).StatusCode);
        var doRastro = await exportados.Esperar(rastro, "GET /api/painel/");

        // Nem o pedido, nem o select 1 no banco: o orquestrador pergunta a
        // cada poucos segundos, e encheria o painel.
        var pedido = Assert.Single(doRastro, atividade => atividade.Kind == ActivityKind.Server);
        Assert.Equal("GET /api/painel/", pedido.DisplayName);
        Assert.All(doRastro, atividade => Assert.Equal(pedido.TraceId, atividade.TraceId));
        Assert.All(doRastro.Where(atividade => atividade != pedido), atividade => Assert.Equal(pedido.SpanId, atividade.ParentSpanId));
    }

    [Fact]
    public async Task O_rastro_de_quem_pede_continua_na_api_e_vai_no_traceId_do_erro()
    {
        var rastro = ActivityTraceId.CreateRandom();
        var cliente = api.Cliente(_empresa, Papeis.Engenheiro);
        using var pedido = new HttpRequestMessage(HttpMethod.Get, $"/api/painel/{Guid.CreateVersion7()}");
        pedido.Headers.Add("traceparent", $"00-{rastro}-{ActivitySpanId.CreateRandom()}-01");

        var resposta = await cliente.SendAsync(pedido, Cancelamento);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.StartsWith($"00-{rastro}-", (await resposta.Problema()).Extensions["traceId"]?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_rastro_de_um_fato_vai_do_pedido_http_ao_tratamento_em_segundo_plano()
    {
        var linhaDeBase = await UmaLinhaDeBase();
        var exportados = new RastrosExportados();
        await using var rastreada = Rastreada(exportados);
        var (rastro, engenheiro) = ClienteNoRastro(rastreada, Papeis.Engenheiro);

        var acompanhamento = await engenheiro.Criar("/api/acompanhamentos", new { linhaDeBase }).IdCriado();
        await api.ProcessarAte(
            async () => await api.Cliente(_empresa, Papeis.Engenheiro).LerDoPainel(acompanhamento) is not null,
            "o acompanhamento novo chegar ao painel");

        var doFato = await exportados.Esperar(
            rastro,
            "POST /api/acompanhamentos",
            "publicar acompanhamento-iniciado",
            "tratar acompanhamento-iniciado");
        var pedido = Assert.Single(doFato, atividade => atividade.Kind == ActivityKind.Server);
        var publicacao = Assert.Single(doFato, atividade => atividade.DisplayName == "publicar acompanhamento-iniciado");
        var tratamento = Assert.Single(doFato, atividade => atividade.DisplayName == "tratar acompanhamento-iniciado");

        // Um rastro só: o pedido publica o fato na caixa de saída, e o
        // processador, noutro momento, trata em continuação a ele.
        Assert.Equal(pedido.SpanId, publicacao.ParentSpanId);
        Assert.Equal(publicacao.SpanId, tratamento.ParentSpanId);
        Assert.Equal(_empresa.ToString(), tratamento.GetTagItem("planejamento.empresa.id"));
    }

    [Fact]
    public async Task As_metricas_dos_pedidos_e_da_fila_entram_na_telemetria()
    {
        var linhaDeBase = await UmaLinhaDeBase();
        var metricas = new List<Metric>();
        await using var medida = api.Variante(web => web.ConfigureTestServices(servicos =>
            servicos.ConfigureOpenTelemetryMeterProvider(medidores => medidores.AddInMemoryExporter(metricas))));
        var (_, engenheiro) = ClienteNoRastro(medida, Papeis.Engenheiro);

        var acompanhamento = await engenheiro.Criar("/api/acompanhamentos", new { linhaDeBase }).IdCriado();
        await api.ProcessarAte(async () => await engenheiro.LerDoPainel(acompanhamento) is not null, "o acompanhamento novo chegar ao painel");
        medida.Services.GetRequiredService<MeterProvider>().ForceFlush();

        var nomes = metricas.Select(metrica => metrica.Name).ToHashSet();
        Assert.Contains("http.server.request.duration", nomes);
        Assert.Contains(MetricasDaCaixaDeSaida.Mensagens, nomes);
        Assert.Contains(MetricasDaCaixaDeSaida.Espera, nomes);
    }

    [Fact]
    public void Os_logs_vao_no_rastro_com_a_mensagem_pronta_e_o_escopo()
    {
        var registros = new List<LogRecord>();
        using var registrando = api.Variante(web => web.ConfigureTestServices(servicos =>
            servicos.ConfigureOpenTelemetryLoggerProvider(logs => logs.AddInMemoryExporter(registros))));
        var registro = registrando.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PlanejamentoObras.Testes");

        using var pedido = new Activity("pedido de teste").Start();
        using (registro.BeginScope(new Dictionary<string, object?> { ["Obra"] = "Residencial Vista Serra" }))
        {
            MedicaoAtrasada(registro, 3);
        }

        registrando.Services.GetRequiredService<LoggerProvider>().ForceFlush();

        var linha = Assert.Single(registros, registrado => registrado.Body == "A medição {Medicao} chegou atrasada.");
        Assert.Equal(("A medição 3 chegou atrasada.", pedido.TraceId), (linha.FormattedMessage, linha.TraceId));

        var escopos = new List<KeyValuePair<string, object?>>();
        linha.ForEachScope(
            (escopo, lista) =>
            {
                foreach (var item in escopo)
                {
                    lista.Add(item);
                }
            },
            escopos);
        Assert.Contains(new KeyValuePair<string, object?>("Obra", "Residencial Vista Serra"), escopos);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A medição {Medicao} chegou atrasada.")]
    private static partial void MedicaoAtrasada(ILogger registro, int medicao);

    [Fact]
    public async Task Com_o_endereco_do_coletor_rastros_metricas_e_logs_saem_por_otlp()
    {
        await using var coletor = await ColetorDeTeste.Subir();
        await using var exportando = api.Variante(web => web
            .UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", coletor.Endereco)
            .UseSetting("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"));
        var (_, cliente) = ClienteNoRastro(exportando, Papeis.Engenheiro);

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/painel", Cancelamento)).StatusCode);

        // O SDK manda em lotes, de tempos em tempos; aqui, na hora.
        exportando.Services.GetRequiredService<TracerProvider>().ForceFlush();
        exportando.Services.GetRequiredService<MeterProvider>().ForceFlush();
        exportando.Services.GetRequiredService<LoggerProvider>().ForceFlush();

        Assert.Equal(["/v1/logs", "/v1/metrics", "/v1/traces"], coletor.Recebidos.Distinct().Order(StringComparer.Ordinal));
    }

    /// <summary>A API exportando os rastros para a memória, como exportaria para o coletor.</summary>
    private WebApplicationFactory<Program> Rastreada(RastrosExportados exportados) =>
        api.Variante(web => web.ConfigureTestServices(servicos =>
            servicos.ConfigureOpenTelemetryTracerProvider(rastros => rastros.AddInMemoryExporter(exportados))));

    /// <summary>
    /// Um cliente cujos pedidos vêm todos de um rastro só, como de um
    /// front-end instrumentado. Os testes rodam em paralelo, e o SDK escuta o
    /// processo inteiro: é o rastro que separa o que é deste teste.
    /// </summary>
    private (ActivityTraceId Rastro, HttpClient Cliente) ClienteNoRastro(WebApplicationFactory<Program> variante, params string[] papeis)
    {
        var rastro = ActivityTraceId.CreateRandom();
        var cliente = variante.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Tokens.De(_empresa, papeis));
        cliente.DefaultRequestHeaders.Add("traceparent", $"00-{rastro}-{ActivitySpanId.CreateRandom()}-01");

        return (rastro, cliente);
    }

    private async Task<Guid> UmaLinhaDeBase()
    {
        var orcamentista = api.Cliente(_empresa, Papeis.Orcamentista);
        var orcamento = await orcamentista.Criar("/api/orcamentos", ObraDoReadme.Orcamento()).IdCriado();
        var baseDeCustos = await orcamentista.Criar("/api/bases-de-custos", ObraDoReadme.BaseDeCustos()).IdCriado();
        var cronograma = await api.Cliente(_empresa, Papeis.Planejador).Criar("/api/cronogramas", RedeDoReadme.Cronograma()).IdCriado();

        return await api.Cliente(_empresa, Papeis.Gestor)
            .Criar("/api/linhas-de-base", ObraDoReadme.LinhaDeBase(orcamento, baseDeCustos, cronograma))
            .IdCriado();
    }
}
