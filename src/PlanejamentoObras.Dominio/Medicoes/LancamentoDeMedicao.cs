namespace PlanejamentoObras.Dominio.Medicoes;

/// <summary>
/// O que se lança na medição de um item: quanto se executou no mês e quanto
/// custou.
/// </summary>
/// <param name="Item">O número do item na linha de base: "2.1".</param>
/// <param name="Quantidade">
/// Executada no mês, na unidade do item, com até duas casas. Negativa é glosa:
/// corrige o que foi medido a mais antes.
/// </param>
/// <param name="CustoReal">O que o item custou no mês — a apropriação de custos —, em reais.</param>
public sealed record LancamentoDeMedicao(string Item, decimal Quantidade, decimal CustoReal = 0m);
