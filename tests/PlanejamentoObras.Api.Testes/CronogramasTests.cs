using System.Net;
using PlanejamentoObras.Api.Recursos;
using PlanejamentoObras.Api.Recursos.Cronogramas;
using PlanejamentoObras.Api.Seguranca;
using PlanejamentoObras.Dominio.Cronogramas;

namespace PlanejamentoObras.Api.Testes;

/*
 * O cronograma pela API: a rede, montada de uma vez ou passo a passo, e o
 * que se calcula dela. Os números são os do exemplo do README.
 */
public sealed class CronogramasTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly HttpClient _planejador = api.Cliente(Guid.CreateVersion7(), Papeis.Planejador);

    private async Task<string> CronogramaDoReadme() =>
        (await _planejador.Criar("/api/cronogramas", RedeDoReadme.Cronograma())).Headers.Location!.ToString();

    private Task<HttpResponseMessage> Alterar(HttpMethod metodo, string caminho, int versao, object? corpo = null) =>
        _planejador.Enviar(metodo, caminho, corpo, versao);

    [Fact]
    public async Task Cria_a_rede_inteira_de_uma_vez()
    {
        var criado = await _planejador.Criar("/api/cronogramas", RedeDoReadme.Cronograma());
        var cronograma = await criado.Ler<CronogramaResposta>();

        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        Assert.Equal($"/api/cronogramas/{cronograma.Id}", criado.Headers.Location?.ToString());
        Assert.Equal(("Residencial Vista Serra", 8, 8), (cronograma.Nome, cronograma.Atividades.Count, cronograma.Dependencias.Count));
        Assert.True(cronograma.Atividades.Single(atividade => atividade.Codigo == "ENT").EhMarco);
    }

    [Fact]
    public async Task Calcula_o_caminho_critico_do_exemplo()
    {
        var cronograma = await CronogramaDoReadme();

        var caminho = await (await _planejador.GetAsync($"{cronograma}/caminho-critico", Cancelamento)).Ler<CaminhoCriticoResposta>();

        Assert.Equal(64, caminho.DuracaoEmDiasUteis);
        Assert.Equal(
            ["MOB", "FUN", "EST", "ALV", "REB", "PIN", "ENT"],
            caminho.Atividades.Where(atividade => atividade.EhCritica).Select(atividade => atividade.Codigo));
        var eletrica = caminho.Atividades.Single(atividade => atividade.Codigo == "ELE");
        Assert.Equal((3, 3), (eletrica.FolgaTotal, eletrica.FolgaLivre));
    }

    [Fact]
    public async Task Programa_no_calendario_com_os_feriados()
    {
        var cronograma = await CronogramaDoReadme();

        var resposta = await _planejador.GetAsync($"{cronograma}/programacao?inicio={RedeDoReadme.Inicio}&{RedeDoReadme.Feriados}", Cancelamento);
        var programacao = await resposta.Ler<ProgramacaoResposta>();
        var da = programacao.Atividades.ToDictionary(atividade => atividade.Codigo);

        Assert.Equal((new DateOnly(2026, 10, 5), new DateOnly(2027, 1, 7), 64), (programacao.DataDeInicio, programacao.DataDeTermino, programacao.DuracaoEmDiasUteis));
        Assert.Equal(new DateOnly(2026, 10, 13), da["FUN"].InicioCedo);
        Assert.Equal((new DateOnly(2026, 12, 11), new DateOnly(2026, 12, 16)), (da["ELE"].TerminoCedo, da["ELE"].TerminoTarde));
        Assert.Equal((new DateOnly(2027, 1, 7), new DateOnly(2027, 1, 7)), (da["ENT"].InicioCedo, da["ENT"].TerminoCedo));
    }

    [Fact]
    public async Task Sem_a_data_de_inicio_a_programacao_e_400()
    {
        var cronograma = await CronogramaDoReadme();

        var resposta = await _planejador.GetAsync($"{cronograma}/programacao", Cancelamento);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Monta_a_rede_passo_a_passo_e_cada_passo_sobe_a_versao()
    {
        var cronograma = (await _planejador.Criar("/api/cronogramas", new { nome = "Casa" })).Headers.Location!.ToString();

        var fundacoes = await Alterar(HttpMethod.Post, $"{cronograma}/atividades", 1, new { codigo = "fun", descricao = "Fundações", duracaoEmDiasUteis = 10 });
        var estrutura = await Alterar(HttpMethod.Post, $"{cronograma}/atividades", 2, new { codigo = "EST", descricao = "Estrutura", duracaoEmDiasUteis = 20 });
        var vinculo = await Alterar(HttpMethod.Post, $"{cronograma}/dependencias", 3, new { predecessora = "FUN", sucessora = "EST", tipo = "InicioInicio", defasagemEmDiasUteis = 5 });
        var duracao = await Alterar(HttpMethod.Patch, $"{cronograma}/atividades/EST", 4, new { duracaoEmDiasUteis = 25 });
        var renomeado = await Alterar(HttpMethod.Patch, cronograma, 5, new { nome = "Casa térrea" });

        var final = await renomeado.Ler<CronogramaResposta>();

        Assert.Equal([2, 3, 4, 5, 6], new[] { fundacoes, estrutura, vinculo, duracao, renomeado }.Select(resposta => resposta.Versao()));
        Assert.Equal("Casa térrea", final.Nome);
        Assert.Equal(["FUN", "EST"], final.Atividades.Select(atividade => atividade.Codigo));
        Assert.Equal(25, final.Atividades.Single(atividade => atividade.Codigo == "EST").DuracaoEmDiasUteis);
        Assert.Equal(new DependenciaResposta("FUN", "EST", TipoDeDependencia.InicioInicio, 5), Assert.Single(final.Dependencias));
    }

    [Fact]
    public async Task Desmonta_a_rede()
    {
        var cronograma = await CronogramaDoReadme();

        var semVinculo = await Alterar(HttpMethod.Delete, $"{cronograma}/dependencias/ELE/REB", 1);
        var semAtividade = await Alterar(HttpMethod.Delete, $"{cronograma}/atividades/ELE", 2);
        var final = await semAtividade.Ler<CronogramaResposta>();

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (semVinculo.StatusCode, semAtividade.StatusCode));
        Assert.DoesNotContain(final.Atividades, atividade => atividade.Codigo == "ELE");
        Assert.DoesNotContain(final.Dependencias, dependencia => dependencia.Predecessora == "ELE" || dependencia.Sucessora == "ELE");
    }

    [Fact]
    public async Task Vinculo_que_fecha_um_ciclo_e_422_com_o_ciclo_na_mensagem()
    {
        var cronograma = await CronogramaDoReadme();

        var resposta = await Alterar(HttpMethod.Post, $"{cronograma}/dependencias", 1, new { predecessora = "PIN", sucessora = "EST" });
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Contains("EST", problema.Detail, StringComparison.Ordinal);
        Assert.Contains("PIN", problema.Detail, StringComparison.Ordinal);
        Assert.Equal("\"1\"", (await _planejador.GetAsync(cronograma, Cancelamento)).Headers.ETag?.Tag);
    }

    [Fact]
    public async Task Atividade_ou_vinculo_que_nao_existe_e_404()
    {
        var cronograma = await CronogramaDoReadme();

        var atividade = await Alterar(HttpMethod.Patch, $"{cronograma}/atividades/XYZ", 1, new { duracaoEmDiasUteis = 3 });
        var semAtividade = await Alterar(HttpMethod.Delete, $"{cronograma}/atividades/XYZ", 1);
        var vinculo = await Alterar(HttpMethod.Delete, $"{cronograma}/dependencias/MOB/PIN", 1);

        Assert.All(new[] { atividade, semAtividade, vinculo }, resposta => Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode));
        Assert.Equal("Não existe vínculo de MOB para PIN no cronograma " + cronograma.Split('/')[^1] + ".", (await vinculo.Problema()).Detail);
    }

    [Fact]
    public async Task Alterar_uma_atividade_sem_dizer_o_que_muda_e_400()
    {
        var cronograma = await CronogramaDoReadme();

        var resposta = await Alterar(HttpMethod.Patch, $"{cronograma}/atividades/EST", 1, new { });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Removido_deixa_de_existir()
    {
        var cronograma = await CronogramaDoReadme();

        var removido = await Alterar(HttpMethod.Delete, cronograma, 1);
        var lido = await _planejador.GetAsync(cronograma, Cancelamento);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NotFound), (removido.StatusCode, lido.StatusCode));
    }

    [Fact]
    public async Task Lista_os_cronogramas_da_empresa_por_nome()
    {
        await _planejador.Criar("/api/cronogramas", new { nome = "Galpão" });
        await _planejador.Criar("/api/cronogramas", new { nome = "Casa" });

        var lista = await (await _planejador.GetAsync("/api/cronogramas", Cancelamento)).Ler<List<ResumoResposta>>();

        Assert.Equal(["Casa", "Galpão"], lista.Select(resumo => resumo.Nome));
        Assert.All(lista, resumo => Assert.Equal(1, resumo.Versao));
    }
}
