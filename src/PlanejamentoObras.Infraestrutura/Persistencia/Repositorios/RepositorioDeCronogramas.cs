using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;

namespace PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

/// <summary>Grava e lê cronogramas, com concorrência otimista (ADR-0012).</summary>
public sealed class RepositorioDeCronogramas(ContextoDoBanco contexto, TimeProvider relogio)
{
    private readonly TabelaDeAgregados<RegistroDeCronograma> _tabela = new(contexto, relogio, "o cronograma");

    public async Task<Versionado<Cronograma>?> Obter(CronogramaId id, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var registro = await _tabela.Ler(id.Valor, cancelamento);

        return registro is null ? null : new(_tabela.Abrir(registro, lido => lido.ParaCronograma()), registro.Versao);
    }

    /// <returns>A versão gravada: 1.</returns>
    public Task<int> Adicionar(Cronograma cronograma, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(cronograma);

        return _tabela.Inserir(RegistroDeCronograma.De(cronograma, versao: 1, _tabela.Agora), cancelamento);
    }

    /// <param name="versaoLida">A versão em que o cronograma foi lido.</param>
    /// <returns>A versão nova.</returns>
    public Task<int> Atualizar(Cronograma cronograma, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(cronograma);

        var novo = RegistroDeCronograma.De(cronograma, versaoLida + 1, _tabela.Agora);

        return _tabela.Atualizar(novo, versaoLida, colunas => colunas.SetProperty(registro => registro.Nome, novo.Nome), cancelamento);
    }

    public Task Remover(CronogramaId id, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Apagar(id.Valor, versaoLida, cancelamento);
    }

    public Task<IReadOnlyList<ResumoDoAgregado<CronogramaId>>> Listar(CancellationToken cancelamento = default) =>
        contexto.Cronogramas.Listar(id => new CronogramaId(id), cancelamento);
}
