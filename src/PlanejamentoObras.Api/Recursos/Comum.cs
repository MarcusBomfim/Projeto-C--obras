using PlanejamentoObras.Api.Http;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Infraestrutura.Persistencia;

namespace PlanejamentoObras.Api.Recursos;

/// <summary>O que se lista de um agregado: o id, o nome, a versão e quando foi gravado pela última vez.</summary>
public sealed record ResumoResposta(Guid Id, string Nome, int Versao, DateTimeOffset AtualizadoEm)
{
    public static ResumoResposta De<TId>(ResumoDoAgregado<TId> resumo)
        where TId : Identificador
    {
        ArgumentNullException.ThrowIfNull(resumo);

        return new(resumo.Id.Valor, resumo.Nome, resumo.Versao, resumo.AtualizadoEm);
    }
}

/// <summary>Um nome novo para o agregado.</summary>
public sealed record NovoNome(string Nome);

/// <summary>O que os endpoints fazem com um agregado lido antes de alterá-lo.</summary>
internal static class Lidos
{
    /// <summary>
    /// A alteração parte da versão que o cliente leu. Se o banco já está em
    /// outra, ela nem é aplicada — 412, com a versão atual. O banco confere de
    /// novo ao gravar, para quem gravar no meio do caminho (ADR-0012).
    /// </summary>
    /// <param name="agregado">Como o agregado aparece na mensagem: "O cronograma 0199…".</param>
    public static void ConferirVersao<T>(Versionado<T> lido, VersaoLida versao, string agregado)
    {
        ArgumentNullException.ThrowIfNull(lido);
        ArgumentNullException.ThrowIfNull(versao);

        if (lido.Versao != versao.Valor)
        {
            throw new ConflitoDeConcorrencia(
                $"{agregado} mudou desde a versão {versao.Valor}: está na versão {lido.Versao}. Leia de novo e refaça a alteração.",
                lido.Versao);
        }
    }
}
