# ADR-0004 — Dinheiro em decimal, e arredonda-se o que aparece na planilha

**Status:** aceita

## Contexto

Orçamento de obra é conferido com calculadora. A fiscalização, o cliente e o auditor pegam a planilha impressa e refazem as contas: coeficiente vezes preço, quantidade vezes preço unitário, soma da coluna. Se o resultado dá um centavo diferente do impresso, a planilha "não fecha" — e planilha que não fecha é questionada inteira, não só naquela linha.

Há duas formas de fazer as contas:

1. **Precisão total, arredonda-se só para mostrar.** Na composição da alvenaria, a argamassa entra com 0,012 × 646,06 = 7,75272. O sistema guarda 7,75272, mostra 7,75 e soma 7,75272. Matematicamente é o mais exato.
2. **Arredonda-se cada número no momento em que ele aparece, e o seguinte é calculado a partir do número arredondado.** A linha vale 7,75, e é 7,75 que entra na soma.

Na forma 1, quem soma as linhas impressas não chega ao total impresso. Na forma 2, chega.

## Decisão

**Forma 2.** Todo número que aparece na planilha é guardado como aparece, e todo número da planilha se refaz com uma calculadora a partir dos números que ela mesma mostra.

Na base de custos, isso dá três pontos de arredondamento:

- **Preço do insumo:** entra em centavos. Preço com mais casas é recusado no cadastro — R$ 1,3249 não existe em nota fiscal.
- **Custo parcial de cada linha da composição:** coeficiente × custo unitário, arredondado ao centavo.
- **Custo unitário da composição:** soma das parcelas, que já estão em centavos. Uma composição auxiliar entra na de cima por esse custo.

O coeficiente não é arredondado: ele é dado, não calculado, e aparece como foi digitado.

**A regra de arredondamento é a da NBR 5891:** meio centavo exato vai para o centavo par (3,085 → 3,08; 3,075 → 3,08). Arredondar o meio sempre para cima puxa para cima a soma de muitas parcelas; para o par, os desvios se compensam. É o padrão do `decimal` no .NET, mas o código escreve `MidpointRounding.ToEven` explicitamente: a regra vem da norma, não do framework.

**O tipo é `Dinheiro`, sobre `decimal`.** Nunca `double`: em ponto flutuante binário, 0,1 + 0,2 não dá 0,3.

## Consequências

- **A planilha fecha à mão**, linha por linha e na soma. É a propriedade que importa para quem confere, e os testes a verificam.
- **O custo se afasta do exato em até meio centavo por linha.** Uma composição de 20 linhas pode diferir em até R$ 0,10 por unidade do custo em precisão total — numa quantidade de 10.000 m², até R$ 1.000. O desvio é limitado, aparece na planilha e é auditável; o da forma 1 é menor, mas invisível e impossível de conferir.
- **Arredondar vira regra de negócio, com lugar definido.** `Dinheiro` guarda a precisão que a conta tiver e só arredonda quando alguém chama `EmCentavos()` — e quem chama é o ponto da planilha onde o número aparece. Os próximos pontos (BDI, preço unitário, total do item) seguem o mesmo princípio.
- **Uma moeda só.** `Dinheiro` não tem campo de moeda. No dia em que houver obra cotada em outra, ele entra — e somar real com dólar passa a ser erro de compilação ou de execução, não de conta.
