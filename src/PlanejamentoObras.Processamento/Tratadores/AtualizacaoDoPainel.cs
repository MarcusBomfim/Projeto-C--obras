using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Infraestrutura.Mensagens;
using PlanejamentoObras.Infraestrutura.Persistencia.Painel;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

namespace PlanejamentoObras.Processamento.Tratadores;

/// <summary>
/// Mantém o painel das obras: a cada fato de um acompanhamento, refaz a linha
/// dele a partir do estado atual.
/// </summary>
/// <remarks>
/// A mensagem só diz qual acompanhamento mudou; o que mostrar vem do
/// acompanhamento como está agora. Por isso tratar a mesma mensagem duas
/// vezes, ou uma atrasada depois da seguinte, dá a mesma linha (ADR-0017).
/// </remarks>
public sealed class AtualizacaoDoPainel(RepositorioDeAcompanhamentos acompanhamentos, ProjecaoDoPainel painel)
    : ITratadorDe<MensagemDeAcompanhamentoIniciado>,
      ITratadorDe<MensagemDeMedicaoRegistrada>,
      ITratadorDe<MensagemDeMedicaoDesfeita>
{
    public Task Tratar(MensagemDeAcompanhamentoIniciado mensagem, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        return Refazer(mensagem.Acompanhamento, cancelamento);
    }

    public Task Tratar(MensagemDeMedicaoRegistrada mensagem, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        return Refazer(mensagem.Acompanhamento, cancelamento);
    }

    public Task Tratar(MensagemDeMedicaoDesfeita mensagem, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        return Refazer(mensagem.Acompanhamento, cancelamento);
    }

    private async Task Refazer(Guid acompanhamento, CancellationToken cancelamento)
    {
        // Removido depois do fato: não há o que mostrar — e a linha dele, se
        // havia, saiu junto, pela chave estrangeira.
        if (await acompanhamentos.Obter(new AcompanhamentoId(acompanhamento), cancelamento) is { } lido)
        {
            await painel.Atualizar(lido, cancelamento);
        }
    }
}
