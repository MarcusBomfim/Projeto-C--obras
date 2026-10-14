using System.Net;
using PlanejamentoObras.Api.Seguranca;

namespace PlanejamentoObras.Api.Testes;

/*
 * Cada recurso, o seu papel: a base e o orçamento são do orçamentista; a
 * linha de base, do gestor; o acompanhamento, do engenheiro. Os outros
 * papéis da mesma empresa recebem 403 — antes de qualquer outra conferência
 * do pedido (ADR-0015).
 */
public sealed class PapeisPorRecursoTests(ApiDeTestes api)
{
    private static readonly string[] Todos = [Papeis.Planejador, Papeis.Orcamentista, Papeis.Gestor, Papeis.Engenheiro];

    public static TheoryData<string, string, string> Criacoes => new()
    {
        { "/api/bases-de-custos", """{"nome": "Tabela", "dataBase": "2026-09-01"}""", Papeis.Orcamentista },
        { "/api/orcamentos", """{"nome": "Casa", "bdi": {"percentual": 25}}""", Papeis.Orcamentista },
        { "/api/cronogramas", """{"nome": "Casa"}""", Papeis.Planejador },
        { "/api/linhas-de-base", """{"nome": "LB", "aprovadaEm": "2026-09-28", "orcamento": "01992b6e-5d1c-7a3e-9f00-0000000000a1", "baseDeCustos": "01992b6e-5d1c-7a3e-9f00-0000000000a2", "cronograma": "01992b6e-5d1c-7a3e-9f00-0000000000a3", "inicio": "2026-10-05"}""", Papeis.Gestor },
        { "/api/acompanhamentos", """{"linhaDeBase": "01992b6e-5d1c-7a3e-9f00-0000000000a4"}""", Papeis.Engenheiro },
    };

    [Theory]
    [MemberData(nameof(Criacoes))]
    public async Task So_o_papel_do_recurso_cria(string caminho, string corpo, string papel)
    {
        var empresa = Guid.CreateVersion7();

        foreach (var outro in Todos.Where(outro => outro != papel))
        {
            var recusado = await api.Cliente(empresa, outro).Criar(caminho, corpo);

            Assert.True(recusado.StatusCode == HttpStatusCode.Forbidden, $"{outro} criou em {caminho}: {(int)recusado.StatusCode}.");
        }

        var doPapel = await api.Cliente(empresa, papel).Criar(caminho, corpo);

        Assert.NotEqual(HttpStatusCode.Forbidden, doPapel.StatusCode);
    }
}
