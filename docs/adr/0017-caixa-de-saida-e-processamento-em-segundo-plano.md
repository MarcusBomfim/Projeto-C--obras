# ADR-0017 — Caixa de saída na mesma transação, tratada em segundo plano

**Status:** aceita

## Contexto

O acompanhamento registra os fatos (ADR-0016); falta levá-los a quem se interessa — por ora, o painel das obras. Publicar direto num serviço de mensagens, no meio da gravação, é escrever em dois lugares sem transação entre eles: o processo cai depois de gravar a medição e antes de publicar, e o fato se perde; publica e a gravação falha, e o painel mostra uma medição que não existe.

E há trabalho que não é de pedido nenhum: a ADR-0014 deixou para esta parte a limpeza das chaves de idempotência antigas.

## Decisão

**1. Caixa de saída, na mesma transação do agregado.** Cada fato vira uma mensagem na tabela `caixa_de_saida`, gravada na transação que grava o agregado: ou ficam os dois, ou nenhum — num conflito de versão, nem um nem outro. As mensagens de uma gravação entram uma a uma, na ordem dos fatos, e a sequência da caixa guarda essa ordem.

**2. Um catálogo traduz evento em mensagem**: um tipo (`medicao-registrada`), um número de formato e o conteúdo em JSON — separado do evento como o documento é separado do agregado (ADR-0011). Mudar o formato publicado é mudar o número dele. Tipo ou formato que esta versão não conhece é recusado, e não adivinhado: a mensagem espera uma versão que a conheça.

**3. Um processador em host próprio.** O `PlanejamentoObras.Processador` roda fora da API: cai e escala sem ela. Ele reserva uma mensagem por transação, com `for update skip locked` — dois processadores dividem a fila, e o segundo pula a mensagem que o primeiro travou.

**4. Cada mensagem é tratada em nome da empresa dela.** A reserva é a única leitura que atravessa empresas, e passa por uma função do banco que devolve só o id e a empresa da mensagem. Dali em diante o processador assume essa empresa — no código e na conexão, só até o fim da transação —, e o conteúdo da mensagem, o que o tratamento lê e o que ele grava passam pelo filtro e pela segurança por linha como num pedido (ADR-0013). São três as funções que atravessam empresas: a reserva e as duas limpezas. Rodam como o dono das tabelas, com o `search_path` fixo, e só o papel do processador as executa: a migração revoga o acesso de `PUBLIC`, e a API, que está de frente para a internet, não alcança nenhuma.

**5. O que o tratador grava e a marca de tratada são confirmados juntos.** Se um tratador falha, o que ele gravou é desfeito até um ponto de salvamento anterior ao tratamento, e a falha fica registrada na mensagem — a tentativa, o erro e a hora de tentar de novo: 30 segundos, o dobro a cada falha, no máximo uma hora. Na décima falha a fila desiste da mensagem, e ela fica abandonada para alguém ver por quê. Só o host parando interrompe o tratamento: um tempo esgotado dentro de um tratador é falha dele.

**6. Tratador idempotente e indiferente à ordem.** O que o tratador grava no banco acontece uma vez só, porque é confirmado junto com a marca de tratada. Fora do banco — um e-mail, uma chamada a outro sistema —, a entrega é *ao menos uma vez*: o processador pode cair entre o efeito e a confirmação. E a ordem não é garantida: a mensagem que falhou volta depois da seguinte, e dois processadores tratam duas ao mesmo tempo. Todo tratador tem de dar o mesmo resultado repetido ou fora de ordem.

**7. O painel das obras é uma projeção, refeita do estado.** Uma linha por acompanhamento, com os indicadores da última medição e a situação da obra — o pior dos dois índices de desempenho decide: abaixo de 0,85 é crítica, abaixo de 0,95 pede atenção, e sem medição não há o que medir. A mensagem só diz qual acompanhamento mudou; a linha é recalculada do acompanhamento como está, e gravada num comando só, que não troca a linha por outra feita de uma versão mais velha. Tratar a mesma mensagem duas vezes, ou uma atrasada depois da seguinte, dá a mesma linha.

**8. O processador acorda com a mensagem nova.** Um gatilho na caixa avisa por `NOTIFY` — que o PostgreSQL só entrega quando a transação é confirmada — e o processador escuta numa conexão própria, fora do pool. A cada 30 segundos ele olha a fila mesmo sem aviso: é quando voltam as mensagens que falharam, e é a rede para o aviso que se perdeu. Sem a conexão de escuta, ele segue só com o intervalo, e a refaz na volta seguinte.

**9. Rotinas de limpeza**, de hora em hora: chaves de idempotência com mais de 24 horas, como a ADR-0014 prometia, e mensagens tratadas há mais de 7 dias. A abandonada fica. Todo processador roda todas as rotinas, então cada uma pode rodar duas vezes ao mesmo tempo — apagar o que já foi apagado não apaga mais nada.

## Consequências

- **Nenhum fato se perde entre a gravação e a publicação, e nenhum é publicado sem ter sido gravado.**
- **Registrar uma medição não espera o painel.** O painel fica atrás da última medição pelo tempo do tratamento — instantes, com o aviso; até o intervalo, sem ele. Cada linha diz de que versão do acompanhamento veio e quando foi atualizada.
- **Mais processadores, mais vazão**, sem coordenação entre eles — e sem ordem entre mensagens, que é o contrato do tratador.
- **A implantação pode ser aos poucos.** A API nova pode gravar um fato que o processador velho não conhece: ele tenta de novo, com espera crescente, por cerca de três horas antes de desistir.
- **Quem implanta cria mais um papel no banco**, o do processador, e concede a ele a execução das três funções. Os testes criam os dois papéis do mesmo jeito.
- **Mensagem abandonada pede gente.** A desistência é registrada como erro no log, e a mensagem fica na caixa, com o último erro, até alguém tratar a causa.
- **Uma mensagem por transação** custa algumas idas ao banco por mensagem — de sobra para o ritmo de medições de uma construtora, que se contam por mês. Se um dia faltar vazão, o próximo passo é reservar em lotes.
