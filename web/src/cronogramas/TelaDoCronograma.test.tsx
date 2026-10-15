import { screen, within } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, test } from 'vitest';
import { abrir } from '../testes/abrir';
import { cronogramaDoReadme, cronogramasDoReadme, programacaoDoReadme } from '../testes/exemplosDoCronograma';
import { servidor } from '../testes/servidor';

const id = cronogramaDoReadme.id;

/** Responde com o cronograma do README e guarda o que a tela pediu na programação. */
function responder() {
  const pedidos: URLSearchParams[] = [];

  servidor.use(
    http.get(`/api/cronogramas/${id}`, () => HttpResponse.json(cronogramaDoReadme)),
    http.get(`/api/cronogramas/${id}/programacao`, ({ request }) => {
      pedidos.push(new URL(request.url).searchParams);

      return HttpResponse.json(programacaoDoReadme);
    }),
  );

  return pedidos;
}

describe('o cronograma', () => {
  test('programa com o calendário do endereço e mostra o Gantt, o resumo e a tabela', async () => {
    const pedidos = responder();
    abrir(`/cronogramas/${id}?inicio=2026-10-05&feriados=2026-10-12,2026-11-02`);

    expect(await screen.findByText(/64 dias úteis/)).toHaveTextContent(
      'De 05/10/2026 a 07/01/2027, 64 dias úteis. Atividades críticas: MOB, FUN, EST, ALV, REB, PIN e ENT.',
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Residencial Vista Serra');
    expect(screen.getByLabelText('Feriados')).toHaveValue('12/10/2026, 02/11/2026');

    const [pedido] = pedidos;
    expect(pedido?.get('inicio')).toBe('2026-10-05');
    expect(pedido?.getAll('feriados')).toEqual(['2026-10-12', '2026-11-02']);
    expect(pedido?.get('sabadoUtil')).toBe('false');

    const tabela = screen.getByRole('table', { name: 'Atividades do cronograma, com as datas cedo e tarde' });
    expect(within(tabela).getAllByRole('row')).toHaveLength(9);
    expect(within(tabela).getByRole('row', { name: /^ELE/ })).toHaveTextContent(
      'qui. 26/11sex. 11/12ter. 01/12qua. 16/1233—',
    );
    expect(within(tabela).getByRole('row', { name: /^ENT/ })).toHaveTextContent('marco');
  });

  test('mudar o calendário muda o endereço e programa de novo', async () => {
    const pedidos = responder();
    const { roteador } = abrir(`/cronogramas/${id}?inicio=2026-10-05`);
    await screen.findByText(/64 dias úteis/);

    await userEvent.type(screen.getByLabelText('Feriados'), '12/10/2026, 02/11/2026');
    await userEvent.click(screen.getByLabelText('Sábado é dia útil'));
    await userEvent.click(screen.getByRole('button', { name: 'Programar' }));

    await screen.findByText(/64 dias úteis/);
    expect(roteador.state.location.search).toBe(
      '?inicio=2026-10-05&feriados=2026-10-12%2C2026-11-02&sabado=util',
    );
    expect(pedidos.at(-1)?.getAll('feriados')).toEqual(['2026-10-12', '2026-11-02']);
    expect(pedidos.at(-1)?.get('sabadoUtil')).toBe('true');
  });

  test('feriado que não é data é apontado, e nada é programado, nem com os que são', async () => {
    const pedidos = responder();
    const { roteador } = abrir(`/cronogramas/${id}?inicio=2026-10-05`);
    await screen.findByText(/64 dias úteis/);

    await userEvent.type(screen.getByLabelText('Feriados'), '12/10/2026, 31/09/2026');
    await userEvent.click(screen.getByRole('button', { name: 'Programar' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Não são datas: 31/09/2026.');
    expect(roteador.state.location.search).toBe('?inicio=2026-10-05');
    expect(pedidos).toHaveLength(1);
  });

  test('a lista leva ao cronograma, e a área entra sozinha no menu', async () => {
    servidor.use(http.get('/api/cronogramas', () => HttpResponse.json(cronogramasDoReadme)));
    abrir('/cronogramas');

    expect(await screen.findByRole('link', { name: 'Residencial Vista Serra' })).toHaveAttribute(
      'href',
      `/cronogramas/${id}`,
    );
    expect(
      within(screen.getByRole('navigation', { name: 'Principal' }))
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(['Painel', 'Cronogramas']);
  });

  test('o cronograma que não existe mostra o motivo da API', async () => {
    const problema = {
      title: 'O recurso não existe.',
      detail: `Não existe o cronograma ${id}.`,
      status: 404,
    };
    servidor.use(
      http.get(`/api/cronogramas/${id}`, () => HttpResponse.json(problema, { status: 404 })),
      http.get(`/api/cronogramas/${id}/programacao`, () => HttpResponse.json(problema, { status: 404 })),
    );
    abrir(`/cronogramas/${id}?inicio=2026-10-05`);

    expect((await screen.findAllByRole('alert'))[0]).toHaveTextContent(`Não existe o cronograma ${id}.`);
  });
});
