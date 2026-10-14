# ADR-0010 — Identidade nos agregados, e a reconstituição passa pelas regras

**Status:** aceita

## Contexto

Até a Parte 3, os agregados viviam só em memória: um `Cronograma` era aquele objeto, e pronto. Para gravar e ler de um banco — e, depois, para a API endereçar "o cronograma tal" —, duas coisas faltavam:

1. **Identidade.** Algo que continue igual quando o nome, as atividades e todo o resto mudarem.
2. **Um caminho de volta.** Ler um cronograma gravado é criar um objeto com estado que já existia. Os construtores públicos não servem — dão identidade nova —, e os métodos de escrita checam ciclo vínculo a vínculo, o que é quadrático numa rede grande.

E uma pergunta de desenho: o que fazer com um dado gravado que viola uma regra — corrompido, editado à mão no banco, gravado por uma versão com defeito?

## Decisão

**1. Cada raiz de agregado tem um identificador do seu próprio tipo** — `CronogramaId`, `BaseDeCustosId`, `OrcamentoId`, `LinhaDeBaseId`, `AcompanhamentoId` —, todos derivados de `Identificador`. Passar o id de um orçamento onde se espera o de um cronograma é erro de compilação, e dois ids de tipos diferentes nunca são iguais, nem com o mesmo Guid.

São records de classe, como os outros valores do domínio: o Guid vazio é recusado, e não existe `default` que fure a regra. Os ids novos são **Guid versão 7**, que começam pelo instante da criação — ids criados em sequência ficam em sequência no índice do banco, sem a fragmentação do Guid aleatório.

**2. A reconstituição é interna, e visível só para a infraestrutura e os testes do domínio.** Cada agregado tem um caminho `internal` para ser refeito com uma identidade existente: `Cronograma.Reconstituir`, `BaseDeCustos.Reconstituir`, `LinhaDeBase.Reconstituir`, e construtores internos para `Orcamento` e `Acompanhamento`. A API pública continua sendo a das regras de negócio; quem não é a persistência não tem como criar um agregado por fora da porta da frente. Um teste de arquitetura confere a lista de quem enxerga os internos.

**3. Nada volta sem passar pelas regras.** Reconstituir não é desligar a validação:

| Agregado | Como volta |
|---|---|
| Cronograma | atividades pelas regras de cadastro; vínculos sem repetição e sem laço, com **uma ordenação topológica no fim**, em O(V + E), em vez de uma busca por vínculo |
| Base de custos | insumos e composições primeiro, itens depois — uma composição pode usar outra gravada depois dela —, e uma checagem de ciclo no fim |
| Orçamento | pelos métodos de sempre, a partir do construtor com identidade |
| BDI | **como foi gravado**: o percentual e as parcelas, sem recalcular a fórmula |
| Linha de base | os valores gravados, conferidos: meses em sequência, um valor por mês, os meses somando cada linha, as etapas somando a obra |
| Acompanhamento | cada medição registrada de novo, pelas regras de sempre |

Dado gravado que viola uma regra é recusado **na leitura**, com uma mensagem que diz o quê — e não descoberto depois, num caminho crítico ou num boletim errado.

**4. O que foi decidido volta como foi decidido.** O BDI e a linha de base não são recalculados na leitura. Se um dia a regra de arredondamento mudar, recalcular mudaria em silêncio o preço de um orçamento fechado ou o planejado de uma obra em andamento. O que é derivado — caminho crítico, planilha, físico-financeiro, valor agregado — continua sendo recalculado a cada pedido, como sempre.

## Consequências

- **O domínio continua sem saber que existe banco.** Não há atributo de mapeamento, não há construtor sem parâmetros para o ORM, não há setter público. A persistência conversa com ele pela reconstituição e pelas propriedades de leitura.
- **`InternalsVisibleTo` é um acoplamento consciente.** A infraestrutura enxerga todos os internos, não só a reconstituição. É o preço de não expor publicamente um jeito de criar agregados com identidade arbitrária; a lista fechada, conferida por teste, limita a quem.
- **Ler uma rede de três mil vínculos é linear.** A escrita normal continua checando ciclo vínculo a vínculo — é lá que a mensagem "fecharia um ciclo: A → B → C → A" ajuda quem está editando. Na leitura, a mensagem diz quais atividades estão presas no laço.
- **A identidade nasce no domínio**, e não no banco: um agregado tem id antes de ser gravado, e a API pode devolvê-lo sem esperar o insert.
