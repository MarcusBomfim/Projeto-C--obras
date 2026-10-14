using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;

namespace PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

/// <summary>Grava e lê orçamentos, com concorrência otimista (ADR-0012).</summary>
public sealed class RepositorioDeOrcamentos(ContextoDoBanco contexto, TimeProvider relogio)
{
    private readonly TabelaDeAgregados<RegistroDeOrcamento> _tabela = new(contexto, relogio, "o orçamento");

    public async Task<Versionado<Orcamento>?> Obter(OrcamentoId id, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var registro = await _tabela.Ler(id.Valor, cancelamento);

        return registro is null ? null : new(_tabela.Abrir(registro, lido => lido.ParaOrcamento()), registro.Versao);
    }

    public Task<int> Adicionar(Orcamento orcamento, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(orcamento);

        return _tabela.Inserir(RegistroDeOrcamento.De(orcamento, versao: 1, _tabela.Agora), cancelamento);
    }

    public Task<int> Atualizar(Orcamento orcamento, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(orcamento);

        var novo = RegistroDeOrcamento.De(orcamento, versaoLida + 1, _tabela.Agora);

        return _tabela.Atualizar(novo, versaoLida, colunas => colunas.SetProperty(registro => registro.Nome, novo.Nome), cancelamento);
    }

    public Task Remover(OrcamentoId id, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Apagar(id.Valor, versaoLida, cancelamento);
    }

    public Task<IReadOnlyList<ResumoDoAgregado<OrcamentoId>>> Listar(CancellationToken cancelamento = default) =>
        contexto.Orcamentos.Listar(id => new OrcamentoId(id), cancelamento);
}
