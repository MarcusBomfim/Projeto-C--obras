using PlanejamentoObras.Dominio.Comum;

namespace PlanejamentoObras.Dominio.Cronogramas;

/// <summary>
/// Um serviço da obra com duração: fundação, estrutura, alvenaria, pintura.
/// </summary>
/// <remarks>
/// <para>
/// A atividade só existe dentro de um <see cref="Cronograma"/>, e só muda por
/// ele. Os métodos que alteram estado são <c>internal</c>: de fora do domínio
/// não há como mexer numa atividade sem passar pela raiz do agregado — que é
/// quem sabe se a mudança quebra alguma regra da rede.
/// </para>
/// <para>
/// A duração é em dias úteis, nunca em dias corridos: é o que o engenheiro
/// estima — "a alvenaria leva 15 dias de trabalho" — e o que não muda quando
/// o calendário muda. Duração zero faz da atividade um marco.
/// </para>
/// </remarks>
public sealed class Atividade
{
    /// <summary>Cerca de vinte anos de dias úteis. Acima disso é erro de digitação, não obra.</summary>
    public const int DuracaoMaximaEmDiasUteis = 5000;

    public const int TamanhoMaximoDaDescricao = 200;

    internal Atividade(CodigoDaAtividade codigo, string descricao, int duracaoEmDiasUteis)
    {
        Codigo = codigo;
        Descricao = ValidarDescricao(codigo, descricao);
        DuracaoEmDiasUteis = ValidarDuracao(codigo, duracaoEmDiasUteis);
    }

    public CodigoDaAtividade Codigo { get; }

    public string Descricao { get; private set; }

    public int DuracaoEmDiasUteis { get; private set; }

    /// <summary>
    /// Marco: atividade de duração zero. Não consome tempo, marca um instante —
    /// a conclusão da estrutura, a entrega das chaves.
    /// </summary>
    public bool EhMarco => DuracaoEmDiasUteis == 0;

    internal void AlterarDuracao(int novaDuracao) =>
        DuracaoEmDiasUteis = ValidarDuracao(Codigo, novaDuracao);

    internal void AlterarDescricao(string novaDescricao) =>
        Descricao = ValidarDescricao(Codigo, novaDescricao);

    private static string ValidarDescricao(CodigoDaAtividade codigo, string descricao)
    {
        var limpa = (descricao ?? string.Empty).Trim();

        if (limpa.Length == 0)
        {
            throw new ExcecaoDeDominio($"A atividade {codigo} precisa de uma descrição.");
        }

        if (limpa.Length > TamanhoMaximoDaDescricao)
        {
            throw new ExcecaoDeDominio(
                $"A descrição da atividade {codigo} tem no máximo {TamanhoMaximoDaDescricao} caracteres.");
        }

        return limpa;
    }

    private static int ValidarDuracao(CodigoDaAtividade codigo, int duracao)
    {
        if (duracao < 0)
        {
            throw new ExcecaoDeDominio(
                $"A duração da atividade {codigo} não pode ser negativa. Para um marco, use duração zero.");
        }

        if (duracao > DuracaoMaximaEmDiasUteis)
        {
            throw new ExcecaoDeDominio(
                $"A duração da atividade {codigo} passa de {DuracaoMaximaEmDiasUteis} dias úteis. Confira o valor.");
        }

        return duracao;
    }
}
