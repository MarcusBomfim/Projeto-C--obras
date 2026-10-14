using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// O orçamento de uma obra: a EAP, as quantidades e o BDI.
/// </summary>
/// <remarks>
/// <para>
/// Raiz do agregado. Guarda a estrutura e as quantidades; preços e totais são
/// derivados, calculados contra uma base de custos por
/// <see cref="CalculadoraDoOrcamento"/>. Trocar a base — o mês seguinte da
/// tabela, outra região — reprecifica o orçamento sem mexer nele (ADR-0005).
/// </para>
/// <para>
/// As linhas são endereçadas pela numeração da EAP, como o engenheiro as
/// chama. A regra estrutural é uma só: item fica dentro de etapa, e nada fica
/// dentro de item.
/// </para>
/// </remarks>
public sealed class Orcamento
{
    public const int TamanhoMaximoDoNome = 160;

    private readonly List<Etapa> _etapas = [];

    public Orcamento(string nome, Bdi bdi)
        : this(OrcamentoId.Novo(), nome, bdi)
    {
    }

    /// <summary>
    /// Um orçamento vazio com uma identidade já existente — o começo da
    /// reconstituição de um orçamento gravado, que a persistência completa
    /// pelos métodos de sempre (ADR-0010).
    /// </summary>
    internal Orcamento(OrcamentoId id, string nome, Bdi bdi)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(bdi);

        Id = id;
        Nome = ValidarNome(nome);
        Bdi = bdi;
    }

    public OrcamentoId Id { get; }

    public string Nome { get; private set; }

    /// <summary>O BDI de todos os itens que não têm um diferenciado.</summary>
    public Bdi Bdi { get; private set; }

    /// <summary>As etapas de primeiro nível, na ordem da planilha.</summary>
    public IReadOnlyList<Etapa> Etapas => _etapas.AsReadOnly();

    public void Renomear(string nome) => Nome = ValidarNome(nome);

    public void AlterarBdi(Bdi bdi)
    {
        ArgumentNullException.ThrowIfNull(bdi);

        Bdi = bdi;
    }

    // ------------------------------------------------------------ etapas

    /// <summary>Acrescenta uma etapa de primeiro nível no fim da planilha.</summary>
    public NumeroDaEap AdicionarEtapa(string titulo)
    {
        _etapas.Add(new Etapa(titulo));

        return NumeroDaEap.De([_etapas.Count]);
    }

    /// <summary>Acrescenta uma etapa no fim de outra.</summary>
    public NumeroDaEap AdicionarEtapa(string titulo, string dentroDe)
    {
        var (numero, pai) = LocalizarEtapa(dentroDe);

        return numero.Filho(pai.Adicionar(new Etapa(titulo)));
    }

    public Etapa Etapa(string numero) => LocalizarEtapa(numero).Etapa;

    public void RenomearEtapa(string etapa, string titulo) => LocalizarEtapa(etapa).Etapa.Renomear(titulo);

    // ------------------------------------------------------------- itens

    /// <summary>
    /// Acrescenta um item no fim da etapa.
    /// </summary>
    /// <remarks>
    /// O código é conferido no formato, mas não contra a base de custos: o
    /// orçamento não conhece a base (ADR-0005). Código que não existe nela é
    /// acusado no cálculo, junto com todos os outros que faltarem.
    /// </remarks>
    public NumeroDaEap AdicionarItem(string dentroDe, string codigo, decimal quantidade)
    {
        var (numero, pai) = LocalizarEtapa(dentroDe);
        var item = new ItemDoOrcamento(new CodigoDeCusto(codigo), quantidade);

        return numero.Filho(pai.Adicionar(item));
    }

    public ItemDoOrcamento Item(string numero) => LocalizarItem(numero);

    public void AlterarQuantidade(string item, decimal quantidade) => LocalizarItem(item).AlterarQuantidade(quantidade);

    public void VincularAtividade(string item, string atividade) =>
        LocalizarItem(item).VincularAtividade(new CodigoDaAtividade(atividade));

    public void DesvincularAtividade(string item) => LocalizarItem(item).VincularAtividade(null);

    public void DefinirBdiDiferenciado(string item, Bdi bdi)
    {
        ArgumentNullException.ThrowIfNull(bdi);

        LocalizarItem(item).DefinirBdiDiferenciado(bdi);
    }

    public void RemoverBdiDiferenciado(string item) => LocalizarItem(item).DefinirBdiDiferenciado(null);

    // ----------------------------------------------------------- remoção

    /// <summary>
    /// Remove a linha — e, se for etapa, tudo o que está dentro dela. As
    /// linhas seguintes sobem uma posição.
    /// </summary>
    public void Remover(string numero)
    {
        var alvo = NumeroDaEap.Ler(numero);
        Localizar(alvo);

        var posicoes = alvo.Posicoes();

        if (posicoes.Count == 1)
        {
            _etapas.RemoveAt(posicoes[0] - 1);

            return;
        }

        // Toda linha abaixo do primeiro nível está dentro de uma etapa.
        var pai = (Etapa)Localizar(NumeroDaEap.De([.. posicoes.Take(posicoes.Count - 1)]));
        pai.RemoverNaPosicao(posicoes[^1]);
    }

    // ------------------------------------------------------------- apoio

    private (NumeroDaEap Numero, Etapa Etapa) LocalizarEtapa(string numero)
    {
        var chave = NumeroDaEap.Ler(numero);

        return Localizar(chave) is Etapa etapa
            ? (chave, etapa)
            : throw new ExcecaoDeDominio($"A linha {chave} é um item, e não uma etapa.");
    }

    private ItemDoOrcamento LocalizarItem(string numero)
    {
        var chave = NumeroDaEap.Ler(numero);

        return Localizar(chave) as ItemDoOrcamento
            ?? throw new ExcecaoDeDominio($"A linha {chave} é uma etapa, e não um item.");
    }

    private NoDaEap Localizar(NumeroDaEap numero)
    {
        NoDaEap? atual = null;

        foreach (var posicao in numero.Posicoes())
        {
            IReadOnlyList<NoDaEap> irmaos = atual switch
            {
                null => _etapas,
                Etapa etapa => etapa.Filhos,
                _ => throw new ExcecaoDeDominio($"Não existe a linha {numero}: dentro de um item não há outras linhas."),
            };

            if (posicao > irmaos.Count)
            {
                throw new ExcecaoDeDominio($"Não existe a linha {numero} neste orçamento.");
            }

            atual = irmaos[posicao - 1];
        }

        return atual!;
    }

    private static string ValidarNome(string nome)
    {
        var limpo = (nome ?? string.Empty).Trim();

        if (limpo.Length == 0)
        {
            throw new ExcecaoDeDominio("O orçamento precisa de um nome.");
        }

        if (limpo.Length > TamanhoMaximoDoNome)
        {
            throw new ExcecaoDeDominio($"O nome do orçamento tem no máximo {TamanhoMaximoDoNome} caracteres.");
        }

        return limpo;
    }
}
