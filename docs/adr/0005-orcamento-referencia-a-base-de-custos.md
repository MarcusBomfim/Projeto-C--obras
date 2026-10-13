# ADR-0005 — O orçamento referencia a base de custos, e é endereçado pela numeração da EAP

**Status:** aceita

## Contexto

Um orçamento de obra é uma planilha: etapas e itens numerados (a EAP), uma quantidade por item, e um preço que vem de uma base de custos — a tabela SINAPI de um mês, ou a tabela própria da construtora — acrescido do BDI.

Três perguntas de desenho:

1. **O orçamento copia os preços da base ou aponta para eles?**
2. **Como se identifica uma linha?** O engenheiro diz "o item 2.1.3"; um banco de dados prefere um identificador que não muda.
3. **Com que precisão entra o BDI?** A fórmula do TCU dá uma dízima; a planilha mostra duas casas.

## Decisão

**1. O orçamento aponta.** Cada item guarda o código do serviço na base e a quantidade — não descrição, unidade nem preço. A planilha é calculada contra uma base por `CalculadoraDoOrcamento`, quando pedida. Trocar a base reprecifica o orçamento inteiro sem tocar nele: é assim que se atualiza um orçamento para a tabela do mês seguinte, ou se compara o mesmo projeto em duas regiões.

Congelar preços é outro conceito, com nome próprio: a **linha de base**, que fotografa orçamento e cronograma num momento para medir o realizado contra ela. Ela vem na Parte 3 — e por isso aqui não se congela nada.

**2. A linha é endereçada pela numeração da EAP**, a mesma da planilha impressa. A numeração vem da posição: removida a etapa 1, a antiga 2 passa a ser a 1 — como numa planilha. Identificadores estáveis são necessidade da persistência e da API, e chegam com elas (Partes 4 e 5) sem mudar a linguagem do domínio.

A regra estrutural é uma só: item fica dentro de etapa, e nada fica dentro de item.

**3. O BDI entra com duas casas.** Calculado pela fórmula do Acórdão 2.622/2013 do TCU —

```text
BDI = (1 + AC + S + R + G) × (1 + DF) × (1 + L) / (1 − I) − 1
```

— e arredondado a duas casas pela NBR 5891. É o percentual de duas casas, o impresso, que multiplica o custo. Pelo princípio da ADR-0004, quem confere a planilha com o BDI impresso chega ao preço impresso. A diferença não é teórica: num custo de R$ 1.000,00, o BDI de 22,88 % dá R$ 1.228,80, e o fator exato da fórmula daria R$ 1.228,77.

Os pontos de arredondamento do orçamento, somados aos da composição:

| Número | Como sai |
|---|---|
| BDI | fórmula, arredondada a 2 casas — ou informado com até 2 |
| preço unitário | custo unitário × (1 + BDI), ao centavo |
| total do item | quantidade × preço unitário, ao centavo |
| subtotal de etapa, total da obra | soma exata de valores já em centavos |

**4. O item pode ter BDI diferenciado.** O caso típico é o fornecimento de equipamento — o elevador comprado pronto, em que a construtora só intermedeia. A Súmula 253 do TCU pede BDI reduzido nesses casos: cobrar sobre o elevador o BDI da alvenaria remunera uma administração e um risco que não existem.

## Consequências

- **O orçamento não sabe se os códigos existem.** Quem confere é o cálculo, que acusa todos os códigos ausentes numa mensagem só (`1.2 (XYZ), 2.2.2 (ABC)`), em vez de um por vez — num orçamento importado de outra base, seriam dezenas de rodadas de corrigir e recalcular.
- **Mudar a base muda todo orçamento que a usa.** É o comportamento desejado até a linha de base; depois dela, o que se mede é contra a fotografia, não contra a base viva.
- **Remover uma linha renumera as seguintes.** Quem guardou "o item 2.3" de ontem pode estar olhando outro item hoje. Para o engenheiro, é o comportamento da planilha; para sistemas integrados, é o motivo dos identificadores estáveis da Parte 4.
- **A planilha fecha à mão em 400 orçamentos sorteados**, com subetapas, BDI diferenciado e composições auxiliares em vários níveis — os testes refazem cada conta com `decimal.Round`, sem usar o código que estão conferindo.
