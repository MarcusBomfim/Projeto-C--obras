using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Orcamentos;

/// <summary>
/// Um grupo da EAP: "Infraestrutura", "Vedações", "Revestimentos". Contém
/// itens, outras etapas, ou os dois.
/// </summary>
public sealed class Etapa : NoDaEap
{
    public const int TamanhoMaximoDoTitulo = 200;

    private readonly List<NoDaEap> _filhos = [];

    internal Etapa(string titulo)
    {
        Titulo = ValidarTitulo(titulo);
    }

    public string Titulo { get; private set; }

    /// <summary>O que está dentro da etapa, na ordem da planilha.</summary>
    public IReadOnlyList<NoDaEap> Filhos => _filhos.AsReadOnly();

    internal void Renomear(string titulo) => Titulo = ValidarTitulo(titulo);

    /// <summary>Acrescenta no fim e devolve a posição, a partir de 1.</summary>
    internal int Adicionar(NoDaEap filho)
    {
        _filhos.Add(filho);

        return _filhos.Count;
    }

    internal void RemoverNaPosicao(int posicao) => _filhos.RemoveAt(posicao - 1);

    private static string ValidarTitulo(string titulo)
    {
        var limpo = (titulo ?? string.Empty).Trim();

        if (limpo.Length == 0)
        {
            throw new ExcecaoDeDominio("A etapa precisa de um título.");
        }

        if (limpo.Length > TamanhoMaximoDoTitulo)
        {
            throw new ExcecaoDeDominio($"O título da etapa tem no máximo {TamanhoMaximoDoTitulo} caracteres.");
        }

        return limpo;
    }
}
