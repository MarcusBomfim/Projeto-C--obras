using System.Diagnostics.CodeAnalysis;
using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Custos;

/// <summary>
/// Os insumos e as composições de custo de uma data-base: o equivalente a
/// uma tabela SINAPI de um mês, ou à tabela própria da construtora.
/// </summary>
/// <remarks>
/// <para>
/// Raiz do agregado, pelo mesmo motivo do cronograma: as regras que importam
/// são da base inteira. Toda composição usa só o que existe na base; nada que
/// esteja em uso pode ser removido; e nenhuma composição usa, direta ou
/// indiretamente, a si mesma — a mesma decisão da ADR-0002, garantida na
/// escrita.
/// </para>
/// <para>
/// A base guarda preços e coeficientes, não custos. O custo de uma composição
/// é derivado — ver <see cref="CalculadoraDeCustos"/> — e muda sozinho
/// quando muda o preço de um insumo lá embaixo.
/// </para>
/// </remarks>
public sealed class BaseDeCustos
{
    public const int TamanhoMaximoDoNome = 160;

    private readonly OrderedDictionary<CodigoDeCusto, Insumo> _insumos = [];

    private readonly OrderedDictionary<CodigoDeCusto, Composicao> _composicoes = [];

    public BaseDeCustos(string nome, DateOnly dataBase)
    {
        Nome = ValidarNome(nome);
        DataBase = dataBase;
    }

    public string Nome { get; private set; }

    /// <summary>A data a que os preços se referem: "SINAPI de setembro de 2026".</summary>
    public DateOnly DataBase { get; private set; }

    /// <summary>Os insumos, na ordem em que foram cadastrados.</summary>
    public IReadOnlyList<Insumo> Insumos => _insumos.Values;

    /// <summary>As composições, na ordem em que foram cadastradas.</summary>
    public IReadOnlyList<Composicao> Composicoes => _composicoes.Values;

    public void Renomear(string nome) => Nome = ValidarNome(nome);

    /// <summary>Muda a data a que os preços se referem — ao atualizar a base para um mês novo.</summary>
    public void AlterarDataBase(DateOnly dataBase) => DataBase = dataBase;

    public bool Contem(string codigo) => Contem(new CodigoDeCusto(codigo));

    // ---------------------------------------------------------- insumos

    public Insumo AdicionarInsumo(string codigo, string descricao, string unidade, TipoDeInsumo tipo, decimal preco)
    {
        var chave = CodigoLivre(codigo);
        var insumo = new Insumo(chave, descricao, new UnidadeDeMedida(unidade), tipo, new Dinheiro(preco));
        _insumos.Add(chave, insumo);

        return insumo;
    }

    public Insumo Insumo(string codigo) => LocalizarInsumo(new CodigoDeCusto(codigo));

    public void AlterarPrecoDoInsumo(string codigo, decimal preco) =>
        LocalizarInsumo(new CodigoDeCusto(codigo)).AlterarPreco(new Dinheiro(preco));

    /// <summary>
    /// Remove o insumo, se nenhuma composição o usa.
    /// </summary>
    /// <remarks>
    /// Ao contrário da atividade no cronograma, aqui a remoção é recusada, e
    /// não propagada. Tirar o vínculo de uma atividade removida não muda o que
    /// as outras são; tirar um insumo de uma composição muda o custo dela em
    /// silêncio — e de todo orçamento que a usa.
    /// </remarks>
    public void RemoverInsumo(string codigo)
    {
        var insumo = LocalizarInsumo(new CodigoDeCusto(codigo));
        var usuarias = Usuarias(insumo.Codigo);

        if (usuarias.Count > 0)
        {
            throw new ExcecaoDeDominio(
                $"O insumo {insumo.Codigo} é usado em {Listar(usuarias)}. Tire-o dessas composições antes de removê-lo.");
        }

        _insumos.Remove(insumo.Codigo);
    }

    // ------------------------------------------------------ composições

    public Composicao AdicionarComposicao(string codigo, string descricao, string unidade)
    {
        var chave = CodigoLivre(codigo);
        var composicao = new Composicao(chave, descricao, new UnidadeDeMedida(unidade));
        _composicoes.Add(chave, composicao);

        return composicao;
    }

    public Composicao Composicao(string codigo) => LocalizarComposicao(new CodigoDeCusto(codigo));

    public void RemoverComposicao(string codigo)
    {
        var composicao = LocalizarComposicao(new CodigoDeCusto(codigo));
        var usuarias = Usuarias(composicao.Codigo);

        if (usuarias.Count > 0)
        {
            throw new ExcecaoDeDominio(
                $"A composição {composicao.Codigo} é usada em {Listar(usuarias)}. Tire-a dessas composições antes de removê-la.");
        }

        _composicoes.Remove(composicao.Codigo);
    }

