# ADR-0006 — O físico-financeiro é derivado do cronograma, e fecha com o orçamento ao centavo

**Status:** aceita

## Contexto

O cronograma físico-financeiro diz quanto da obra se executa — e se fatura — em cada mês. É com ele que o cliente programa o desembolso, o banco libera as parcelas do financiamento e a fiscalização confere as medições.

O jeito comum de fazê-lo é à parte: uma planilha em que alguém digita, etapa por etapa, o percentual de cada mês. Ela nasce coerente com o cronograma e deixa de ser no primeiro atraso — ninguém volta a redistribuir os percentuais à mão. E como cada percentual é arredondado, a soma dos meses raramente dá o total do orçamento.

## Decisão

**1. O físico-financeiro é calculado, não digitado.** Cada item do orçamento é vinculado a uma atividade do cronograma. O valor do item se distribui pelos dias úteis da atividade, por igual — a alvenaria de 10 dias executa um décimo por dia — e os dias de cada mês somam a parte do mês. Um marco concentra o valor no dia dele: o elevador pago na entrega das chaves entra inteiro no mês da entrega.

Os dias de trabalho vêm do calendário em que o cronograma foi programado, que o próprio `CronogramaProgramado` carrega. Passar outro calendário seria abrir espaço para datas e dias discordarem.

**2. Todo item precisa estar vinculado.** Item sem atividade, ou vinculado a uma atividade que não está no cronograma, impede o cálculo — senão a soma dos meses não daria o total da obra. Os problemas vêm todos numa mensagem, separados por tipo.

**3. Os centavos se rateiam pelo maior resto.** A parte exata de cada mês raramente cai no centavo: R$ 100,00 em três meses iguais são 33,333… cada, e arredondar cada um dá 99,99. Cada mês recebe os centavos inteiros da sua parcela exata, e os que sobram vão, um a um, para os meses de maior resto; no empate, para o mês mais cedo. Resultado: cada mês fica a menos de um centavo da sua parcela exata, e **cada item fecha ao centavo**. Como as etapas e os meses são somas de itens, tudo fecha ao centavo com o orçamento.

**4. Duas curvas: pelas datas cedo e pelas datas tarde.** A curva S cedo é o ritmo mais rápido possível; a tarde, o mais lento que ainda entrega no prazo. A faixa entre as duas é onde a obra pode andar. É a régua que a Parte 3 usa para dizer se a obra está adiantada ou atrasada.

## Consequências

- **Mudou o cronograma, mudou o físico-financeiro**, sem ninguém redistribuir nada. É o mesmo princípio do resto do domínio: o que é derivado não é guardado.
- **A distribuição uniforme é uma aproximação.** Na obra real, a fundação compra o aço no primeiro dia e a pintura gasta mais mão de obra no fim. Quando isso importar, cada item pode ganhar uma curva de distribuição própria — carregada no início, no fim, em sino — sem mudar o resto: o rateio já trabalha com pesos, não com dias.
- **O período é o mês de calendário**, porque é o período da medição. Quinzena ou semana seriam um parâmetro a mais, quando alguém pedir.
- **A distribuição é por item**, e não só por etapa: é o valor planejado de cada item em cada mês — a base do valor agregado da Parte 3.
- **Os testes conferem as promessas em 400 obras sorteadas, nas duas curvas:** os meses somam o total do orçamento, cada linha soma o próprio total, cada etapa soma o que está abaixo dela mês a mês, e cada item fica, em cada mês, a menos de um centavo da parte proporcional aos dias que a atividade trabalha nele — contados de novo, dia a dia, pelo teste.
