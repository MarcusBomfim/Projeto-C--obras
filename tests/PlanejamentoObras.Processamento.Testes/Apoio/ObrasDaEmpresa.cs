using Microsoft.EntityFrameworkCore;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Testes.FisicoFinanceiro;
using PlanejamentoObras.Infraestrutura.Persistencia.Painel;
using PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;
using PlanejamentoObras.Infraestrutura.Testes.Persistencia;

namespace PlanejamentoObras.Processamento.Testes.Apoio;

/// <summary>
/// As obras de uma empresa, gravadas como a API as grava: pelos
/// repositórios, com o papel da aplicação — e, com elas, as mensagens na
/// caixa de saída.
/// </summary>
public sealed class ObrasDaEmpresa(BancoDeTestes banco, Guid empresa, TimeProvider relogio)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private RepositorioDeAcompanhamentos Acompanhamentos() => new(banco.NovoContexto(empresa), relogio);

    /// <summary>A casa térrea, com a linha de base dela; <paramref name="medir"/> mede antes de gravar.</summary>
    public async Task<Acompanhamento> Gravar(Action<Acompanhamento>? medir = null)
    {
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();
        var acompanhamento = new Acompanhamento(linhaDeBase);
        medir?.Invoke(acompanhamento);

        await new RepositorioDeLinhasDeBase(banco.NovoContexto(empresa), relogio).Adicionar(linhaDeBase, Cancelamento);
        await Acompanhamentos().Adicionar(acompanhamento, Cancelamento);

        return acompanhamento;
    }

    /// <summary>Grava uma obra numa transação que é desfeita no fim: nada fica, nem a mensagem.</summary>
    public async Task GravarEDesfazer()
    {
        var linhaDeBase = CasaTerrea.LinhaDeBaseAprovada();
        await using var contexto = banco.NovoContexto(empresa);
        await using var transacao = await contexto.Database.BeginTransactionAsync(Cancelamento);

        await new RepositorioDeLinhasDeBase(contexto, relogio).Adicionar(linhaDeBase, Cancelamento);
        await new RepositorioDeAcompanhamentos(contexto, relogio).Adicionar(new Acompanhamento(linhaDeBase), Cancelamento);

        await transacao.RollbackAsync(Cancelamento);
    }

    public Task Medir(AcompanhamentoId acompanhamento, DateOnly mes, params LancamentoDeMedicao[] lancamentos) =>
        Alterar(acompanhamento, lido => lido.RegistrarMedicao(mes, lancamentos));

    public Task DesfazerUltimaMedicao(AcompanhamentoId acompanhamento) =>
        Alterar(acompanhamento, lido => lido.DesfazerUltimaMedicao());

    public async Task Remover(AcompanhamentoId acompanhamento)
    {
        var lido = (await Acompanhamentos().Obter(acompanhamento, Cancelamento))!;

        await Acompanhamentos().Remover(acompanhamento, lido.Versao, Cancelamento);
    }

    /// <summary>A linha da obra no painel, como a empresa a lê.</summary>
    public Task<PainelDaObra?> Painel(AcompanhamentoId acompanhamento) =>
        new ConsultaDoPainel(banco.NovoContexto(empresa)).Obter(acompanhamento.Valor, Cancelamento);

    private async Task Alterar(AcompanhamentoId acompanhamento, Action<Acompanhamento> alteracao)
    {
        var lido = (await Acompanhamentos().Obter(acompanhamento, Cancelamento))!;
        alteracao(lido.Agregado);

        await Acompanhamentos().Atualizar(lido.Agregado, lido.Versao, Cancelamento);
    }
}
