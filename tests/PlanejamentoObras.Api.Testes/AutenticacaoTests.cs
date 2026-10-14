using System.Net;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using PlanejamentoObras.Api.Seguranca;

namespace PlanejamentoObras.Api.Testes;

/*
 * Quem é quem. A API não emite token: confere a assinatura, o emissor, a
 * audiência e a validade do token que o provedor de identidade emitiu — e,
 * no token, a empresa de quem pede (ADR-0015).
 */
public sealed class AutenticacaoTests(ApiDeTestes api)
{
    private const string Cronogramas = "/api/cronogramas";

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _empresa = Guid.CreateVersion7();

    private async Task<HttpResponseMessage> ListarCom(string token) =>
        await api.ClienteComToken(token).GetAsync(Cronogramas, Cancelamento);

    [Fact]
    public async Task Sem_token_e_401_com_o_problema_em_portugues()
    {
        var resposta = await api.Anonimo().GetAsync(Cronogramas, Cancelamento);
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("Bearer", resposta.Headers.WwwAuthenticate.Single().Scheme);
        Assert.Equal("É preciso se autenticar.", problema.Title);
        Assert.EndsWith("#nao-autenticado", problema.Type, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/api/qualquer-coisa")]
    [InlineData("/api/cronogramas/00000000-0000-0000-0000-000000000000/nada")]
    public async Task Sem_token_nem_rota_que_nao_existe_se_descobre(string caminho)
    {
        // A política de reserva vale também para o que não tem endpoint: sem
        // token, 401 — e não um 404 ou 405 que contaria o que existe.
        var resposta = await api.Anonimo().GetAsync(caminho, Cancelamento);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Token_valido_da_empresa_entra()
    {
        var resposta = await ListarCom(Tokens.De(_empresa));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Token_assinado_com_outra_chave_e_recusado()
    {
        var outraChave = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));

        var resposta = await ListarCom(Tokens.Emitir(_empresa.ToString(), [], outraChave, Tokens.Emissor, Tokens.Audiencia, DateTime.UtcNow.AddHours(1)));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Token_vencido_e_recusado()
    {
        var resposta = await ListarCom(Tokens.Emitir(_empresa.ToString(), [], Tokens.Chave, Tokens.Emissor, Tokens.Audiencia, DateTime.UtcNow.AddHours(-1)));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Token_de_outro_emissor_ou_para_outra_audiencia_e_recusado()
    {
        var deOutroEmissor = await ListarCom(Tokens.Emitir(_empresa.ToString(), [], Tokens.Chave, "https://outro.emissor", Tokens.Audiencia, DateTime.UtcNow.AddHours(1)));
        var paraOutraAudiencia = await ListarCom(Tokens.Emitir(_empresa.ToString(), [], Tokens.Chave, Tokens.Emissor, "outra-api", DateTime.UtcNow.AddHours(1)));

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (deOutroEmissor.StatusCode, paraOutraAudiencia.StatusCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("não é um guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Token_sem_empresa_valida_e_403_mesmo_com_papel(string? empresa)
    {
        var token = Tokens.Emitir(empresa, [Papeis.Planejador], Tokens.Chave, Tokens.Emissor, Tokens.Audiencia, DateTime.UtcNow.AddHours(1));

        var resposta = await ListarCom(token);
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("Sem permissão para este pedido.", problema.Title);
    }

    [Fact]
    public async Task O_documento_OpenAPI_e_publico()
    {
        var resposta = await api.Anonimo().GetAsync("/openapi/v1.json", Cancelamento);
        var documento = await resposta.Content.ReadAsStringAsync(Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("\"/api/cronogramas\"", documento, StringComparison.Ordinal);
    }
}
