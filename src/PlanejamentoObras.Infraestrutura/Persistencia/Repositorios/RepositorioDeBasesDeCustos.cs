using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;

namespace PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

/// <summary>Grava e lê bases de custos, com concorrência otimista (ADR-0012).</summary>
public sealed class RepositorioDeBasesDeCustos(ContextoDoBanco contexto, TimeProvider relogio)
{
    private readonly TabelaDeAgregados<RegistroDeBaseDeCustos> _tabela = new(contexto, relogio, "a base de custos");

    public async Task<Versionado<BaseDeCustos>?> Obter(BaseDeCustosId id, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var registro = await _tabela.Ler(id.Valor, cancelamento);

        return registro is null ? null : new(_tabela.Abrir(registro, lido => lido.ParaBaseDeCustos()), registro.Versao);
    }

    public Task<int> Adicionar(BaseDeCustos baseDeCustos, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(baseDeCustos);

        return _tabela.Inserir(RegistroDeBaseDeCustos.De(baseDeCustos, versao: 1, _tabela.Agora), cancelamento);
    }

    public Task<int> Atualizar(BaseDeCustos baseDeCustos, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(baseDeCustos);

        var novo = RegistroDeBaseDeCustos.De(baseDeCustos, versaoLida + 1, _tabela.Agora);

        return _tabela.Atualizar(
            novo,
            versaoLida,
            colunas => colunas
                .SetProperty(registro => registro.Nome, novo.Nome)
                .SetProperty(registro => registro.DataBase, novo.DataBase),
            cancelamento);
    }

    public Task Remover(BaseDeCustosId id, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Apagar(id.Valor, versaoLida, cancelamento);
    }

    public Task<IReadOnlyList<ResumoDoAgregado<BaseDeCustosId>>> Listar(CancellationToken cancelamento = default) =>
        contexto.BasesDeCustos.Listar(id => new BaseDeCustosId(id), cancelamento);
}
