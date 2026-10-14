using System.Text.Json.Serialization;
using PlanejamentoObras.Api.Http;
using PlanejamentoObras.Dominio.FisicoFinanceiro;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Api.Recursos.Orcamentos;

/// <summary>
/// O BDI, nos dois sentidos. No pedido vem um dos dois: o percentual pronto,
/// ou as parcelas da fórmula do TCU. Na resposta vem sempre o percentual — e
/// as parcelas, se ele foi calculado delas.
/// </summary>
public sealed record BdiDoOrcamento(decimal? Percentual = null, ParcelasDoBdiDoOrcamento? Parcelas = null)
{
    public Bdi ParaBdi() => (Percentual, Parcelas) switch
    {
        ({ } percentual, null) => Bdi.Informado(percentual),
        (null, { } parcelas) => Bdi.PelaFormulaDoTcu(new ParcelasDoBdi(
            parcelas.AdministracaoCentral,
            parcelas.Seguros,
            parcelas.Riscos,
            parcelas.Garantias,
            parcelas.DespesasFinanceiras,
            parcelas.Lucro,
            parcelas.Tributos)),
        _ => throw new PedidoInvalido("O BDI vem informado (percentual) ou pelas parcelas da fórmula do TCU (parcelas) — um dos dois."),
    };

    public static BdiDoOrcamento De(Bdi bdi) =>
        new(
            bdi.Percentual,
            bdi.Parcelas is { } parcelas
                ? new ParcelasDoBdiDoOrcamento(
                    parcelas.AdministracaoCentral,
                    parcelas.Seguros,
                    parcelas.Riscos,
                    parcelas.Garantias,
                    parcelas.DespesasFinanceiras,
                    parcelas.Lucro,
                    parcelas.Tributos)
                : null);
}

/// <summary>As parcelas do BDI pela fórmula do Acórdão 2.622/2013 do TCU, em percentual.</summary>
public sealed record ParcelasDoBdiDoOrcamento(
    decimal AdministracaoCentral,
    decimal Seguros,
    decimal Riscos,
    decimal Garantias,
    decimal DespesasFinanceiras,
    decimal Lucro,
    decimal Tributos);

/// <summary>Um orçamento novo: o nome, o BDI e, se quiser, a EAP inteira de uma vez.</summary>
public sealed record NovoOrcamento(string Nome, BdiDoOrcamento Bdi, IReadOnlyList<NovaEtapa>? Etapas = null);

/// <summary>Um nó da EAP no pedido: etapa ou item, conforme o <c>tipo</c>.</summary>
/// <remarks>
/// Não é abstrato de propósito. Nó sem <c>tipo</c> vira este tipo vazio e é
/// recusado com 400 — pelos campos que ele não tem, ou na montagem da EAP. Se
/// fosse abstrato, o leitor de JSON acusaria "tipo não suportado", e o erro
/// do cliente sairia como erro do servidor.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(NovaEtapa), "etapa")]
[JsonDerivedType(typeof(NovoItemDoOrcamento), "item")]
public record NovoNoDaEap;

/// <summary>Uma etapa e o que vai dentro dela. No primeiro nível só há etapas, e elas vêm sem <c>tipo</c>.</summary>
public sealed record NovaEtapa(string Titulo, IReadOnlyList<NovoNoDaEap>? Filhos = null) : NovoNoDaEap;

/// <summary>Um item: o código na base de custos, a quantidade e, se já se sabe, a atividade e um BDI diferenciado.</summary>
public sealed record NovoItemDoOrcamento(string Codigo, decimal Quantidade, string? Atividade = null, BdiDoOrcamento? BdiDiferenciado = null)
    : NovoNoDaEap;

/// <summary>O que muda no orçamento; o que não vier fica como está.</summary>
public sealed record AlteracaoDoOrcamento(string? Nome = null, BdiDoOrcamento? Bdi = null);

/// <summary>Uma etapa nova, no fim da planilha ou no fim de outra etapa.</summary>
public sealed record EtapaNaEap(string Titulo, string? DentroDe = null);

public sealed record NovoTitulo(string Titulo);

/// <summary>Um item novo, no fim da etapa.</summary>
public sealed record ItemNaEap(string DentroDe, string Codigo, decimal Quantidade, string? Atividade = null, BdiDoOrcamento? BdiDiferenciado = null);

public sealed record NovaQuantidade(decimal Quantidade);

/// <summary>A atividade do cronograma em que o item acontece.</summary>
public sealed record AtividadeDoItem(string Atividade);

public sealed record OrcamentoResposta(Guid Id, string Nome, BdiDoOrcamento Bdi, IReadOnlyList<EtapaResposta> Etapas)
{
    public static OrcamentoResposta De(Orcamento orcamento) =>
        new(
            orcamento.Id.Valor,
            orcamento.Nome,
            BdiDoOrcamento.De(orcamento.Bdi),
            [.. orcamento.Etapas.Select((etapa, indice) => EtapaResposta.De(etapa, $"{indice + 1}"))]);
}

/// <summary>Um nó da EAP na resposta: etapa ou item, com o número que tem na planilha.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(EtapaResposta), "etapa")]
[JsonDerivedType(typeof(ItemDoOrcamentoResposta), "item")]
public abstract record NoDaEapResposta(string Numero);

