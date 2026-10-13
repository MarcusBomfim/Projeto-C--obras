# ADR-0007 — A linha de base é uma cópia congelada do plano

**Status:** aceita

## Contexto

Para dizer se uma obra está adiantada ou atrasada, mais cara ou mais barata, é preciso uma referência que não se mexa: o plano aprovado. É a **linha de base**.

Até aqui, tudo no domínio é vivo de propósito (ADR-0005): o orçamento aponta para a base de custos, e mudar a base reprecifica o orçamento; o físico-financeiro é recalculado do cronograma a cada pedido. Se a execução fosse medida contra esses objetos vivos, a régua mudaria junto com o que ela mede — a obra atrasada "deixaria de estar atrasada" no dia em que alguém atualizasse o cronograma.

## Decisão

**A linha de base é uma cópia, feita uma vez, de tudo o que a medição precisa.** `LinhaDeBase.Estabelecer` recebe a planilha calculada e o cronograma programado e guarda, para cada etapa e cada item:

- número, descrição, total a preço e custo total;
- para itens, a unidade, a **quantidade contratada**, o **preço unitário com BDI**, o **custo unitário sem BDI** e a atividade vinculada;
- o **planejado de cada mês, a preço e a custo**, tirado do cronograma físico-financeiro — pelas datas cedo, ou pelas tarde, à escolha de quem aprova.

E, da obra, os meses planejados, as datas de início e término, o total contratado e o custo total.

**Preço e custo, os dois.** A preço é o que se mede e se paga: o boletim de medição usa o preço unitário. A custo é o que se gasta: o valor agregado compara o custo real com o custo orçado, e não com o preço de venda, que inclui o BDI. Para isso, o físico-financeiro passou a distribuir também o custo direto de cada item, pelos mesmos dias e com os mesmos pesos do valor.

**Não guarda referência a nada vivo.** Nem ao orçamento, nem ao cronograma, nem à base de custos: copia valores. Os resultados calculados (`OrcamentoCalculado`, `CronogramaProgramado`) já são fotografias, mas expõem listas que um consumidor poderia converter de volta e alterar; a linha de base copia tudo para coleções só de leitura de verdade (`Array.AsReadOnly`).

**Não tem método que a altere.** Plano novo — depois de um aditivo de contrato, por exemplo — é outra linha de base, com outro nome e outra data de aprovação.

## Consequências

- **A régua não se mexe.** Revisar o orçamento ou o cronograma depois da aprovação não muda o que já foi planejado; o teste muda a quantidade de um item depois de estabelecida a linha de base e confere que ela continua igual.
- **Os números da linha de base estabilizam a numeração.** O orçamento vivo renumera quando uma linha é removida (ADR-0005); a linha de base, não. A medição aponta para "o item 2.1 da linha de base 1", e esse endereço vale para sempre.
- **O que fica de fora fica de fora de propósito:** a composição analítica de cada preço, a base de custos, a rede de precedências. A medição mede contra quantidade e preço unitário; a abertura do preço é assunto do orçamento.
- **Trocar de linha de base não está implementado.** O aditivo que muda quantidades exige levar o histórico de medições para a linha de base nova, e as regras disso — o que acontece com o valor planejado dos meses já passados — dependem do contrato. Fica para quando houver o fluxo de contrato.