    /// <summary>
    /// Inclui um insumo, ou outra composição, na composição.
    /// </summary>
    public void AdicionarItem(string composicao, string item, decimal coeficiente)
    {
        var alvo = LocalizarComposicao(new CodigoDeCusto(composicao));
        var chave = new CodigoDeCusto(item);

        if (!Contem(chave))
        {
            throw new ExcecaoDeDominio(
                $"Não existe {chave} nesta base. Cadastre o insumo ou a composição antes de usá-lo.");
        }

        if (chave == alvo.Codigo)
        {
            throw new ExcecaoDeDominio($"A composição {chave} não pode usar a si mesma.");
        }

        if (alvo.Usa(chave))
        {
            throw new ExcecaoDeDominio(
                $"A composição {alvo.Codigo} já usa {chave}. Para mudar a quantidade, altere o coeficiente.");
        }

        /*
         * Usar `item` em `alvo` fecha um ciclo exatamente quando `item` já
         * usa `alvo`, direta ou indiretamente. Como no cronograma, a busca
         * devolve o caminho, para a mensagem mostrar o ciclo inteiro.
         */
        var caminho = CaminhoEntre(chave, alvo.Codigo);

        if (caminho is not null)
        {
            var ciclo = alvo.Codigo + " → " + string.Join(" → ", caminho.Select(codigo => codigo.Valor));

            throw new ExcecaoDeDominio(
                $"Usar {chave} em {alvo.Codigo} fecharia um ciclo: {ciclo}. "
                + "Uma composição não pode usar, direta ou indiretamente, a si mesma.");
        }

        alvo.AdicionarItem(new ItemDaComposicao(chave, coeficiente));
    }

    public void AlterarCoeficiente(string composicao, string item, decimal coeficiente) =>
        LocalizarComposicao(new CodigoDeCusto(composicao)).AlterarCoeficiente(new CodigoDeCusto(item), coeficiente);

    public void RemoverItem(string composicao, string item) =>
        LocalizarComposicao(new CodigoDeCusto(composicao)).RemoverItem(new CodigoDeCusto(item));

    // ------------------------------------------------- para o cálculo

    internal bool TentarInsumo(CodigoDeCusto codigo, [NotNullWhen(true)] out Insumo? insumo) =>
        _insumos.TryGetValue(codigo, out insumo);

    internal bool TentarComposicao(CodigoDeCusto codigo, [NotNullWhen(true)] out Composicao? composicao) =>
        _composicoes.TryGetValue(codigo, out composicao);

    // --------------------------------------------------------- apoio

    private bool Contem(CodigoDeCusto codigo) => _insumos.ContainsKey(codigo) || _composicoes.ContainsKey(codigo);

    private CodigoDeCusto CodigoLivre(string codigo)
    {
        var chave = new CodigoDeCusto(codigo);

        if (_insumos.ContainsKey(chave))
        {
            throw new ExcecaoDeDominio($"Já existe o insumo {chave} nesta base.");
        }

        if (_composicoes.ContainsKey(chave))
        {
            throw new ExcecaoDeDominio($"Já existe a composição {chave} nesta base.");
        }

        return chave;
    }

    private Insumo LocalizarInsumo(CodigoDeCusto codigo) =>
        _insumos.TryGetValue(codigo, out var insumo)
            ? insumo
            : throw new ExcecaoDeDominio($"Não existe o insumo {codigo} nesta base.");

    private Composicao LocalizarComposicao(CodigoDeCusto codigo) =>
        _composicoes.TryGetValue(codigo, out var composicao)
            ? composicao
            : throw new ExcecaoDeDominio($"Não existe a composição {codigo} nesta base.");

    private List<CodigoDeCusto> Usuarias(CodigoDeCusto codigo) =>
        [.. _composicoes.Values.Where(composicao => composicao.Usa(codigo)).Select(composicao => composicao.Codigo)];

    private static string Listar(List<CodigoDeCusto> composicoes) =>
        composicoes.Count == 1
            ? $"{composicoes[0]}"
            : string.Join(", ", composicoes.SkipLast(1)) + " e " + composicoes[^1];

    /// <summary>
    /// Um caminho de <paramref name="origem"/> até <paramref name="destino"/>
    /// descendo pelas composições auxiliares, ou nulo se não houver.
    /// </summary>
    private List<CodigoDeCusto>? CaminhoEntre(CodigoDeCusto origem, CodigoDeCusto destino)
    {
        var anterior = new Dictionary<CodigoDeCusto, CodigoDeCusto>();
        var visitados = new HashSet<CodigoDeCusto> { origem };
        var fila = new Queue<CodigoDeCusto>();
        fila.Enqueue(origem);

        while (fila.Count > 0)
        {
            var atual = fila.Dequeue();

            if (atual == destino)
            {
                var caminho = new List<CodigoDeCusto> { atual };

                while (anterior.TryGetValue(caminho[^1], out var passo))
                {
                    caminho.Add(passo);
                }

                caminho.Reverse();

                return caminho;
            }

            // Insumo é folha: só composição tem itens para descer.
            if (!_composicoes.TryGetValue(atual, out var composicao))
            {
                continue;
            }

            foreach (var item in composicao.Itens)
            {
                if (visitados.Add(item.Codigo))
                {
                    anterior[item.Codigo] = atual;
                    fila.Enqueue(item.Codigo);
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
            throw new ExcecaoDeDominio("A base de custos precisa de um nome.");
        }

        if (limpo.Length > TamanhoMaximoDoNome)
        {
            throw new ExcecaoDeDominio($"O nome da base de custos tem no máximo {TamanhoMaximoDoNome} caracteres.");
        }

        return limpo;
    }
}
