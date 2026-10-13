namespace PlanejamentoObras.Dominio.Cronogramas;

/// <summary>
/// O método do caminho crítico (CPM): datas mais cedo, datas mais tarde,
/// folgas e quais atividades não podem atrasar.
/// </summary>
/// <remarks>
/// <para>
/// Três passos, todos em O(V + E) depois da ordenação:
/// </para>
/// <list type="number">
/// <item><b>Ordem topológica</b> — toda predecessora antes da sucessora.</item>
/// <item><b>Ida</b> — na ordem, cada atividade começa o mais cedo que os
/// vínculos de entrada permitem. Dá o início cedo e a duração da obra.</item>
/// <item><b>Volta</b> — na ordem inversa, cada atividade termina o mais tarde
/// que os vínculos de saída permitem sem estourar a duração da obra. Dá o
/// término tarde.</item>
/// </list>
/// <para>
/// Todo vínculo é lido da mesma forma: "o ponto X da sucessora vem depois do
/// ponto Y da predecessora, mais a defasagem", com X e Y sendo início ou
/// término conforme o tipo (<see cref="TipoDeDependenciaExtensoes"/>). Os
/// quatro tipos — TI, II, TT, IT — viram uma fórmula só na ida e uma só na
/// volta, em vez de quatro ramos repetidos nos dois sentidos.
/// </para>
/// <para>
/// Stateless e sem efeito colateral: recebe a rede, devolve a análise. É um
/// serviço de domínio porque o cálculo não pertence a nenhuma atividade
/// sozinha — depende da rede inteira — e não é estado do cronograma, porque é
/// derivado dele.
/// </para>
/// </remarks>
public static class CalculadoraDeCaminhoCritico
{
    public static AnaliseDoCaminhoCritico Analisar(Cronograma cronograma)
    {
        ArgumentNullException.ThrowIfNull(cronograma);

        var atividades = cronograma.Atividades;
        var posicao = new Dictionary<CodigoDaAtividade, int>(atividades.Count);
        var duracao = new Dictionary<CodigoDaAtividade, int>(atividades.Count);
        var entrando = new Dictionary<CodigoDaAtividade, List<Dependencia>>(atividades.Count);
        var saindo = new Dictionary<CodigoDaAtividade, List<Dependencia>>(atividades.Count);

        for (var i = 0; i < atividades.Count; i++)
        {
            var codigo = atividades[i].Codigo;
            posicao[codigo] = i;
            duracao[codigo] = atividades[i].DuracaoEmDiasUteis;
            entrando[codigo] = [];
            saindo[codigo] = [];
        }

        foreach (var dependencia in cronograma.Dependencias)
        {
            entrando[dependencia.Sucessora].Add(dependencia);
            saindo[dependencia.Predecessora].Add(dependencia);
        }

        var ordem = OrdemTopologica(atividades, posicao, entrando, saindo);

        // ---------------------------------------------------------- ida

        var inicioCedo = new Dictionary<CodigoDaAtividade, int>(atividades.Count);
        var terminoCedo = new Dictionary<CodigoDaAtividade, int>(atividades.Count);
        var marcoDeTermino = new Dictionary<CodigoDaAtividade, bool>(atividades.Count);

        foreach (var codigo in ordem)
        {
            /*
             * Começa em 0 — nenhuma atividade começa antes da obra — e sobe
             * até o maior início que algum vínculo de entrada exige. Um
             * vínculo TT com sucessora mais longa que a predecessora, por
             * exemplo, "pediria" início negativo; o zero absorve isso.
             */
            var inicio = 0;

            // O começo da obra é começo de dia: o que nada empurra é de início.
            var deTermino = false;

            foreach (var vinculo in entrando[codigo])
            {
                var exigido = Ancora(vinculo, inicioCedo, terminoCedo) + vinculo.DefasagemEmDiasUteis;

                if (vinculo.Tipo.RestringeOTerminoDaSucessora)
                {
                    exigido -= duracao[codigo];
                }

                var vemDeTermino = AncoraEhTermino(vinculo, duracao, marcoDeTermino);

                if (exigido > inicio)
                {
                    inicio = exigido;
                    deTermino = vemDeTermino;
                }
                else if (exigido == inicio)
                {
                    /*
                     * Empate entre um término e um início no mesmo instante:
                     * o fim de um dia e o começo do seguinte. O marco tem que
                     * aparecer depois dos dois, e só o começo do dia seguinte
                     * fica depois dos dois. Ver ADR-0003.
                     */
                    deTermino &= vemDeTermino;
                }
            }

            inicioCedo[codigo] = inicio;
            terminoCedo[codigo] = inicio + duracao[codigo];
            marcoDeTermino[codigo] = deTermino;
        }

        var duracaoDaObra = terminoCedo.Count == 0 ? 0 : terminoCedo.Values.Max();

        // -------------------------------------------------------- volta

        var inicioTarde = new Dictionary<CodigoDaAtividade, int>(atividades.Count);
        var terminoTarde = new Dictionary<CodigoDaAtividade, int>(atividades.Count);

        for (var i = ordem.Count - 1; i >= 0; i--)
        {
            var codigo = ordem[i];

            /*
             * Começa no fim da obra — nenhuma atividade termina depois dela —
             * e desce até o menor término que algum vínculo de saída permite.
             * É o espelho da ida: lá o limite era o início da obra, aqui é o
             * fim.
             */
            var termino = duracaoDaObra;

            foreach (var vinculo in saindo[codigo])
            {
                var pontoDaSucessora = vinculo.Tipo.RestringeOTerminoDaSucessora
                    ? terminoTarde[vinculo.Sucessora]
                    : inicioTarde[vinculo.Sucessora];

                // Até onde a âncora da predecessora pode ir. Se a âncora é o
                // início, o término pode ir uma duração além.
                var limite = pontoDaSucessora - vinculo.DefasagemEmDiasUteis;

                if (!vinculo.Tipo.AncoraNoTerminoDaPredecessora)
                {
                    limite += duracao[codigo];
                }

                termino = Math.Min(termino, limite);
            }

            terminoTarde[codigo] = termino;
            inicioTarde[codigo] = termino - duracao[codigo];
        }

        // ------------------------------------------------------- folgas

        var tempos = new List<TemposDaAtividade>(atividades.Count);

        foreach (var codigo in ordem)
        {
            var folgaTotal = inicioTarde[codigo] - inicioCedo[codigo];

            /*
             * Folga livre: quanto a atividade pode atrasar sem empurrar
             * NENHUMA sucessora. É a menor sobra entre os vínculos de saída;
             * sem sucessora, é a sobra até o fim da obra.
             *
             * Por definição ela nunca passa da folga total — se a atividade
             * não empurra ninguém, também não empurra a obra. Com defasagem
             * negativa e marco no fim da rede, a conta crua pode passar; o
             * Clamp mantém a definição de pé.
             */
            var folgaLivre = saindo[codigo].Count == 0
                ? duracaoDaObra - terminoCedo[codigo]
                : saindo[codigo].Min(vinculo => FolgaDoVinculo(vinculo, inicioCedo, terminoCedo));

            folgaLivre = Math.Clamp(folgaLivre, 0, folgaTotal);

            tempos.Add(new TemposDaAtividade(
                codigo,
                duracao[codigo],
                inicioCedo[codigo],
                terminoCedo[codigo],
                inicioTarde[codigo],
                terminoTarde[codigo],
                folgaTotal,
                folgaLivre,
                MarcoDeTermino: duracao[codigo] == 0 && marcoDeTermino[codigo]));
        }

        // Ordem do Gantt: quem começa antes vem antes; empate, a ordem de
        // cadastro.
        var ordenadas = tempos
            .OrderBy(t => t.InicioCedo)
            .ThenBy(t => posicao[t.Codigo])
            .ToList();

        var criticas = ordenadas.Where(t => t.EhCritica).Select(t => t.Codigo).ToHashSet();

        var dependenciasCriticas = cronograma.Dependencias
            .Where(vinculo =>
                criticas.Contains(vinculo.Predecessora)
                && criticas.Contains(vinculo.Sucessora)
                && FolgaDoVinculo(vinculo, inicioCedo, terminoCedo) == 0)
            .ToList();

        return new AnaliseDoCaminhoCritico(duracaoDaObra, ordenadas, dependenciasCriticas);
    }

