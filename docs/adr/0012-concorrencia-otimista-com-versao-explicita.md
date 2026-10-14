# ADR-0012 — Concorrência otimista, com a versão conferida pelo banco num comando só

**Status:** aceita

## Contexto

Um cronograma ou um orçamento é editado por mais de uma pessoa — o planejador, o orçamentista, o engenheiro da obra —, e cada uma trabalha numa cópia lida minutos antes. Sem controle, quem grava por último apaga, sem saber, o que o outro gravou no meio: a atualização perdida.

Trancar a linha enquanto alguém edita não serve. A edição dura minutos, atravessa várias requisições HTTP (Parte 5), e a pessoa pode simplesmente fechar o navegador com a tranca na mão.

## Decisão

**1. Cada linha tem uma versão inteira**, que começa em 1 e sobe a cada gravação. Atualizar e remover pedem a versão que foi lida; se o banco estiver em outra, a gravação é recusada com `ConflitoDeConcorrencia`: "O cronograma … mudou desde a versão 1: está na versão 2. Leia de novo e refaça a alteração." Ninguém grava por cima do que não viu.

Uma coluna própria, e não a coluna de sistema `xmin` do PostgreSQL. O `xmin` é o número da transação que gravou a linha: não diz nada a quem lê, não sobrevive a um backup restaurado e amarraria o contrato da API a um detalhe interno do banco. A versão é dado do sistema — e a API vai mandá-la como `ETag`.

**2. A versão fica fora do domínio.** O `Cronograma` não sabe que é versionado: o repositório devolve um `Versionado<Cronograma>` — o agregado e a versão lida —, e quem grava informa a versão que leu. Concorrência é problema de quem guarda e de quem edita pela rede, não regra da obra.

**3. Conferir e gravar são um comando só:** `update … where id = @id and versao = @lida`, e o mesmo para o `delete`, pelo `ExecuteUpdate` e pelo `ExecuteDelete` do Entity Framework. Se outra gravação passou no meio, nenhuma linha muda — e só então uma consulta separa "mudou" (`ConflitoDeConcorrencia`, com a versão atual) de "não existe" (`AgregadoNaoEncontrado`). Não há janela entre conferir e gravar: um teste dispara dez gravações ao mesmo tempo a partir da mesma versão, cada uma na sua conexão, e o banco deixa passar exatamente uma.

Nada fica no rastreador de mudanças do Entity Framework: lê-se sem rastreio, grava-se direto no banco. O contexto não guarda estado entre uma operação e outra.

**4. O que o PostgreSQL responde vira exceção com nome — e mensagem para gente:**

| O banco responde | A persistência lança |
|---|---|
| nenhuma linha mudou, e a linha existe | `ConflitoDeConcorrencia` |
| nenhuma linha mudou, e a linha não existe | `AgregadoNaoEncontrado` |
| chave primária repetida (`23505`) | `AgregadoJaExiste` |
| chave estrangeira violada ao inserir (`23503`) — acompanhamento de linha de base não gravada | `AgregadoNaoEncontrado` |
| chave estrangeira violada ao apagar (`23503`) — linha de base com acompanhamento | `AgregadoEmUso` |
| documento que não se lê (ADR-0011) | `DocumentoInvalido` |

Quem chama — a API, na Parte 5 — trata o que aconteceu, e não um código SQL.

**5. O relógio é injetado.** `criado_em` e `atualizado_em` vêm de um `TimeProvider`; os testes acertam a hora que quiserem.

**6. A linha de base não tem `Atualizar`.** Ela não muda depois de aprovada (ADR-0007): fica na versão 1, e a versão só serve para removê-la.

## Consequências

- **Nenhuma gravação apaga outra em silêncio.** O conflito chega a quem gravou por último, com a versão atual, e a decisão — ler de novo e refazer — fica com quem sabe o que mudou.
- **Quem grava precisa mandar a versão que leu.** É o contrato: na API, o `ETag` da leitura volta no `If-Match` da escrita.
- **Edições em partes diferentes do mesmo agregado também conflitam.** Ana muda a fundação, Bruno muda a estrutura: o segundo é recusado, mesmo sem colisão de fato. É o preço de o agregado ser a unidade de consistência — mesclar sozinho exigiria entender cada mudança, e uma rede com vínculos não se mescla campo a campo.
- **Um conflito custa uma consulta a mais**, só quando acontece; o caminho comum é um comando.
