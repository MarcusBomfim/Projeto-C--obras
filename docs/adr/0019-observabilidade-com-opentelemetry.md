# ADR-0019 — Observabilidade com OpenTelemetry, com o rastro atravessando a caixa de saída

**Status:** aceita

## Contexto

São dois serviços — a API e o processador — e um fato passa pelos dois: a medição registrada num pedido HTTP chega ao painel minutos ou instantes depois, noutro processo, pela caixa de saída (ADR-0017). Quando o painel atrasa ou uma mensagem falha, a pergunta é sempre a mesma: o que aconteceu com *aquele* pedido? Log solto em dois processos não responde; o `traceId` do ProblemDetails (ADR-0015) aponta para um rastro que ninguém está guardando.

E quem vai rodar os serviços — o Compose hoje, um orquestrador amanhã — precisa saber, sem ler log, se cada um está vivo e se está pronto para trabalhar.

## Decisão

**1. OpenTelemetry nos dois serviços, configurado num lugar só.** O `PlanejamentoObras.Observabilidade` instrumenta os pedidos HTTP, os comandos no PostgreSQL (pelo Npgsql), o runtime do .NET e as fontes do próprio sistema — tudo o que se chama `PlanejamentoObras.*` —, e manda os logs no mesmo rastro. O recurso diz o serviço, a versão com o commit e o ambiente.

**2. Para onde vai é configuração do ambiente.** Com `OTEL_EXPORTER_OTLP_ENDPOINT` definido, rastros, métricas e logs saem por OTLP — para um coletor, um painel local ou um fornecedor, sem mudar código. Sem ele, nada sai do processo; o rastro continua existindo, e é dele o `traceId` dos erros. O rastro de quem chama — um `traceparent` no pedido — é continuado.

**3. O rastro atravessa a caixa de saída.** Publicar a mensagem é um passo do pedido que registrou o fato, e a mensagem leva o contexto dele, no formato `traceparent`, numa coluna da caixa. O processador trata em continuação a ele: o pedido HTTP, a publicação e o tratamento — com os comandos que ele fez no banco — ficam num rastro só, mesmo com minutos entre um e outro. As tentativas depois de uma falha também. A mensagem sem contexto — gravada sem ninguém rastreando, ou antes desta versão — começa um rastro novo. Os atributos seguem as convenções de mensageria do OpenTelemetry. O sistema de mensageria, que não tem nome padrão para uma caixa de saída numa tabela, é `caixa_de_saida`; o destino é o tipo da mensagem — `medicao-registrada` —, e não a tabela: os tratadores assinam por tipo, como se assina um tópico.

**4. Olhar a fila não é rastro.** A reserva da próxima mensagem e a abertura da escuta do `LISTEN` rodam com a instrumentação suspensa: a volta vazia acontece a cada aviso e a cada intervalo, e encheria o painel de rastros sem história. Só o tratamento de uma mensagem conta uma — e cada execução de rotina de limpeza, que também vira um rastro.

**5. A fila tem métricas próprias**, com só o tipo da mensagem e o resultado — tratada, falhou, abandonada — como atributos: tentativas, duração de cada uma e a **espera**, da gravação do fato ao fim do tratamento. A espera é o número de quem usa: quanto a medição leva para chegar ao painel. É ela que diz se a consistência eventual está curta como a ADR-0017 promete. Id de mensagem ou de empresa numa métrica multiplicaria as séries sem fim; esses ficam no rastro.

**6. Duas verificações de saúde**, sem token e fora do documento OpenAPI: `/saude/viva` e `/saude/pronta`.
- **Viva** quer dizer que reiniciar não resolveria: o processo responde e, no processador, a fila está girando — deu sinal de vida há menos de quatro intervalos. Um tratador que trava para a fila, e só reiniciar a destrava.
- **Pronta** quer dizer viva e alcançando o banco. O banco fora do ar não mata ninguém: o serviço só deixa de receber trabalho até ele voltar, porque reiniciar não põe o banco no ar.

A resposta diz a situação de cada verificação, e nada do erro por trás dela, que vai para o log. As verificações não viram rastro, nem o `select 1` delas: o orquestrador pergunta a cada poucos segundos.

**7. O processador responde HTTP**, só para as verificações. Deixa de ser um worker puro e passa a ser um host web sem nenhuma rota de negócio.

## Consequências

- **Um `traceId` no erro leva ao rastro inteiro** — o pedido, os comandos no banco e, se houve fato, o tratamento dele em segundo plano.
- **O atraso do painel vira número.** A espera da fila, por tipo, responde "quanto tempo a medição leva para aparecer" sem abrir rastro nenhum.
- **Rastro longo.** O tratamento que falha volta em 30 segundos, um minuto, até uma hora — e continua no mesmo rastro. Um painel que agrupa por rastro mostra horas entre o primeiro e o último passo; é a história verdadeira.
- **O processamento depende do SDK do OpenTelemetry**, e não só da API do .NET, por um motivo: suspender a instrumentação ao olhar a fila. As fontes e os medidores são da biblioteca padrão.
- **A coluna do rastro vai para o banco**, a cada mensagem: 55 caracteres. A migração a cria nula, e as mensagens que já estavam na caixa começam rastros novos.
- **Amostragem fica para o coletor.** Todo rastro é registrado; com volume, quem decide o que guardar é o coletor, de preferência depois de ver o rastro inteiro — o tratamento com erro é o que mais interessa guardar.
- **O processador ocupa uma porta**, que em desenvolvimento é a 5090.