public sealed record EtapaResposta(string Numero, string Titulo, IReadOnlyList<NoDaEapResposta> Filhos) : NoDaEapResposta(Numero)
{
    public static EtapaResposta De(Etapa etapa, string numero) =>
        new(
            numero,
            etapa.Titulo,
            [.. etapa.Filhos.Select((filho, indice) => filho switch
            {
                Etapa subetapa => De(subetapa, $"{numero}.{indice + 1}"),
                ItemDoOrcamento item => (NoDaEapResposta)new ItemDoOrcamentoResposta(
                    $"{numero}.{indice + 1}",
                    item.Codigo.Valor,
                    item.Quantidade,
                    item.Atividade?.Valor,
                    item.BdiDiferenciado is { } bdi ? BdiDoOrcamento.De(bdi) : null),
                _ => throw new InvalidOperationException($"Nó da EAP desconhecido: {filho.GetType().Name}."),
            })]);
}

public sealed record ItemDoOrcamentoResposta(string Numero, string Codigo, decimal Quantidade, string? Atividade, BdiDoOrcamento? BdiDiferenciado)
    : NoDaEapResposta(Numero);

/// <summary>A planilha: o orçamento calculado contra uma base de custos.</summary>
public sealed record PlanilhaResposta(
    string Orcamento,
    string BaseDeCustos,
    DateOnly DataBase,
    BdiDoOrcamento Bdi,
    decimal CustoDireto,
    decimal Total,
    IReadOnlyList<LinhaDaPlanilhaResposta> Linhas)
{
    public static PlanilhaResposta De(OrcamentoCalculado planilha) =>
        new(
            planilha.Nome,
            planilha.NomeDaBase,
            planilha.DataBase,
            BdiDoOrcamento.De(planilha.Bdi),
            planilha.CustoDireto.Valor,
            planilha.Total.Valor,
            [.. planilha.Linhas.Select(linha => linha switch
            {
                LinhaDeItem item => (LinhaDaPlanilhaResposta)new ItemDaPlanilhaResposta(
                    item.Numero.Valor,
                    item.Descricao,
                    item.CustoTotal.Valor,
                    item.Total.Valor,
                    item.Peso,
                    item.Codigo.Valor,
                    item.Unidade.Valor,
                    item.Quantidade,
                    item.CustoUnitario.Valor,
                    item.Bdi.Percentual,
                    item.PrecoUnitario.Valor,
                    item.Atividade?.Valor),
                _ => new EtapaDaPlanilhaResposta(linha.Numero.Valor, linha.Descricao, linha.CustoTotal.Valor, linha.Total.Valor, linha.Peso),
            })]);
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(EtapaDaPlanilhaResposta), "etapa")]
[JsonDerivedType(typeof(ItemDaPlanilhaResposta), "item")]
public abstract record LinhaDaPlanilhaResposta(string Numero, string Descricao, decimal CustoTotal, decimal Total, decimal Peso);

public sealed record EtapaDaPlanilhaResposta(string Numero, string Descricao, decimal CustoTotal, decimal Total, decimal Peso)
    : LinhaDaPlanilhaResposta(Numero, Descricao, CustoTotal, Total, Peso);

public sealed record ItemDaPlanilhaResposta(
    string Numero,
    string Descricao,
    decimal CustoTotal,
    decimal Total,
    decimal Peso,
    string Codigo,
    string Unidade,
    decimal Quantidade,
    decimal CustoUnitario,
    decimal Bdi,
    decimal PrecoUnitario,
    string? Atividade)
    : LinhaDaPlanilhaResposta(Numero, Descricao, CustoTotal, Total, Peso);

public sealed record ItemDaCurvaAbcResposta(string Numero, string Descricao, decimal Total, decimal Peso, decimal PesoAcumulado, ClasseAbc Classe)
{
    public static ItemDaCurvaAbcResposta De(ItemDaCurvaAbc item) =>
        new(item.Numero.Valor, item.Descricao, item.Total.Valor, item.Peso, item.PesoAcumulado, item.Classe);
}

/// <summary>O orçamento distribuído nos meses em que cada serviço acontece.</summary>
public sealed record FisicoFinanceiroResposta(
    DatasDoCronograma Datas,
    decimal Total,
    IReadOnlyList<PeriodoResposta> Periodos,
    IReadOnlyList<LinhaDoFisicoFinanceiroResposta> Linhas)
{
    public static FisicoFinanceiroResposta De(CronogramaFisicoFinanceiro fisicoFinanceiro) =>
        new(
            fisicoFinanceiro.Datas,
            fisicoFinanceiro.Total.Valor,
            [.. fisicoFinanceiro.Periodos.Select(periodo => new PeriodoResposta(
                periodo.Numero,
                periodo.Mes,
                periodo.Valor.Valor,
                periodo.Percentual,
                periodo.Acumulado.Valor,
                periodo.PercentualAcumulado))],
            [.. fisicoFinanceiro.Linhas.Select(linha => new LinhaDoFisicoFinanceiroResposta(
                linha.Numero.Valor,
                linha.Descricao,
                linha.Total.Valor,
                [.. linha.Valores.Select(valor => valor.Valor)],
                linha.CustoTotal.Valor,
                [.. linha.Custos.Select(custo => custo.Valor)]))]);
}

public sealed record PeriodoResposta(int Numero, DateOnly Mes, decimal Valor, decimal Percentual, decimal Acumulado, decimal PercentualAcumulado);

/// <summary>Uma linha da planilha nos meses: os valores e os custos, um por período, na ordem dos períodos.</summary>
public sealed record LinhaDoFisicoFinanceiroResposta(
    string Numero,
    string Descricao,
    decimal Total,
    IReadOnlyList<decimal> Valores,
    decimal CustoTotal,
    IReadOnlyList<decimal> Custos);
