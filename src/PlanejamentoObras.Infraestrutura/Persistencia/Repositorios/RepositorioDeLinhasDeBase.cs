using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;

namespace PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

/// <summary>
/// Grava e lê linhas de base. Não há <c>Atualizar</c>: a linha de base não
/// muda depois de aprovada (ADR-0007) — plano novo é linha de base nova.
/// </summary>
public sealed class RepositorioDeLinhasDeBase(ContextoDoBanco contexto, TimeProvider relogio)
{
    private readonly TabelaDeAgregados<RegistroDeLinhaDeBase> _tabela = new(contexto, relogio, "a linha de base");

    public async Task<Versionado<LinhaDeBase>?> Obter(LinhaDeBaseId id, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var registro = await _tabela.Ler(id.Valor, cancelamento);

        return registro is null ? null : new(_tabela.Abrir(registro, lido => lido.ParaLinhaDeBase()), registro.Versao);
    }

    public Task<int> Adicionar(LinhaDeBase linhaDeBase, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(linhaDeBase);

        return _tabela.Inserir(RegistroDeLinhaDeBase.De(linhaDeBase, versao: 1, _tabela.Agora), cancelamento);
    }

    /// <remarks>Recusado enquanto houver acompanhamento medido contra ela — o banco garante.</remarks>
    public Task Remover(LinhaDeBaseId id, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Apagar(
            id.Valor,
            versaoLida,
            cancelamento,
            emUso: $"Há acompanhamento medido contra a linha de base {id}: ela só sai do banco depois dele.");
    }

    public Task<IReadOnlyList<ResumoDoAgregado<LinhaDeBaseId>>> Listar(CancellationToken cancelamento = default) =>
        contexto.LinhasDeBase.Listar(id => new LinhaDeBaseId(id), cancelamento);
}
