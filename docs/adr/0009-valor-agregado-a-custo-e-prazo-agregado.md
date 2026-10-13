# ADR-0009 — Valor agregado a custo, estimativas a partir dos valores, e prazo agregado em meses

**Status:** aceita

## Contexto

O gerenciamento do valor agregado (EVM) responde, numa data de status, se a obra está adiantada ou atrasada e se está gastando mais ou menos que o orçado — e projeta onde vai terminar. Três números sustentam tudo:

- **VP**, valor planejado: quanto se planejou executar até a data;
- **VA**, valor agregado: quanto se executou, avaliado pelo orçamento;
- **CR**, custo real: quanto custou o que se executou.

Daí saem as variações (VPR = VA − VP, VC = VA − CR), os índices (IDP = VA / VP, IDC = VA / CR) e as estimativas no término.

Quatro decisões não são óbvias.

## Decisão

**1. Tudo a custo, e não a preço.** A primeira versão media o valor agregado a preço de contrato — o valor medido no boletim — e comparava com o custo real. Está errado: o preço inclui o BDI, que não é custo da obra (administração central, risco, tributos, lucro). Com BDI de 22,88 %, uma obra exatamente no orçado mostraria IDC de 1,23; uma obra 15 % acima do custo ainda pareceria abaixo dele.

O EVM mede "o custo orçado do trabalho executado". Então VP, VA e o orçamento no término vêm do **custo direto** da linha de base: VA de um item é quantidade medida × custo unitário, ao centavo; VP é a soma dos custos planejados dos meses até a data de status. O boletim continua a preço — é o que se paga. As duas medidas convivem, cada uma para o seu uso.

**2. Índices não arredondam; estimativas saem dos valores.** IDP e IDC são razões guardadas inteiras; quem mostra arredonda. Já as estimativas em dinheiro seguem a ADR-0004: saem dos valores mostrados, não dos índices arredondados. A estimativa no término pelo desempenho de custo é ONT / IDC, calculada como **ONT × CR / VA** — que se refaz à mão com os três valores da tela. Pelo índice arredondado (0,85), a conta daria outro centavo.

São três estimativas no término, porque são três hipóteses diferentes sobre o futuro:

| Estimativa | Fórmula | Hipótese |
|---|---|---|
| pelo desempenho de custo | ONT × CR / VA | o desvio de custo continua |
| com o restante no orçamento | CR + (ONT − VA) | o desvio não se repete |
| com custo e prazo | CR + (ONT − VA) × CR × VP / VA² | custo e atraso pesam juntos no que falta |

**3. Prazo agregado (earned schedule), além do IDP.** O IDP clássico tem um defeito conhecido: no fim da obra, VA e VP chegam os dois ao orçamento, e o IDP volta a 1 — mesmo que a obra tenha terminado meses atrasada. O prazo agregado mede o atraso em tempo: em que mês do plano a obra estaria, se tivesse executado o que executou no ritmo planejado.

```text
prazo agregado = C + (VA − VP_C) / (VP_C+1 − VP_C)
```

C é o último mês cujo planejado acumulado não passa do valor agregado; a fração interpola o mês seguinte. Dividido pelo prazo decorrido, dá o IDP(t); a duração planejada × prazo decorrido / prazo agregado projeta a duração da obra. No exemplo dos testes, a obra planejada para dois meses termina em três: o IDP clássico fecha em 1,00, o IDP(t) em 0,67 — e a duração estimada é 3 meses.

**4. A data de status precisa ter medição.** Mês sem medição é mês que ninguém mediu; o valor agregado dele seria um palpite. A análise é sempre no mês de uma medição — por padrão, a última.

## Consequências

- **O IDC diz a verdade.** A obra que gastou R$ 16.000,00 para executar o que estava orçado em R$ 13.600,00 aparece com IDC de 0,85, e não de 1,06.
- **Índice que não existe é nulo, e não zero nem infinito.** Sem valor planejado (antes de a linha de base começar), não há IDP; sem custo real, não há IDC nem estimativa por ele; com o orçamento já gasto, não há IDPT.
- **Os indicadores são os mesmos para a obra, cada etapa e cada item.** Etapas e obra somam os itens; é assim que se acha onde está o problema — as fundações a 0,77 de IDC num mês em que os serviços iniciais estão em dia.
- **Os testes conferem as definições em obras sorteadas**, com glosas, meses pulados e obras que começam antes e terminam depois do planejado: VA de cada item refeito a partir dos lançamentos, somas das etapas, variações, ENT a menos de meio centavo de ONT × CR / VA, e o prazo agregado contando a mesma história do valor agregado — adiantado em tempo exatamente quando adiantado em dinheiro.
- **A unidade de tempo do prazo agregado é o mês**, a do período da linha de base. Uma obra de seis semanas tem duas ou três medições, e o prazo agregado dela é grosseiro — como o resto do controle mensal.
