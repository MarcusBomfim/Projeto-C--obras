using Microsoft.EntityFrameworkCore;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;

namespace PlanejamentoObras.Infraestrutura.Persistencia.Repositorios;

/// <summary>
/// Grava e lê acompanhamentos. A linha de base é outro agregado: o
/// acompanhamento guarda o id dela, e a leitura traz as duas.
/// </summary>
public sealed class RepositorioDeAcompanhamentos(ContextoDoBanco contexto, TimeProvider relogio)
{
    private readonly TabelaDeAgregados<RegistroDeAcompanhamento> _tabela = new(contexto, relogio, "o acompanhamento");

    private readonly TabelaDeAgregados<RegistroDeLinhaDeBase> _linhasDeBase = new(contexto, relogio, "a linha de base");

    public async Task<Versionado<Acompanhamento>?> Obter(AcompanhamentoId id, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var registro = await _tabela.Ler(id.Valor, cancelamento);

        if (registro is null)
        {
            return null;
        }

        // A chave estrangeira garante que a linha de base existe.
        var linhaDeBase = await _linhasDeBase.Ler(registro.LinhaDeBaseId, cancelamento)
            ?? throw new DocumentoInvalido($"O acompanhamento {id} aponta para a linha de base {registro.LinhaDeBaseId}, que não está gravada.");

        var lida = _linhasDeBase.Abrir(linhaDeBase, lido => lido.ParaLinhaDeBase());

        return new(_tabela.Abrir(registro, lido => lido.ParaAcompanhamento(lida)), registro.Versao);
    }

    /// <remarks>
    /// A linha de base precisa estar gravada antes. Os fatos do acompanhamento
    /// vão para a caixa de saída junto com ele, e saem dele depois de gravados
    /// (ADR-0017).
    /// </remarks>
    public async Task<int> Adicionar(Acompanhamento acompanhamento, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(acompanhamento);

        var versao = await _tabela.Inserir(
            RegistroDeAcompanhamento.De(acompanhamento, versao: 1, _tabela.Agora),
            cancelamento,
            semReferencia: $"A linha de base {acompanhamento.LinhaDeBase.Id} não está gravada: grave-a antes do acompanhamento.",
            eventos: acompanhamento.Eventos);

        acompanhamento.DescartarEventos();

        return versao;
    }

    public async Task<int> Atualizar(Acompanhamento acompanhamento, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(acompanhamento);

        // A linha de base de um acompanhamento não muda; só o documento.
        var novo = RegistroDeAcompanhamento.De(acompanhamento, versaoLida + 1, _tabela.Agora);

        var versao = await _tabela.Atualizar(novo, versaoLida, outrasColunas: null, cancelamento, acompanhamento.Eventos);

        acompanhamento.DescartarEventos();

        return versao;
    }

    public Task Remover(AcompanhamentoId id, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Apagar(id.Valor, versaoLida, cancelamento);
    }

    /// <summary>Os acompanhamentos medidos contra uma linha de base, com a versão de cada um.</summary>
    public async Task<IReadOnlyList<Versionado<AcompanhamentoId>>> ListarDaLinhaDeBase(
        LinhaDeBaseId linhaDeBase,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(linhaDeBase);

        var linhas = await contexto.Acompanhamentos
            .AsNoTracking()
            .Where(registro => registro.LinhaDeBaseId == linhaDeBase.Valor)
            .OrderBy(registro => registro.CriadoEm)
            .ThenBy(registro => registro.Id)
            .Select(registro => new { registro.Id, registro.Versao })
            .ToListAsync(cancelamento);

        return [.. linhas.Select(linha => new Versionado<AcompanhamentoId>(new AcompanhamentoId(linha.Id), linha.Versao))];
    }
}
