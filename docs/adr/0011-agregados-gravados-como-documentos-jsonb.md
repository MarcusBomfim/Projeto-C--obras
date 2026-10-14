# ADR-0011 — Cada agregado é um documento `jsonb`, lido de forma estrita

**Status:** aceita

## Contexto

A Parte 4 grava os agregados no PostgreSQL. Cada agregado é uma árvore: o cronograma tem atividades e vínculos; a base de custos, insumos e composições com itens; o orçamento, etapas com itens; a linha de base, uma linha por item da EAP com um valor por mês; o acompanhamento, medições com lançamentos. Mapear cada nó para uma tabela dá mais de uma dúzia de tabelas, e o domínio teria de ceder ao ORM: construtor sem parâmetros, setters, coleções mutáveis à mostra.

E o agregado nunca é usado pela metade. O caminho crítico precisa da rede inteira; a planilha, do orçamento inteiro; o valor agregado, de todas as medições. Nenhuma pergunta do negócio busca uma atividade solta no banco.

Guardar a estrutura num documento traz dois riscos próprios: o documento lido "mais ou menos" — um campo que falta vira zero, em silêncio — e o documento escrito por outra versão do sistema.

## Decisão

**1. Uma tabela por tipo de agregado, uma linha por agregado, o agregado inteiro numa coluna `jsonb`.** No esquema `planejamento`, com nomes em snake_case:

| Coluna | Tabelas | Para quê |
|---|---|---|
| `id uuid` | todas | a identidade do agregado (ADR-0010) |
| `versao int` | todas | começa em 1 e sobe a cada gravação |
| `formato smallint` | todas | a versão do formato do documento |
| `documento jsonb` | todas | o agregado |
| `criado_em`, `atualizado_em` | todas | quando |
| `nome`, indexado | cronogramas, bases de custos, orçamentos, linhas de base | listar e procurar sem abrir documento |
| `data_base` | bases de custos | o mês de referência dos preços |
| `aprovada_em` | linhas de base | a data de aprovação |
| `linha_de_base_id` | acompanhamentos | a única referência entre agregados que o banco guarda |

Fora do documento fica só o que o banco precisa enxergar: chave, versão, o que se lista e uma referência.

**2. O banco garante o que consegue sem repetir regra de negócio:** versão e formato a partir de 1, documento que é um objeto JSON (`jsonb_typeof(documento) = 'object'`) e a chave estrangeira do acompanhamento para a linha de base, com `RESTRICT` — a linha de base não sai do banco enquanto houver acompanhamento medido contra ela. As restrições têm nome (`pk_`, `ck_`, `fk_`, `ix_`), e o erro diz qual foi violada. As regras do negócio — rede sem ciclo, composição que não usa a si mesma, valores que somam — continuam no domínio, e valem também na leitura (ADR-0010).

Só o acompanhamento tem chave estrangeira porque só ele depende de outro agregado gravado: ele mede contra uma linha de base e é lido junto com ela. A linha de base copia o que precisa quando é aprovada (ADR-0007), e o orçamento aponta para a base de custos pelos códigos, resolvidos no cálculo (ADR-0005).

**3. O documento é um tipo da infraestrutura, e não o agregado serializado.** `DocumentoDoCronograma`, `DocumentoDoOrcamento` e os demais são records que descrevem o que se grava. O domínio não ganha atributo de serialização, e renomear uma propriedade do domínio não muda o formato gravado sem ninguém ver: muda o mapeamento, e os testes comparam o JSON de cada agregado com o formato esperado.

**4. A leitura é estrita.** System.Text.Json, com o código de serialização gerado na compilação:

- propriedade obrigatória que falta, nulo onde o tipo não aceita nulo e propriedade que o documento não deveria ter fazem a leitura falhar;
- enums são gravados como texto — `"TerminoInicio"`, e não `0` —: legíveis numa consulta, e imunes a uma reordenação dos membros;
- os nós da EAP e as linhas da linha de base são polimórficos, com o discriminador `"tipo"` (`"etapa"` ou `"item"`); tipo desconhecido é recusado.

Documento que não se lê vira `DocumentoInvalido`, com o id do agregado e o motivo.

**5. O `jsonb` não devolve o texto que recebeu.** Ele descarta os espaços e não guarda a ordem das chaves: devolve as mais curtas primeiro. Hoje o discriminador `"tipo"` é a chave mais curta de todo nó polimórfico e volta na frente — por coincidência. Uma propriedade nova de nome curto, como `"id"` ou `"mes"`, passaria na frente dele, e a quebra só apareceria em documento que passou pelo banco. Por isso o leitor aceita os metadados em qualquer posição (`AllowOutOfOrderMetadataProperties`), e um teste lê um documento com o `"tipo"` no fim de cada objeto.

**6. O formato tem número.** Cada linha diz em que formato o documento foi escrito, e o código lê um formato. Documento noutro formato é recusado com a razão, em vez de lido pela metade. Mudar o formato é subir o número e escrever uma migração que converta os documentos gravados — ou manter um leitor para o formato antigo.

**7. Migrações do Entity Framework, versionadas com o código.** O `dotnet-ef` é ferramenta local do repositório (`dotnet tool restore`), e o histórico das migrações fica no próprio esquema, em `planejamento.historico_de_migracoes`. Um teste sem banco confere que o modelo não mudou sem migração; outro, com banco, que nenhuma migração ficou para trás.

**8. Os testes de persistência usam PostgreSQL de verdade**, num contêiner (Testcontainers, `postgres:17.6-alpine`, versão fixa), um só para o assembly de testes inteiro. Nem SQLite nem o provedor em memória: `jsonb`, restrições, chave estrangeira e os códigos de erro do PostgreSQL são justamente o que está sendo testado. Cada teste cria agregados com ids novos, então os testes não se atrapalham e não precisam limpar o banco. Sem Docker na máquina, os testes de banco são pulados, com o motivo; no CI, Docker indisponível é falha — teste de integração que some sem ninguém ver é pior do que teste que falha.

## Consequências

- **Ler e gravar um agregado é uma linha**: sem junção, sem N+1, sem carregar coleção por coleção. A fronteira do agregado é a fronteira da linha — e é por linha que se confere se outro gravou no meio.
- **O domínio continua sem saber do banco.** O mapeamento entre agregado e documento é código da infraestrutura, testado ida e volta com centenas de obras aleatórias.
- **Consultar dentro dos documentos é mais caro.** "Que orçamentos usam a composição X?" pede os operadores do `jsonb` — e, se a pergunta ficar frequente, um índice GIN ou uma coluna nova, numa migração. Hoje nenhuma consulta precisa disso.
- **Gravar reescreve o documento inteiro.** Para o tamanho de uma obra — milhares de atividades, não milhões — é barato, e o PostgreSQL comprime os documentos grandes.
- **A evolução do formato é responsabilidade do código**, não do banco: para o banco, o documento é só um objeto. É o preço de não espalhar o agregado em uma dúzia de tabelas, e quem segura são o número do formato, o teste de formato e a leitura estrita.
- **Os testes de integração precisam de Docker.** O contêiner sobe uma vez por execução, em segundos.
