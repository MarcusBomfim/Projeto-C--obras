# ADR-0014 — Criação com chave de idempotência, gravada na mesma transação

**Status:** aceita

## Contexto

Na rede, um pedido pode chegar ao servidor, criar o agregado — e a resposta se perder no caminho de volta. O cliente não sabe se deu certo, e tenta de novo.

Para alterar um agregado que já existe, a versão resolve: a repetição leva a versão antiga e é recusada (ADR-0012), e nada se aplica duas vezes. Para criar, não há versão. O id nasce no servidor, e a repetição criaria um segundo cronograma, uma segunda linha de base, um segundo acompanhamento — a mesma medição, paga duas vezes.

## Decisão

**1. Todo pedido que cria um agregado leva o cabeçalho `Idempotency-Key`**: um valor escolhido pelo cliente — um UUID, por exemplo —, um por operação que ele quer fazer, repetido em cada nova tentativa da mesma operação.

**2. A chave fica gravada no banco, por empresa**, com a impressão do pedido — método, caminho e corpo — e a resposta dada: o status, os cabeçalhos que fazem parte do resultado (`Location`, `ETag`) e o corpo.

**3. Reserva e criação na mesma transação.** A chave é reservada antes da operação (`insert … on conflict do nothing`), na transação que vai gravar o agregado, e a resposta é guardada antes da confirmação. Ou ficam as duas coisas — a chave e o agregado —, ou nenhuma. Operação que falha desfaz a reserva, e o cliente pode tentar de novo com a mesma chave; só a resposta de sucesso fica guardada.

**4. O banco põe os repetidos em fila.** O pedido repetido enquanto o primeiro ainda roda bate na chave reservada e espera — quem segura é o índice único do PostgreSQL — até o primeiro terminar; então recebe a resposta dele. Não há janela entre conferir a chave e reservá-la, nem o "ainda em processamento, tente depois" de outras implementações.

**5. Mesma chave, pedido diferente: recusado.** Chave repetida com outra impressão é erro de quem pediu (422), e não repetição — a operação não roda, e a resposta guardada não é devolvida para um pedido que não é o dela.

**6. A repetição devolve a resposta original, igual**, com o cabeçalho `Idempotent-Replayed: true` — mesmo que o agregado tenha mudado depois. É a resposta *daquele* pedido.

## Consequências

- **Criar é seguro de repetir.** O cliente pode tentar até receber resposta, sem medo de duplicar uma linha de base ou um acompanhamento.
- **A chave vale por empresa**: duas construtoras podem gerar o mesmo valor sem se esbarrar.
- **As chaves se acumulam.** Por enquanto ficam guardadas para sempre. A rotina que apaga as antigas — 24 horas é o prazo comum — entra com o processamento em segundo plano (Parte 6).
- **Alterar não usa chave**: a versão já impede a dupla aplicação. Uma alteração repetida depois do sucesso recebe 412 e precisa ler de novo — seguro, ainda que menos cômodo do que receber a resposta repetida.
