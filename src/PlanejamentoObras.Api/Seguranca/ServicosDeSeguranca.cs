using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace PlanejamentoObras.Api.Seguranca;

/// <summary>Autenticação por JWT e as políticas de autorização (ADR-0015).</summary>
internal static class ServicosDeSeguranca
{
    /// <remarks>
    /// <para>
    /// Quem emite o token não é a API: um provedor de identidade, configurado
    /// em <c>Authentication:Schemes:Bearer</c> — em desenvolvimento, o
    /// <c>dotnet user-jwts</c>. A API só confere assinatura, emissor,
    /// audiência e validade.
    /// </para>
    /// <para>
    /// Toda política começa por ser da empresa, e é também a política de
    /// reserva: endpoint que esquecer de declarar uma ainda exige ao menos
    /// isso. Anônimo, só o documento OpenAPI.
    /// </para>
    /// </remarks>
    public static IServiceCollection AdicionarSeguranca(this IServiceCollection servicos)
    {
        servicos
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opcoes =>
            {
                // As reivindicações com os nomes do token — role, empresa —,
                // sem a tradução para os URIs longos do WS-Federation.
                opcoes.MapInboundClaims = false;
                opcoes.TokenValidationParameters.NameClaimType = Reivindicacoes.Nome;
                opcoes.TokenValidationParameters.RoleClaimType = Reivindicacoes.Papel;
            });

        var daEmpresa = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(contexto => EmpresaDoUsuario.Ler(contexto.User) is not null)
            .Build();

        servicos.AddAuthorizationBuilder()
            .SetDefaultPolicy(daEmpresa)
            .SetFallbackPolicy(daEmpresa)
            .AddPolicy(Politicas.Consultar, daEmpresa)
            .AddPolicy(Politicas.Planejar, politica => politica.Combine(daEmpresa).RequireRole(Papeis.Planejador))
            .AddPolicy(Politicas.Orcar, politica => politica.Combine(daEmpresa).RequireRole(Papeis.Orcamentista))
            .AddPolicy(Politicas.AprovarLinhaDeBase, politica => politica.Combine(daEmpresa).RequireRole(Papeis.Gestor))
            .AddPolicy(Politicas.Medir, politica => politica.Combine(daEmpresa).RequireRole(Papeis.Engenheiro));

        return servicos;
    }
}