    /// <summary>
    /// Ordem topológica pelo algoritmo de Kahn, desempatando pela ordem de
    /// cadastro — para o resultado ser o mesmo a cada cálculo.
    /// </summary>
    private static List<CodigoDaAtividade> OrdemTopologica(
        IReadOnlyList<Atividade> atividades,
        Dictionary<CodigoDaAtividade, int> posicao,
        Dictionary<CodigoDaAtividade, List<Dependencia>> entrando,
        Dictionary<CodigoDaAtividade, List<Dependencia>> saindo)
    {
        var pendentes = atividades.ToDictionary(atividade => atividade.Codigo, atividade => entrando[atividade.Codigo].Count);
        var prontas = new PriorityQueue<CodigoDaAtividade, int>();

        foreach (var atividade in atividades)
        {
            if (pendentes[atividade.Codigo] == 0)
            {
                prontas.Enqueue(atividade.Codigo, posicao[atividade.Codigo]);
            }
        }

        var ordem = new List<CodigoDaAtividade>(atividades.Count);

        while (prontas.TryDequeue(out var codigo, out _))
        {
            ordem.Add(codigo);

            foreach (var vinculo in saindo[codigo])
            {
                if (--pendentes[vinculo.Sucessora] == 0)
                {
                    prontas.Enqueue(vinculo.Sucessora, posicao[vinculo.Sucessora]);
                }
            }
        }

        /*
         * Não deveria acontecer: o Cronograma recusa o vínculo que fecharia
         * um ciclo (ADR-0002). Se acontecer, é defeito de programação, não
         * erro de quem usa — por isso InvalidOperationException, e não
         * ExcecaoDeDominio.
         */
        if (ordem.Count != atividades.Count)
        {
            throw new InvalidOperationException(
                "A rede do cronograma tem um ciclo, e o Cronograma deveria tê-lo impedido na escrita.");
        }

        return ordem;
    }

