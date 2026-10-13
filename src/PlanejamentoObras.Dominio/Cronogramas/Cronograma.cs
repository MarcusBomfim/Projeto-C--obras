using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Cronogramas;

/// <summary>
/// A rede de atividades de uma obra e os vínculos entre elas.
/// </summary>
/// <remarks>
/// <para>
/// Raiz do agregado. Toda mudança na rede passa por aqui, porque as regras que
/// importam são da rede inteira, não de uma atividade sozinha: código único,
/// vínculo apontando para atividade que existe, e — a principal — nenhum
/// ciclo.
/// </para>
/// <para>
/// As regras são garantidas na ESCRITA, não descobertas na leitura (ver
/// ADR-0002). Não existe instante em que este objeto contenha um ciclo: a
/// dependência que o fecharia é recusada antes de entrar. Por isso o cálculo
/// do caminho crítico pode assumir uma rede acíclica, em vez de ter que lidar
/// com uma.
/// </para>
/// <para>
/// O cronograma guarda a REDE, não as datas. Datas, folgas e caminho crítico
/// são derivados — calculados a partir da rede e de um calendário — e por isso
/// não são estado. Guardar o que é derivado é guardar algo que pode ficar
/// desatualizado em relação àquilo de que deriva.
/// </para>
/// </remarks>
public sealed class Cronograma
{
    public const int TamanhoMaximoDoNome = 160;

    // OrderedDictionary (.NET 9+): busca por código em O(1) e a ordem em que
    // as atividades foram cadastradas preservada — que é a ordem em que o
    // engenheiro espera vê-las quando duas começam no mesmo dia.
    private readonly OrderedDictionary<CodigoDaAtividade, Atividade> _atividades = [];

    private readonly List<Dependencia> _dependencias = [];

    public Cronograma(string nome)
    {
        Nome = ValidarNome(nome);
    }

    public string Nome { get; private set; }

    /// <summary>As atividades, na ordem em que foram cadastradas.</summary>
    public IReadOnlyList<Atividade> Atividades => _atividades.Values;

    /// <summary>
    /// Os vínculos, na ordem em que foram criados.
    /// </summary>
    /// <remarks>
    /// <c>AsReadOnly</c>, e não a lista convertida para a interface: quem
    /// recebesse a <c>List</c> poderia convertê-la de volta e acrescentar um
    /// vínculo sem passar pela checagem de ciclo.
    /// </remarks>
    public IReadOnlyList<Dependencia> Dependencias => _dependencias.AsReadOnly();

    public void Renomear(string nome) => Nome = ValidarNome(nome);

    public Atividade AdicionarAtividade(string codigo, string descricao, int duracaoEmDiasUteis)
    {
        var chave = new CodigoDaAtividade(codigo);

        if (_atividades.ContainsKey(chave))
        {
            throw new ExcecaoDeDominio($"Já existe a atividade {chave} neste cronograma.");
        }

        var atividade = new Atividade(chave, descricao, duracaoEmDiasUteis);
        _atividades.Add(chave, atividade);

        return atividade;
    }

    public bool ContemAtividade(string codigo) => _atividades.ContainsKey(new CodigoDaAtividade(codigo));

    public Atividade Atividade(string codigo) => Localizar(codigo);

    public void AlterarDuracaoDaAtividade(string codigo, int novaDuracaoEmDiasUteis) =>
        Localizar(codigo).AlterarDuracao(novaDuracaoEmDiasUteis);

    public void AlterarDescricaoDaAtividade(string codigo, string novaDescricao) =>
        Localizar(codigo).AlterarDescricao(novaDescricao);

    /// <summary>
    /// Remove a atividade e, junto, todo vínculo que chega nela ou sai dela.
    /// </summary>
    /// <remarks>
    /// Remover só a atividade deixaria vínculos apontando para o nada. A outra
    /// opção — recusar a remoção enquanto houver vínculo — obrigaria a
    /// desmontar a rede à mão antes; é mais trabalho sem ganho de segurança,
    /// porque um vínculo sem uma das pontas não tem significado nenhum.
    /// </remarks>
    public void RemoverAtividade(string codigo)
    {
        var atividade = Localizar(codigo);

        _dependencias.RemoveAll(dependencia =>
            dependencia.Predecessora == atividade.Codigo || dependencia.Sucessora == atividade.Codigo);

        _atividades.Remove(atividade.Codigo);
    }

