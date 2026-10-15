import { expect, request, test, type Page } from '@playwright/test';
import { esperarPainel, medir } from '../apoio/api';
import { lerObra } from '../apoio/estado';
import { Api } from '../apoio/pilha';

/*
 * O exemplo do README no navegador, contra a pilha inteira: o banco, a API,
 * o processador e o front-end de produção. Cada teste entra pela tela de
 * entrada, como uma pessoa entraria.
 */

test.describe.configure({ mode: 'serial' });

const FeriadosDoReadme = '2026-10-12,2026-11-02,2026-11-20,2026-12-25,2027-01-01';

async function entrar(page: Page, endereco: string) {
  await page.goto(endereco);
  await expect(page).toHaveURL(/\/entrar/);

  await page.getByLabel('Token de acesso (JWT)').fill(lerObra().token);
  await page.getByRole('button', { name: 'Entrar' }).click();
}

test('do token ao painel, e do painel à curva S da obra', async ({ page }) => {
  await entrar(page, '/');

  const cartao = page.getByRole('article').filter({ hasText: 'Linha de base 1 — contrato' });
  await expect(cartao).toContainText('Crítica');
  await expect(cartao).toContainText('Medição 2 · nov./2026');
  await expect(cartao.getByText('0,74')).toBeVisible();
  await expect(cartao.getByText('0,96')).toBeVisible();

  await cartao.getByRole('link', { name: 'Linha de base 1 — contrato' }).click();

  await expect(page.getByText('Valor agregado a custo, até a medição 2 — nov./2026.')).toBeVisible();
  await expect(page.getByText(/R\$\s1\.442\.159,13/)).toBeVisible();
  const curva = page.getByRole('figure', { name: /Curva S/ });
  await expect(curva.locator('svg path')).toHaveCount(3);
  await expect(curva.getByRole('row', { name: /nov\.\/2026/ })).toContainText(/R\$\s416\.697,00/);
  await expect(page.getByRole('row', { name: /^4 Instalações e acabamentos/ })).toContainText('0,40');
});

test('o Gantt do cronograma do README, com o caminho crítico', async ({ page }) => {
  const { cronograma } = lerObra();
  await entrar(page, `/cronogramas/${cronograma}?inicio=2026-10-05&feriados=${FeriadosDoReadme}`);

  await expect(
    page.getByText(
      'De 05/10/2026 a 07/01/2027, 64 dias úteis. Atividades críticas: MOB, FUN, EST, ALV, REB, PIN e ENT.',
    ),
  ).toBeVisible();
  await expect(page.getByRole('row', { name: /^ELE/ })).toContainText('qui. 26/11');
  await expect(page.getByRole('row', { name: /^ELE/ })).toContainText('qua. 16/12');

  const gantt = page.getByRole('figure', { name: /Gantt/ });
  await expect(gantt.locator('svg rect[rx]')).toHaveCount(7);
  await expect(gantt.locator('svg path[marker-end]')).toHaveCount(8);
});

test('a medição nova chega ao painel aberto, pela caixa de saída e pelo processador', async ({ page }) => {
  const { token, acompanhamento } = lerObra();
  await entrar(page, '/');
  const cartao = page.getByRole('article').filter({ hasText: 'Linha de base 1 — contrato' });
  await expect(cartao).toContainText('Medição 2 · nov./2026');

  const api = await request.newContext({
    baseURL: Api,
    extraHTTPHeaders: { Authorization: `Bearer ${token}` },
  });
  await medir(api, acompanhamento, 3, {
    mes: '2026-12-30',
    lancamentos: [{ item: '3.1', quantidade: 500, custoReal: 30_000 }],
  });
  await esperarPainel(api, acompanhamento, 4);
  await api.dispose();

  // Sem recarregar: o painel pergunta de novo sozinho, a cada 30 segundos.
  await expect(cartao).toContainText('Medição 3 · dez./2026', { timeout: 45_000 });
});
