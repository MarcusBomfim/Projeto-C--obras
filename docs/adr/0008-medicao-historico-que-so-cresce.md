# ADR-0008 — A medição é um histórico que só cresce, e o valor do mês é a diferença entre acumulados

**Status:** aceita

## Contexto

A medição é como a obra avança no papel: todo mês, a fiscalização confere o que foi executado de cada item, o boletim de medição transforma isso em valor a preço de contrato, e o valor do mês é o que se paga. Junto com a quantidade vem o custo real — o que a construtora gastou em cada item —, que o valor agregado compara com o custo orçado do que foi executado.

Três problemas aparecem em qualquer controle de medição:

1. **Medição antiga corrigida em silêncio.** O boletim de março foi assinado e pago; se alguém edita a quantidade de março em maio, o histórico deixa de bater com o que foi pago.
2. **Medir além do contratado.** Uma quantidade acumulada maior que a do contrato é um aditivo disfarçado.
3. **Centavos que aparecem do nada.** Calcular o valor de cada mês como quantidade do mês × preço unitário e somar os meses não fecha com o total do item: 0,5 + 0,5 de um item de R$ 0,15 dá 0,08 + 0,08 = 0,16.

## Decisão

**1. O histórico só cresce.** `Acompanhamento.RegistrarMedicao` acrescenta a medição do mês; nenhuma medição registrada é editada. Erro numa medição antiga se corrige com **glosa**: quantidade negativa na medição seguinte, como se faz no boletim. A única exceção é a última medição, que pode ser desfeita — para corrigir um lançamento antes de a próxima existir.

**2. Uma medição por mês, em ordem.** Qualquer dia vale pelo mês inteiro (31/10 é a medição de outubro), e cada medição vem depois da anterior. Pode haver mês sem medição; mês parado, sem nada executado, tem medição sem lançamento — e conta.

**3. A quantidade acumulada fica entre zero e a contratada.** Passar da contratada exige aditivo — e a mensagem diz isso. A glosa não pode deixar a quantidade medida negativa.

**4. O lançamento é por item da linha de base**, endereçado pelo número dela — que, ao contrário do orçamento vivo, não renumera (ADR-0007). Medição de etapa não existe: etapa é soma.

**5. O valor do mês é a diferença entre acumulados.** O valor acumulado de um item é quantidade acumulada × preço unitário, arredondado ao centavo; o do mês é o acumulado menos o anterior. Assim, o item todo medido vale exatamente o contratado, e nenhum centavo aparece ou some ao longo dos meses — a mesma ideia do rateio pelo maior resto (ADR-0006), aplicada ao tempo.

**6. Os problemas de uma medição vêm todos juntos**, e nada é registrado se houver algum: item que não existe, etapa no lugar de item, item repetido, casas decimais a mais, custo negativo, quantidade além do contrato.

## Consequências

- **O boletim emitido hoje para março é igual ao emitido em março.** É o que permite tratar o histórico como documento.
- **O agregado guarda só o lançado**: quantidade e custo de cada item em cada mês. Acumulados, valores e percentuais são derivados — o boletim é calculado por `EmissaoDoBoletim` quando pedido.
- **Glosa vira valor negativo no mês**, como no boletim de papel: R$ 1.000,00 a menos no mês da correção, e o acumulado certo.
- **Reajuste não entra aqui.** O valor medido é a preço da linha de base; reajuste por índice é um cálculo financeiro sobre ele, que não muda o avanço físico — e é o avanço físico que o valor agregado mede.
- **Desfazer só a última medição limita a correção de erros antigos à glosa.** É a regra de propósito: a alternativa — editar o passado — é exatamente o problema 1.
