/*
 * Onde a pilha dos testes de ponta a ponta roda. Por padrão, a que o próprio
 * Playwright sobe, em portas próprias — 5081 e 5181 —, para não esbarrar na
 * API e no front-end de desenvolvimento.
 *
 * Contra a pilha do Compose (ADR-0020), E2E_WEB diz onde ela está, e a API
 * vem pela mesma origem, pelo nginx, como em produção.
 */

const Externa = process.env.E2E_WEB;

export const Web = Externa ?? 'http://localhost:5181';

export const Api = Externa ?? 'http://localhost:5081';

/** O banco dos testes: o do CI, ou um PostgreSQL local na porta 55433. */
export const Banco =
  process.env.E2E_BANCO ??
  'Host=localhost;Port=55433;Database=planejamento_obras_e2e;Username=postgres;Password=postgres';
