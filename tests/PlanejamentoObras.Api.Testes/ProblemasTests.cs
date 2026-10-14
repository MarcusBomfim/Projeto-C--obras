using System.Net;
using System.Text;
using System.Text.Json;
using Npgsql;
using PlanejamentoObras.Api.Seguranca;

namespace PlanejamentoObras.Api.Testes;

/*
 * Todo erro sai em ProblemDetails (RFC 9457), com um tipo que aponta para a
 * explicação, um título fixo por tipo, o detalhe do que aconteceu e o id do
 * rastreio. Erro do cliente diz o que estava errado; erro interno não vaza
 * nada além do id (ADR-0015).
 */
public sealed class ProblemasTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly HttpClient _planejador = api.Cliente(Guid.CreateVersion7(), Papeis.Planejador);

    private Task<HttpResponseMessage> CriarCom(object corpo) => _planejador.Criar("/api/cronogramas", corpo);

    [Fact]
    public async Task Regra_do_negocio_e_422_com_a_mensagem_do_dominio()
    {
        var comCiclo = new
        {
            nome = "Casa",
            atividades = new[]
            {
                new { codigo = "FUN", descricao = "Fundações", duracaoEmDiasUteis = 10 },
                new { codigo = "EST", descricao = "Estrutura", duracaoEmDiasUteis = 20 },
            },
            dependencias = new[] { new { predecessora = "FUN", sucessora = "EST" }, new { predecessora = "EST", sucessora = "FUN" } },
        };

        var resposta = await CriarCom(comCiclo);
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith("#regra-do-negocio", problema.Type, StringComparison.Ordinal);
        Assert.Equal("O pedido fere uma regra do negócio.", problema.Title);
        Assert.Contains("ciclo", problema.Detail, StringComparison.Ordinal);
        Assert.Equal("POST /api/cronogramas", problema.Instance);
        Assert.True(problema.Extensions.ContainsKey("traceId"));
    }

    [Theory]
    [InlineData("""{}""", "nome")]
    [InlineData("""{"nome": null}""", "nome")]
    [InlineData("""{"nome": "Casa", "cor": "azul"}""", "cor")]
    [InlineData("""{"nome": "Casa", "dependencias": [{"predecessora": "A", "sucessora": "B", "tipo": 0}]}""", "tipo")]
    [InlineData("""{"nome": "Casa",""", "")]
    public async Task Corpo_fora_do_contrato_e_400_dizendo_o_que_estava_errado(string json, string campo)
    {
        var resposta = await CriarCom(json);
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.EndsWith("#pedido-invalido", problema.Type, StringComparison.Ordinal);
        Assert.StartsWith("O corpo não é o JSON esperado", problema.Detail, StringComparison.Ordinal);
        Assert.Contains(campo, problema.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Corpo_que_nao_e_JSON_e_415()
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/cronogramas")
        {
            Content = new StringContent("nome=Casa", Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        pedido.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var resposta = await _planejador.SendAsync(pedido, Cancelamento);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, resposta.StatusCode);
        Assert.EndsWith("#tipo-nao-suportado", (await resposta.Problema()).Type, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rota_ou_metodo_que_nao_existe_tambem_vem_em_problema()
    {
        var semRota = await _planejador.GetAsync("/api/nada", Cancelamento);
        var semMetodo = await _planejador.PutAsync("/api/cronogramas", new StringContent("{}", Encoding.UTF8, "application/json"), Cancelamento);

        Assert.Equal((HttpStatusCode.NotFound, "Não encontrado."), (semRota.StatusCode, (await semRota.Problema()).Title));
        Assert.Equal(
            (HttpStatusCode.MethodNotAllowed, "Este recurso não aceita este método."),
            (semMetodo.StatusCode, (await semMetodo.Problema()).Title));
    }

    [Fact]
    public async Task Erro_interno_nao_vaza_o_que_houve_so_o_id_do_rastreio()
    {
        // Alguém editou o banco à mão e fechou um ciclo no documento.
        var criado = await CriarCom(RedeDoReadme.Cronograma());
        var id = Guid.Parse(criado.Headers.Location!.ToString().Split('/')[^1]);

        await using (var conexao = new NpgsqlConnection(api.Banco.Conexao))
        {
            await conexao.OpenAsync(Cancelamento);
            await using var comando = new NpgsqlCommand(
                """
                update planejamento.cronogramas
                set documento = jsonb_set(documento, '{dependencias}',
                    documento->'dependencias' || '[{"predecessora":"ENT","sucessora":"MOB","tipo":"TerminoInicio","defasagemEmDiasUteis":0}]')
                where id = @id
                """,
                conexao);
            comando.Parameters.AddWithValue("id", id);
            await comando.ExecuteNonQueryAsync(Cancelamento);
        }

        var resposta = await _planejador.GetAsync($"/api/cronogramas/{id}", Cancelamento);
        var corpo = await resposta.Content.ReadAsStringAsync(Cancelamento);
        var problema = JsonSerializer.Deserialize<JsonElement>(corpo);

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        Assert.Equal("Erro interno.", problema.GetProperty("title").GetString());
        Assert.False(problema.TryGetProperty("detail", out _));
        Assert.True(problema.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("ciclo", corpo, StringComparison.OrdinalIgnoreCase);
    }
}
