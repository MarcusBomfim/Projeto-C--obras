using System.Security.Claims;
using PlanejamentoObras.Infraestrutura.Persistencia;

namespace PlanejamentoObras.Api.Seguranca;

/// <summary>
/// A empresa de quem fez o pedido, lida do token: é em nome dela que a
/// persistência lê e grava (ADR-0013).
/// </summary>
/// <remarks>
/// As políticas já recusam token sem empresa válida, então dentro de um
/// endpoint ela sempre existe. Fora de pedido — ou com um token sem ela —, é
/// nenhuma empresa: nada se lê, nada se grava.
/// </remarks>
public sealed class EmpresaDoUsuario(IHttpContextAccessor acessor) : IEmpresaAtual
{
    public Guid? Id => Ler(acessor.HttpContext?.User);

    /// <summary>A empresa do token, se houver uma válida.</summary>
    public static Guid? Ler(ClaimsPrincipal? usuario) =>
        Guid.TryParse(usuario?.FindFirstValue(Reivindicacoes.Empresa), out var empresa) && empresa != Guid.Empty
            ? empresa
            : null;
}
