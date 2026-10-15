# ADR-0016 — Eventos de domínio: fatos no passado, registrados pelo agregado

**Status:** aceita

## Contexto

Até a Parte 5, o que acontece com um agregado só interessa a ele: a medição registrada muda o acompanhamento, e quem quiser saber lê o acompanhamento. A Parte 6 traz quem precisa saber *que* algo aconteceu — o painel das obras, que mostra a situação de cada uma e se atualiza a cada medição.

Há dois caminhos. No primeiro, quem registra a medição atualiza o painel junto: a API chama o painel no meio da gravação. Domínio, API e painel ficam amarrados, e cada novo interessado — uma notificação, uma integração com o financeiro — é mais uma chamada no caminho de quem só queria medir. No segundo, o agregado diz o que aconteceu, e quem se interessa escuta.

## Decisão

**1. O agregado registra os fatos**, numa lista, na ordem em que acontecem: `AcompanhamentoIniciado`, `MedicaoRegistrada`, `MedicaoDesfeita`. O nome é no passado — é fato, não pedido. E um fato só existe se a regra deixou: medição recusada não gera evento.

**2. O evento é um record selado e imutável**, com a identidade do agregado e o mínimo para dizer o que houve — o número e o mês da medição —, e não o estado do agregado. Quem precisa do estado lê o agregado, que é a fonte da verdade. É notificação, e não transferência de estado: o contrato do evento fica pequeno e estável, e ninguém age sobre uma cópia velha do agregado.

**3. O domínio não publica nada; só registra.** Quem publica é a persistência, junto com a gravação, e depois descarta os eventos publicados. O domínio continua sem saber que existem fila, banco ou painel.

**4. Reconstituir não é acontecer de novo.** Ler um acompanhamento do banco registra outra vez as medições gravadas, pelas regras de sempre (ADR-0010) — e descarta os eventos: aqueles fatos já tinham sido publicados quando aconteceram.

**5. Evento só entra quando alguém precisa dele.** Hoje, os do acompanhamento, que o painel consome. Cronograma, orçamento e linha de base não registram nada ainda; quando precisarem, o mecanismo é o mesmo — derivar de `AgregadoComEventos` e registrar.

## Consequências

- **Um interessado novo não mexe no domínio nem na API:** escuta os eventos.
- **A ordem dos fatos de cada agregado fica registrada**; o que eles significam fica com cada interessado.
- **Os eventos vivem na memória até a gravação.** Se a gravação falhar, o agregado é descartado com eles: não se publica fato que não ficou gravado.
- **Um teste de arquitetura confere** que todo evento de domínio é um record selado.
