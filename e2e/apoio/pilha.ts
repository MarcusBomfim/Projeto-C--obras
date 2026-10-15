/*
 * Onde a pilha dos testes de ponta a ponta roda. Portas próprias — 5081 e
 * 5181 —, para não esbarrar na API e no front-end de desenvolvimento.
 */

export const Api = 'http://localhost:5081';

export const Web = 'http://localhost:5181';

/** O banco dos testes: o do CI, ou um PostgreSQL local na porta 55433. */
export const Banco =
  process.env.E2E_BANCO ??
  'Host=localhost;Port=55433;Database=planejamento_obras_e2e;Username=postgres;Password=postgres';
