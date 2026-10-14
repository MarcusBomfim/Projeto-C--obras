# ADR-0013 — Cada empresa só enxerga o que é dela: filtro no código e segurança por linha no banco

**Status:** aceita

## Contexto

A API atende várias construtoras no mesmo sistema. Uma não pode ver, nem por engano, o cronograma, o orçamento ou as medições de outra: são preços, margens e prazos — informação comercial.

Há três jeitos comuns de separar os dados de cada cliente:

- **um banco por empresa** — isolamento completo, mas uma migração por banco, um pool de conexões por banco e um custo que cresce com o número de clientes;
- **um esquema por empresa** — os mesmos problemas de migração e de pool, num banco só;
- **uma coluna com a empresa em cada tabela** — um banco, uma migração, um pool; e o isolamento depende de toda consulta filtrar.

O terceiro é o mais simples de operar e o mais arriscado: basta uma consulta sem o filtro para uma empresa ler os dados de outra.

## Decisão

**1. Uma coluna `empresa_id` em toda tabela com dados de empresa**, obrigatória. A empresa não é conceito do domínio — o `Cronograma` não sabe de quem é —: é dado da persistência, como a versão (ADR-0012). Quem hospeda a persistência diz em nome de quem o contexto trabalha (`IEmpresaAtual`): a API, pelo token de quem fez o pedido; os testes e as ferramentas, por uma empresa fixa.

**2. Duas barreiras, uma em cada camada.**

- **No código:** toda consulta do Entity Framework sai com um filtro global pela empresa — leitura, atualização e remoção —, e toda inserção carimba a empresa do contexto. Sem empresa, nada se lê e nada se grava.
- **No banco:** segurança por linha (*row-level security*) do PostgreSQL, forçada até para o dono das tabelas. A conexão declara a empresa num parâmetro de sessão, `planejamento.empresa_id`, e a política só deixa ver e gravar as linhas dela. Uma consulta que escape do filtro do código — SQL escrito à mão, um `IgnoreQueryFilters` — continua isolada.

Cada barreira é testada sem a outra: o filtro do código conectado como superusuário, que passa por cima do banco; a segurança do banco com o filtro do código desligado.

**3. A empresa vai ao banco a cada conexão aberta**, por um interceptador do Entity Framework — inclusive "nenhuma empresa", com o valor vazio. Uma conexão reaproveitada do pool nunca leva a empresa de um pedido para o seguinte, mesmo com a limpeza do pool desligada (`No Reset On Close`).

**4. Referência entre agregados não atravessa empresas.** A chave estrangeira do acompanhamento para a linha de base é composta: empresa e id. Chave estrangeira não passa pela segurança por linha no PostgreSQL — é a chave composta que impede um acompanhamento de medir contra a linha de base de outra empresa.

**5. Para outra empresa, o agregado não existe.** Ler, alterar ou apagar o agregado de outra empresa dá o mesmo resultado de um id que nunca existiu — na API, 404, e não 403. Responder "existe, mas não é seu" já é vazar.

**6. A aplicação conecta com um papel comum**, sem superusuário e sem `BYPASSRLS`. As migrações rodam com um papel que passa por cima da segurança por linha — superusuário, ou o dono com `BYPASSRLS` —, senão uma migração que converta documentos gravados não enxergaria linha nenhuma. Os testes fazem igual: migram como superusuário e trabalham com um papel da aplicação.

## Consequências

- **Um banco, uma migração, um pool**, para qualquer número de empresas.
- **Esquecer o filtro não vaza.** É preciso errar duas vezes, em duas camadas diferentes.
- **Um comando a mais a cada conexão aberta.** Fora de transação, o Entity Framework abre a conexão a cada operação; um pedido com várias operações paga várias idas a mais ao banco. Se pesar, o pedido passa a manter uma conexão aberta do começo ao fim.
- **Toda tabela nova precisa da política.** Um teste lista as tabelas do esquema e falha se alguma não tiver segurança por linha forçada.
- **A migração não inventa dono.** A coluna entra obrigatória e sem valor padrão: num banco que já tivesse agregados, a migração falharia em vez de entregar todos a uma empresa qualquer.
- **Os índices começam pela empresa** (`empresa_id`, `nome`): a listagem de uma empresa não passa pelas linhas das outras.
