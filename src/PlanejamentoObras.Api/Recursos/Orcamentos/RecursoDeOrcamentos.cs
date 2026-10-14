using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlanejamentoObras.Api.Http;
using PlanejamentoObras.Api.Seguranca;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Infraestrutura.Persistencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Api.Recursos.Orcamentos;

/// <summary>
/// Os orçamentos: a EAP com etapas e itens, o BDI, e o que se calcula com
/// uma base de custos — a planilha, a curva ABC e, com um cronograma, o
/// físico-financeiro.
/// </summary>
/// <remarks>
/// A base de custos não é parte do orçamento: vem em cada cálculo, como
/// parâmetro (ADR-0005). O mesmo orçamento se calcula contra a tabela deste
/// mês e a do mês que vem.
/// </remarks>
public sealed class RecursoDeOrcamentos : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        var orcamentos = api.MapGroup("/orcamentos").WithTags("Orçamentos");

        orcamentos.MapGet("/", Listar).RequireAuthorization(Politicas.Consultar);
        orcamentos.MapPost("/", Criar).RequireAuthorization(Politicas.Orcar).ComIdempotencia();
        orcamentos.MapGet("/{id:guid}", Obter).RequireAuthorization(Politicas.Consultar);
        orcamentos.MapPatch("/{id:guid}", AlterarOrcamento).RequireAuthorization(Politicas.Orcar);
        orcamentos.MapDelete("/{id:guid}", Remover).RequireAuthorization(Politicas.Orcar);

        orcamentos.MapPost("/{id:guid}/etapas", AdicionarEtapa).RequireAuthorization(Politicas.Orcar);
        orcamentos.MapPatch("/{id:guid}/etapas/{numero}", RenomearEtapa).RequireAuthorization(Politicas.Orcar);
        orcamentos.MapDelete("/{id:guid}/etapas/{numero}", RemoverEtapa).RequireAuthorization(Politicas.Orcar);

        orcamentos.MapPost("/{id:guid}/itens", AdicionarItem).RequireAuthorization(Politicas.Orcar);
        orcamentos.MapPatch("/{id:guid}/itens/{numero}", AlterarQuantidade).RequireAuthorization(Politicas.Orcar);
        orcamentos.MapDelete("/{id:guid}/itens/{numero}", RemoverItem).RequireAuthorization(Politicas.Orcar);
        orcamentos.MapPut("/{id:guid}/itens/{numero}/atividade", VincularAtividade).RequireAuthorization(Politicas.Orcar);
        orcamentos.MapDelete("/{id:guid}/itens/{numero}/atividade", DesvincularAtividade).RequireAuthorization(Politicas.Orcar);
        orcamentos.MapPut("/{id:guid}/itens/{numero}/bdi", DefinirBdiDiferenciado).RequireAuthorization(Politicas.Orcar);
        orcamentos.MapDelete("/{id:guid}/itens/{numero}/bdi", RemoverBdiDiferenciado).RequireAuthorization(Politicas.Orcar);

        orcamentos.MapGet("/{id:guid}/planilha", Planilha).RequireAuthorization(Politicas.Consultar);
        orcamentos.MapGet("/{id:guid}/curva-abc", CurvaAbc).RequireAuthorization(Politicas.Consultar);
        orcamentos.MapGet("/{id:guid}/fisico-financeiro", FisicoFinanceiro).RequireAuthorization(Politicas.Consultar);
    }

    private static async Task<Ok<IReadOnlyList<ResumoResposta>>> Listar(RepositorioDeOrcamentos repositorio, CancellationToken cancelamento)
    {
        var resumos = await repositorio.Listar(cancelamento);

        return TypedResults.Ok<IReadOnlyList<ResumoResposta>>([.. resumos.Select(ResumoResposta.De)]);
    }

    private static async Task<Created<OrcamentoResposta>> Criar(
        NovoOrcamento novo,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var orcamento = new Orcamento(novo.Nome, novo.Bdi.ParaBdi());

        foreach (var etapa in novo.Etapas ?? [])
        {
            var numero = orcamento.AdicionarEtapa(etapa.Titulo);
            Montar(orcamento, numero.Valor, etapa.Filhos);
        }

        Etiquetas.Escrever(http, await repositorio.Adicionar(orcamento, cancelamento));

        return TypedResults.Created($"/api/orcamentos/{orcamento.Id}", OrcamentoResposta.De(orcamento));
    }

    private static async Task<Results<Ok<OrcamentoResposta>, StatusCodeHttpResult>> Obter(
        Guid id,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lido = await Leituras.Orcamento(repositorio, id, cancelamento);
        Etiquetas.Escrever(http, lido.Versao);

        return Etiquetas.ClienteJaTem(http, lido.Versao)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(OrcamentoResposta.De(lido.Agregado));
    }

    private static Task<Ok<OrcamentoResposta>> AlterarOrcamento(
        Guid id,
        AlteracaoDoOrcamento alteracao,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        if (alteracao is { Nome: null, Bdi: null })
        {
            throw new PedidoInvalido("Diga o que muda no orçamento: nome, bdi, ou os dois.");
        }

        return Alterar(
            id,
            versao,
            repositorio,
            http,
            orcamento =>
            {
                if (alteracao.Nome is { } nome)
                {
                    orcamento.Renomear(nome);
                }

                if (alteracao.Bdi is { } bdi)
                {
                    orcamento.AlterarBdi(bdi.ParaBdi());
                }
            },
            cancelamento);
    }

    private static async Task<NoContent> Remover(Guid id, VersaoLida versao, RepositorioDeOrcamentos repositorio, CancellationToken cancelamento)
    {
        await repositorio.Remover(new OrcamentoId(id), versao.Valor, cancelamento);

        return TypedResults.NoContent();
    }

    private static Task<Ok<OrcamentoResposta>> AdicionarEtapa(
        Guid id,
        EtapaNaEap nova,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            orcamento =>
            {
                if (nova.DentroDe is { } dentroDe)
                {
                    Exigir<Etapa>(orcamento, dentroDe, "a etapa");
                    orcamento.AdicionarEtapa(nova.Titulo, dentroDe);
                }
                else
                {
                    orcamento.AdicionarEtapa(nova.Titulo);
                }
            },
            cancelamento);

    private static Task<Ok<OrcamentoResposta>> RenomearEtapa(
        Guid id,
        string numero,
        NovoTitulo novo,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            orcamento =>
            {
                Exigir<Etapa>(orcamento, numero, "a etapa");
                orcamento.RenomearEtapa(numero, novo.Titulo);
            },
            cancelamento);

    private static Task<Ok<OrcamentoResposta>> RemoverEtapa(
        Guid id,
        string numero,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            orcamento =>
            {
                Exigir<Etapa>(orcamento, numero, "a etapa");
                orcamento.Remover(numero);
            },
            cancelamento);

    private static Task<Ok<OrcamentoResposta>> AdicionarItem(
        Guid id,
        ItemNaEap novo,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            orcamento =>
            {
                Exigir<Etapa>(orcamento, novo.DentroDe, "a etapa");
                IncluirItem(orcamento, novo.DentroDe, novo.Codigo, novo.Quantidade, novo.Atividade, novo.BdiDiferenciado);
            },
            cancelamento);

    private static Task<Ok<OrcamentoResposta>> AlterarQuantidade(
        Guid id,
        string numero,
        NovaQuantidade nova,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        AlterarItem(id, numero, versao, repositorio, http, orcamento => orcamento.AlterarQuantidade(numero, nova.Quantidade), cancelamento);

    private static Task<Ok<OrcamentoResposta>> RemoverItem(
        Guid id,
        string numero,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        AlterarItem(id, numero, versao, repositorio, http, orcamento => orcamento.Remover(numero), cancelamento);

    private static Task<Ok<OrcamentoResposta>> VincularAtividade(
        Guid id,
        string numero,
        AtividadeDoItem atividade,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        AlterarItem(id, numero, versao, repositorio, http, orcamento => orcamento.VincularAtividade(numero, atividade.Atividade), cancelamento);

    private static Task<Ok<OrcamentoResposta>> DesvincularAtividade(
        Guid id,
        string numero,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        AlterarItem(id, numero, versao, repositorio, http, orcamento => orcamento.DesvincularAtividade(numero), cancelamento);

    private static Task<Ok<OrcamentoResposta>> DefinirBdiDiferenciado(
        Guid id,
        string numero,
        BdiDoOrcamento bdi,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        AlterarItem(id, numero, versao, repositorio, http, orcamento => orcamento.DefinirBdiDiferenciado(numero, bdi.ParaBdi()), cancelamento);

    private static Task<Ok<OrcamentoResposta>> RemoverBdiDiferenciado(
        Guid id,
        string numero,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        AlterarItem(id, numero, versao, repositorio, http, orcamento => orcamento.RemoverBdiDiferenciado(numero), cancelamento);

    /// <param name="baseDeCustos">A base contra a qual calcular: <c>?baseDeCustos={id}</c>.</param>
    private static async Task<Ok<PlanilhaResposta>> Planilha(
        Guid id,
        Guid baseDeCustos,
        RepositorioDeOrcamentos orcamentos,
        RepositorioDeBasesDeCustos bases,
        CancellationToken cancelamento) =>
        TypedResults.Ok(PlanilhaResposta.De(await Calcular(id, baseDeCustos, orcamentos, bases, cancelamento)));

    private static async Task<Ok<IReadOnlyList<ItemDaCurvaAbcResposta>>> CurvaAbc(
        Guid id,
        Guid baseDeCustos,
        RepositorioDeOrcamentos orcamentos,
        RepositorioDeBasesDeCustos bases,
        CancellationToken cancelamento)
    {
        var planilha = await Calcular(id, baseDeCustos, orcamentos, bases, cancelamento);

        return TypedResults.Ok<IReadOnlyList<ItemDaCurvaAbcResposta>>([.. planilha.CurvaAbc().Select(ItemDaCurvaAbcResposta.De)]);
    }

    /// <param name="inicio">O dia pedido para começar a obra.</param>
    /// <param name="feriados">Os feriados, um parâmetro por data.</param>
    /// <param name="sabadoUtil">Se a obra trabalha aos sábados.</param>
    /// <param name="datas">Pelas datas cedo ou tarde do cronograma.</param>
    private static async Task<Ok<FisicoFinanceiroResposta>> FisicoFinanceiro(
        Guid id,
        Guid baseDeCustos,
        Guid cronograma,
        DateOnly inicio,
        [FromQuery] DateOnly[]? feriados,
        RepositorioDeOrcamentos orcamentos,
        RepositorioDeBasesDeCustos bases,
        RepositorioDeCronogramas cronogramas,
        CancellationToken cancelamento,
        bool sabadoUtil = false,
        DatasDoCronograma datas = DatasDoCronograma.Cedo)
    {
        var planilha = await Calcular(id, baseDeCustos, orcamentos, bases, cancelamento);
        var lido = await Leituras.Cronograma(cronogramas, cronograma, cancelamento);
        var obra = ProgramacaoDoCronograma.Programar(lido.Agregado, inicio, Leituras.Calendario(feriados, sabadoUtil));

        return TypedResults.Ok(FisicoFinanceiroResposta.De(CalculadoraDoFisicoFinanceiro.Calcular(planilha, obra, datas)));
    }

    private static async Task<OrcamentoCalculado> Calcular(
        Guid id,
        Guid baseDeCustos,
        RepositorioDeOrcamentos orcamentos,
        RepositorioDeBasesDeCustos bases,
        CancellationToken cancelamento)
    {
        var orcamento = await Leituras.Orcamento(orcamentos, id, cancelamento);
        var baseLida = await Leituras.BaseDeCustos(bases, baseDeCustos, cancelamento);

        return CalculadoraDoOrcamento.Calcular(orcamento.Agregado, baseLida.Agregado);
    }

    /// <summary>Monta a EAP pedida dentro da etapa, na ordem em que veio.</summary>
    private static void Montar(Orcamento orcamento, string etapa, IReadOnlyList<NovoNoDaEap>? filhos)
    {
        foreach (var filho in filhos ?? [])
        {
            switch (filho)
            {
                case NovaEtapa subetapa:
                    Montar(orcamento, orcamento.AdicionarEtapa(subetapa.Titulo, etapa).Valor, subetapa.Filhos);
                    break;
                case NovoItemDoOrcamento item:
                    IncluirItem(orcamento, etapa, item.Codigo, item.Quantidade, item.Atividade, item.BdiDiferenciado);
                    break;
                default:
                    throw new PedidoInvalido($"A etapa {etapa} tem um filho sem tipo: cada filho diz se é \"etapa\" ou \"item\".");
            }
        }
    }

    private static void IncluirItem(Orcamento orcamento, string etapa, string codigo, decimal quantidade, string? atividade, BdiDoOrcamento? bdi)
    {
        var numero = orcamento.AdicionarItem(etapa, codigo, quantidade).Valor;

        if (atividade is not null)
        {
            orcamento.VincularAtividade(numero, atividade);
        }

        if (bdi is not null)
        {
            orcamento.DefinirBdiDiferenciado(numero, bdi.ParaBdi());
        }
    }

    /// <summary>O nó da EAP com o número dado tem de existir e ser do tipo esperado — senão, 404.</summary>
    private static void Exigir<T>(Orcamento orcamento, string numero, string oQue)
        where T : NoDaEap
    {
        IReadOnlyList<NoDaEap> nivel = orcamento.Etapas;
        NoDaEap? no = null;

        foreach (var posicao in NumeroDaEap.Ler(numero).Posicoes())
        {
            if (posicao > nivel.Count)
            {
                no = null;
                break;
            }

            no = nivel[posicao - 1];
            nivel = no is Etapa etapa ? etapa.Filhos : [];
        }

        if (no is not T)
        {
            throw new AgregadoNaoEncontrado($"Não existe {oQue} {numero} no orçamento {orcamento.Id}.");
        }
    }

    private static Task<Ok<OrcamentoResposta>> AlterarItem(
        Guid id,
        string numero,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        Action<Orcamento> alteracao,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            orcamento =>
            {
                Exigir<ItemDoOrcamento>(orcamento, numero, "o item");
                alteracao(orcamento);
            },
            cancelamento);

    private static async Task<Ok<OrcamentoResposta>> Alterar(
        Guid id,
        VersaoLida versao,
        RepositorioDeOrcamentos repositorio,
        HttpContext http,
        Action<Orcamento> alteracao,
        CancellationToken cancelamento)
    {
        var lido = await Leituras.Orcamento(repositorio, id, cancelamento);
        Lidos.ConferirVersao(lido, versao, $"O orçamento {id}");

        alteracao(lido.Agregado);

        Etiquetas.Escrever(http, await repositorio.Atualizar(lido.Agregado, versao.Valor, cancelamento));

        return TypedResults.Ok(OrcamentoResposta.De(lido.Agregado));
    }
}
