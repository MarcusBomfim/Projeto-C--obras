using PlanejamentoObras.Dominio.Calendarios;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Api.Recursos;

/// <summary>
/// Ler um agregado pelo id que veio no pedido — ou 404, com a mensagem de
/// sempre. Um recurso lê também agregados de outros: o orçamento lê a base
/// para calcular a planilha; a linha de base lê os três.
/// </summary>
internal static class Leituras
{
    public static async Task<Versionado<Cronograma>> Cronograma(RepositorioDeCronogramas repositorio, Guid id, CancellationToken cancelamento) =>
        await repositorio.Obter(new CronogramaId(id), cancelamento)
        ?? throw new AgregadoNaoEncontrado($"Não existe o cronograma {id}.");

    public static async Task<Versionado<BaseDeCustos>> BaseDeCustos(RepositorioDeBasesDeCustos repositorio, Guid id, CancellationToken cancelamento) =>
        await repositorio.Obter(new BaseDeCustosId(id), cancelamento)
        ?? throw new AgregadoNaoEncontrado($"Não existe a base de custos {id}.");

    public static async Task<Versionado<Orcamento>> Orcamento(RepositorioDeOrcamentos repositorio, Guid id, CancellationToken cancelamento) =>
        await repositorio.Obter(new OrcamentoId(id), cancelamento)
        ?? throw new AgregadoNaoEncontrado($"Não existe o orçamento {id}.");

    public static async Task<Versionado<LinhaDeBase>> LinhaDeBase(RepositorioDeLinhasDeBase repositorio, Guid id, CancellationToken cancelamento) =>
        await repositorio.Obter(new LinhaDeBaseId(id), cancelamento)
        ?? throw new AgregadoNaoEncontrado($"Não existe a linha de base {id}.");

    public static async Task<Versionado<Acompanhamento>> Acompanhamento(RepositorioDeAcompanhamentos repositorio, Guid id, CancellationToken cancelamento) =>
        await repositorio.Obter(new AcompanhamentoId(id), cancelamento)
        ?? throw new AgregadoNaoEncontrado($"Não existe o acompanhamento {id}.");

    /// <summary>O calendário de trabalho do pedido: de segunda a sexta, ou a sábado, menos os feriados.</summary>
    public static CalendarioDeTrabalho Calendario(IEnumerable<DateOnly>? feriados, bool sabadoUtil) =>
        sabadoUtil ? CalendarioDeTrabalho.DeSegundaASabado(feriados) : CalendarioDeTrabalho.DeSegundaASexta(feriados);
}