    /// <summary>O ponto da predecessora de onde o vínculo parte: início ou término.</summary>
    private static int Ancora(
        Dependencia vinculo,
        Dictionary<CodigoDaAtividade, int> inicio,
        Dictionary<CodigoDaAtividade, int> termino) =>
        vinculo.Tipo.AncoraNoTerminoDaPredecessora
            ? termino[vinculo.Predecessora]
            : inicio[vinculo.Predecessora];

    /// <summary>
    /// Se o ponto de onde o vínculo parte é um término — o fim de um dia — ou
    /// um início — o começo de um dia.
    /// </summary>
    /// <remarks>
    /// Para uma atividade com duração, quem diz é o tipo do vínculo. Um marco
    /// não tem início e término separados, é um instante só: o vínculo parte
    /// do lado em que o próprio marco está, qualquer que seja o tipo.
    /// </remarks>
    private static bool AncoraEhTermino(
        Dependencia vinculo,
        Dictionary<CodigoDaAtividade, int> duracao,
        Dictionary<CodigoDaAtividade, bool> marcoDeTermino) =>
        duracao[vinculo.Predecessora] == 0
            ? marcoDeTermino[vinculo.Predecessora]
            : vinculo.Tipo.AncoraNoTerminoDaPredecessora;

    /// <summary>
    /// Quantos dias sobram no vínculo, com as datas cedo: zero quando ele está
    /// "esticado" e é ele que segura a sucessora.
    /// </summary>
    private static int FolgaDoVinculo(
        Dependencia vinculo,
        Dictionary<CodigoDaAtividade, int> inicio,
        Dictionary<CodigoDaAtividade, int> termino)
    {
        var pontoDaSucessora = vinculo.Tipo.RestringeOTerminoDaSucessora
            ? termino[vinculo.Sucessora]
            : inicio[vinculo.Sucessora];

        return pontoDaSucessora - vinculo.DefasagemEmDiasUteis - Ancora(vinculo, inicio, termino);
    }
}
