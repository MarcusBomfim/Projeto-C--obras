# ADR-0003 — Tempo em dias úteis, intervalos semiabertos e a data dos marcos

**Status:** aceita

## Contexto

O caminho crítico é uma conta com números; o Gantt mostra datas. Entre os dois mora o erro mais comum de software de cronograma: o "um a mais" ou "um a menos". Uma atividade de 5 dias que começa na segunda termina na sexta ou no sábado? A sucessora começa no dia do término ou no seguinte? Quando cada parte do código responde de um jeito, aparecem `+ 1` e `- 1` espalhados — e cada um é um bug esperando a próxima mudança.

Há ainda um caso que não tem resposta óbvia: o **marco**, uma atividade de duração zero ("Entrega das chaves", "Início da alvenaria"). Ele é um instante, e em dias úteis inteiros o fim de um dia e o começo do seguinte são o mesmo número.

## Decisão

**1. O cálculo não conhece datas.** `CalculadoraDeCaminhoCritico` trabalha em dias úteis contados a partir do início da obra. Trocar o calendário — sábado passar a ser dia útil, um feriado municipal entrar — não muda nada no algoritmo; muda só a tradução para datas, feita depois por `ProgramacaoDoCronograma`.

**2. No cálculo, intervalos semiabertos.** Uma atividade de 5 a 15 ocupa os dias úteis 5, 6, …, 14: o término é o primeiro dia em que ela já acabou. Daí:

- término = início + duração, **sempre** — inclusive para marcos, em que início = término;
- num vínculo término-início sem defasagem, a sucessora começa no número exato do término da predecessora;
- a duração da obra é o maior término.

Nenhum `+ 1` ou `- 1` no algoritmo.

**3. Na saída, datas inclusivas.** O engenheiro lê "de segunda a sexta", não "de segunda até antes de sábado". O início *n* vira o *n*-ésimo dia útil; o término *n* vira o dia útil anterior a ele — o último dia em que a atividade acontece. É o único `- 1` do projeto, num lugar só, explicado onde está.

**4. A data de um marco vem do que o posiciona.** O instante *k* é o fim do dia *k − 1* e o começo do dia *k*. Para uma atividade com duração não há dúvida — início é começo de dia, término é fim de dia. Para o marco, a regra é:

| O que posiciona o marco | Lado | Aparece em | Exemplo |
|---|---|---|---|
| o término de uma atividade (TI, TT) | fim de dia | último dia do que ele conclui | "Conclusão da locação" na quinta em que a locação acaba, e não na sexta |
| o início de uma atividade (II, IT) | começo de dia | primeiro dia do que ele inicia | "Início da locação" na terça em que ela começa |
| o começo da obra (nada o empurra) | começo de dia | primeiro dia da obra | "Início da obra" |
| outro marco | o mesmo lado dele | no mesmo dia, se não houver defasagem | o "Aviso à fiscalização" que segue o "Início da locação" sai no dia dele |
| um término e um início no mesmo instante | começo de dia | primeiro dia do que ele inicia | a medição que exige a mobilização concluída e a locação começada sai no dia em que a locação começa |

O empate fica com o começo de dia porque é o único lado que vem depois dos dois: no fim da sexta a locação ainda não começou. É também o que faz o MS Project, que calcula em horas — 08:00 de segunda vem depois de 17:00 de sexta.

**5. A data tarde de um marco usa o mesmo lado da cedo**, e nenhuma data passa do último dia da obra. Um marco de início com folga até o fim da obra aparece, no tarde, no último dia — e não no dia seguinte, que já não faz parte dela.

## Consequências

- **O calendário é substituível sem tocar no algoritmo**, e o algoritmo é testado com números de livro, sem depender de em que dia da semana caiu cada data.
- **Cedo e tarde de um marco nunca se invertem**, e um marco crítico mostra a mesma data nos dois.
- **As regras são conferidas por exemplo e por propriedade.** Além dos casos acima escritos à mão, 400 redes sorteadas, cada uma começando num dia diferente, conferem que toda data é dia útil e cai dentro da obra, que cada atividade ocupa exatamente a sua duração, e que nenhum marco aparece antes do que o empurra.
- **Limitação conhecida:** como o marco tem um lado só, no cronograma *tarde* um marco de término pode aparecer na véspera do início tarde de uma predecessora amarrada a ele por início-início — o mesmo instante, lido dos dois lados da noite. Escolher o lado a cada passada evitaria isso, mas faria um marco crítico mostrar datas diferentes no cedo e no tarde, o que confunde mais quem lê. Se o planejador precisar controlar o lado, o caminho é o tipo de marco explícito — de início ou de término —, como no Primavera P6.
- **A menor unidade é o dia útil.** Não há meio período nem horas; turnos e hora extra pedem outro modelo, e não um ajuste neste.