    public Dependencia AdicionarDependencia(
        string predecessora,
        string sucessora,
        TipoDeDependencia tipo = TipoDeDependencia.TerminoInicio,
        int defasagemEmDiasUteis = 0)
    {
        var de = Localizar(predecessora).Codigo;
        var para = Localizar(sucessora).Codigo;

        if (de == para)
        {
            throw new ExcecaoDeDominio($"A atividade {de} não pode depender de si mesma.");
        }

        if (_dependencias.Exists(dependencia => dependencia.Predecessora == de && dependencia.Sucessora == para))
        {
            throw new ExcecaoDeDominio(
                $"Já existe um vínculo de {de} para {para}. Para mudar o tipo ou a defasagem, remova o atual e crie outro.");
        }

        /*
         * O vínculo de → para fecha um ciclo exatamente quando já existe um
         * caminho de `para` até `de`. A busca é pelo caminho, e não só um
         * "sim ou não": a mensagem mostra o ciclo inteiro, porque "fecharia um
         * ciclo" sem dizer qual obriga o engenheiro a procurar à mão numa rede
         * de duzentas atividades.
         */
        var caminho = CaminhoEntre(para, de);

        if (caminho is not null)
        {
            var ciclo = string.Join(" → ", caminho.Select(codigo => codigo.Valor)) + " → " + para;

            throw new ExcecaoDeDominio(
                $"O vínculo {de} → {para} fecharia um ciclo: {ciclo}. "
                + "Uma atividade não pode depender, direta ou indiretamente, de si mesma.");
        }

        var nova = new Dependencia(de, para, tipo, defasagemEmDiasUteis);
        _dependencias.Add(nova);

        return nova;
    }

    public void RemoverDependencia(string predecessora, string sucessora)
    {
        var de = Localizar(predecessora).Codigo;
        var para = Localizar(sucessora).Codigo;

        var removidas = _dependencias.RemoveAll(dependencia =>
            dependencia.Predecessora == de && dependencia.Sucessora == para);

        if (removidas == 0)
        {
            throw new ExcecaoDeDominio($"Não existe vínculo de {de} para {para}.");
        }
    }

    private Atividade Localizar(string codigo)
    {
        var chave = new CodigoDaAtividade(codigo);

        return _atividades.TryGetValue(chave, out var atividade)
            ? atividade
            : throw new ExcecaoDeDominio($"Não existe a atividade {chave} neste cronograma.");
    }

    /// <summary>
    /// Um caminho de <paramref name="origem"/> até <paramref name="destino"/>
    /// seguindo os vínculos, ou nulo se não houver.
    /// </summary>
    /// <remarks>
    /// Busca em largura: devolve o caminho mais curto, que é o mais fácil de
    /// ler na mensagem de erro. A lista de adjacência é montada uma vez por
    /// chamada — O(V + E) no total, em vez de varrer todos os vínculos a cada
    /// atividade visitada.
    /// </remarks>
    private List<CodigoDaAtividade>? CaminhoEntre(CodigoDaAtividade origem, CodigoDaAtividade destino)
    {
        var sucessoras = _dependencias
            .GroupBy(dependencia => dependencia.Predecessora)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Select(dependencia => dependencia.Sucessora).ToList());

        var anterior = new Dictionary<CodigoDaAtividade, CodigoDaAtividade>();
        var visitadas = new HashSet<CodigoDaAtividade> { origem };
        var fila = new Queue<CodigoDaAtividade>();
        fila.Enqueue(origem);

        while (fila.Count > 0)
        {
            var atual = fila.Dequeue();

            if (atual == destino)
            {
                var caminho = new List<CodigoDaAtividade> { atual };

                while (anterior.TryGetValue(caminho[^1], out var passo))
                {
                    caminho.Add(passo);
                }

                caminho.Reverse();

                return caminho;
            }

            if (!sucessoras.TryGetValue(atual, out var proximas))
            {
                continue;
            }

            foreach (var proxima in proximas)
            {
                if (visitadas.Add(proxima))
                {
                    anterior[proxima] = atual;
                    fila.Enqueue(proxima);
                }
            }
        }

        return null;
    }

    private static string ValidarNome(string nome)
    {
        var limpo = (nome ?? string.Empty).Trim();

        if (limpo.Length == 0)
        {
            throw new ExcecaoDeDominio("O cronograma precisa de um nome.");
        }

        if (limpo.Length > TamanhoMaximoDoNome)
        {
            throw new ExcecaoDeDominio($"O nome do cronograma tem no máximo {TamanhoMaximoDoNome} caracteres.");
        }

        return limpo;
    }
}
