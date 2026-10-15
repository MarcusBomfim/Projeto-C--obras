using Microsoft.EntityFrameworkCore;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.ValorAgregado;

namespace PlanejamentoObras.Infraestrutura.Persistencia.Painel;

/// <summary>
/// Grava o painel das obras: a linha de um acompanhamento, refeita a partir
/// do estado dele (ADR-0017).
/// </summary>
/// <remarks>
/// <para>
/// Refazer do estado, e não aplicar o fato à linha anterior, é o que deixa o
/// tratamento idempotente e indiferente à ordem: a mesma mensagem tratada
/// duas vezes, ou uma mensagem atrasada tratada depois da seguinte, dão a
/// mesma linha.
/// </para>
/// <para>
/// A gravação é um comando só — <c>insert … on conflict do update … where</c>
/// —, que só troca a linha por uma feita de versão igual ou mais nova do
/// acompanhamento. Dois processadores com versões diferentes nunca fazem o
/// painel voltar atrás.
/// </para>
/// </remarks>
public sealed class ProjecaoDoPainel(ContextoDoBanco contexto, TimeProvider relogio)
{
    public async Task Atualizar(Versionado<Acompanhamento> lido, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(lido);

        var acompanhamento = lido.Agregado;
        var linhaDeBase = acompanhamento.LinhaDeBase;
        var analise = acompanhamento.Medicoes.Count > 0 ? CalculadoraDeValorAgregado.Calcular(acompanhamento) : null;
        var obra = analise?.Obra;
        var prazo = analise?.Prazo;
        var situacao = obra is null ? SituacaoDaObra.SemMedicao : ClassificacaoDaObra.Classificar(obra);

        await contexto.Database.ExecuteSqlAsync(
            $"""
            insert into planejamento.painel_das_obras (
                acompanhamento_id, empresa_id, linha_de_base_id, obra, situacao, medicao, mes,
                orcamento_no_termino, valor_planejado, valor_agregado, custo_real,
                indice_de_desempenho_de_prazo, indice_de_desempenho_de_custo,
                estimativa_no_termino, variacao_no_termino,
                duracao_planejada_em_meses, prazo_agregado_em_meses, duracao_estimada_em_meses,
                versao_do_acompanhamento, atualizado_em)
            values (
                {acompanhamento.Id.Valor}, {contexto.EmpresaQueGrava}, {linhaDeBase.Id.Valor}, {linhaDeBase.Nome}, {situacao.ToString()},
                {analise?.Medicao}::integer, {analise?.Mes}::date,
                {linhaDeBase.CustoTotal.Valor}, {obra?.ValorPlanejado.Valor}::numeric, {obra?.ValorAgregado.Valor}::numeric, {obra?.CustoReal.Valor}::numeric,
                {obra?.IndiceDeDesempenhoDePrazo}::numeric, {obra?.IndiceDeDesempenhoDeCusto}::numeric,
                {obra?.EstimativaNoTermino?.Valor}::numeric, {obra?.VariacaoNoTermino?.Valor}::numeric,
                {linhaDeBase.Meses.Count}, {prazo?.PrazoAgregadoEmMeses}::numeric, {prazo?.DuracaoEstimadaEmMeses}::numeric,
                {lido.Versao}, {relogio.GetUtcNow()})
            on conflict (empresa_id, acompanhamento_id) do update set
                obra = excluded.obra,
                situacao = excluded.situacao,
                medicao = excluded.medicao,
                mes = excluded.mes,
                orcamento_no_termino = excluded.orcamento_no_termino,
                valor_planejado = excluded.valor_planejado,
                valor_agregado = excluded.valor_agregado,
                custo_real = excluded.custo_real,
                indice_de_desempenho_de_prazo = excluded.indice_de_desempenho_de_prazo,
                indice_de_desempenho_de_custo = excluded.indice_de_desempenho_de_custo,
                estimativa_no_termino = excluded.estimativa_no_termino,
                variacao_no_termino = excluded.variacao_no_termino,
                duracao_planejada_em_meses = excluded.duracao_planejada_em_meses,
                prazo_agregado_em_meses = excluded.prazo_agregado_em_meses,
                duracao_estimada_em_meses = excluded.duracao_estimada_em_meses,
                versao_do_acompanhamento = excluded.versao_do_acompanhamento,
                atualizado_em = excluded.atualizado_em
            where painel_das_obras.versao_do_acompanhamento <= excluded.versao_do_acompanhamento
            """,
            cancelamento);
    }
}
