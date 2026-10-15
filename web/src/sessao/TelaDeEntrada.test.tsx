import { act, screen } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, onTestFinished, test, vi } from 'vitest';
import { abrir } from '../testes/abrir';
import { obraDoReadmeNoPainel } from '../testes/exemplos';
import { servidor } from '../testes/servidor';
import { EmpresaDosTestes, tokenDeTeste } from '../testes/tokens';
import { destinoSeguro } from './destino';

/** O painel como a API responde: cada empresa vê só as obras dela. */
const painelDeCadaEmpresa = http.get('/api/painel', ({ request }) => {
  const carga = request.headers.get('Authorization')?.split('.')[1] ?? '';
  const { empresa } = JSON.parse(Buffer.from(carga, 'base64url').toString()) as { empresa: string };

  return HttpResponse.json(empresa === EmpresaDosTestes ? [obraDoReadmeNoPainel] : []);
});

const tokenDaOutraEmpresa = tokenDeTeste({ name: 'Bruno', empresa: '0199e3b4-0000-7000-8000-0000000000b2' });

describe('a entrada', () => {
  test('sem sessão, o endereço pedido manda para a entrada, e entrar volta para ele', async () => {
    servidor.use(http.get('/api/painel', () => HttpResponse.json([])));
    const { roteador } = abrir('/?filtro=criticas', { token: null });

    expect(await screen.findByRole('heading', { name: 'Planejamento de Obras' })).toBeInTheDocument();
    expect(roteador.state.location.pathname).toBe('/entrar');

    await userEvent.type(screen.getByLabelText('Token de acesso (JWT)'), tokenDeTeste());
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByRole('heading', { name: 'Painel das obras' })).toBeInTheDocument();
    expect(roteador.state.location.search).toBe('?filtro=criticas');
    expect(screen.getByText('Ana Engenheira')).toBeInTheDocument();
  });

  test('o token que não serve não entra, e o motivo aparece', async () => {
    abrir('/entrar', { token: null });

    await userEvent.type(
      screen.getByLabelText('Token de acesso (JWT)'),
      tokenDeTeste({ empresa: undefined }),
    );
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(screen.getByRole('alert')).toHaveTextContent('falta a reivindicação empresa');
    expect(screen.getByLabelText('Token de acesso (JWT)')).toHaveAttribute('aria-invalid', 'true');
  });

  test('a API recusar o token leva de volta à entrada, dizendo por quê', async () => {
    servidor.use(
      http.get('/api/painel', () =>
        HttpResponse.json({ title: 'É preciso entrar.', status: 401 }, { status: 401 }),
      ),
    );
    const { roteador } = abrir('/');

    expect(await screen.findByText(/A API recusou o token/)).toBeInTheDocument();
    expect(roteador.state.location.pathname).toBe('/entrar');
    expect(sessionStorage.length).toBe(0);
  });

  test('sair esquece o token e o que a empresa leu, e volta à entrada', async () => {
    servidor.use(http.get('/api/painel', () => HttpResponse.json([obraDoReadmeNoPainel])));
    const { roteador, consultas } = abrir('/');
    expect(await screen.findByText('Linha de base 1 — contrato')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Sair' }));

    expect(roteador.state.location.pathname).toBe('/entrar');
    expect(sessionStorage.length).toBe(0);
    expect(consultas.getQueryCache().getAll()).toHaveLength(0);
  });

  test('o token que vence com a tela aberta leva de volta à entrada, dizendo por quê', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    onTestFinished(() => {
      vi.useRealTimers();
    });
    servidor.use(http.get('/api/painel', () => HttpResponse.json([])));
    const { roteador } = abrir('/', { token: tokenDeTeste({ exp: Math.floor(Date.now() / 1000) + 60 }) });
    expect(await screen.findByText(/Nenhuma obra acompanhada ainda/)).toBeInTheDocument();

    await act(() => vi.advanceTimersByTimeAsync(60_000));

    expect(screen.getByRole('status')).toHaveTextContent('O token venceu. Entre de novo.');
    expect(roteador.state.location.pathname).toBe('/entrar');
    expect(sessionStorage.length).toBe(0);
  });

  test('trocar de sessão esvazia o cache: a outra empresa não vê o que a primeira leu', async () => {
    servidor.use(painelDeCadaEmpresa);
    abrir('/');
    expect(await screen.findByText('Linha de base 1 — contrato')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Sair' }));
    await userEvent.type(screen.getByLabelText('Token de acesso (JWT)'), tokenDaOutraEmpresa);
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByText(/Nenhuma obra acompanhada ainda/)).toBeInTheDocument();
    expect(screen.queryByText('Linha de base 1 — contrato')).not.toBeInTheDocument();
  });

  test('entrar com outro token sem sair antes também esvazia o cache', async () => {
    servidor.use(painelDeCadaEmpresa);
    const { roteador } = abrir('/');
    expect(await screen.findByText('Linha de base 1 — contrato')).toBeInTheDocument();

    await act(() => roteador.navigate('/entrar'));
    await userEvent.type(screen.getByLabelText('Token de acesso (JWT)'), tokenDaOutraEmpresa);
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByText(/Nenhuma obra acompanhada ainda/)).toBeInTheDocument();
    expect(screen.getByText('Bruno')).toBeInTheDocument();
    expect(screen.queryByText('Linha de base 1 — contrato')).not.toBeInTheDocument();
  });

  test('depois de entrar, só volta para dentro da aplicação', () => {
    expect(destinoSeguro('/obras/123?x=1')).toBe('/obras/123?x=1');
    expect(destinoSeguro('//site-de-fora.com')).toBe('/');
    expect(destinoSeguro('/\\site-de-fora.com')).toBe('/');
    expect(destinoSeguro('https://site-de-fora.com')).toBe('/');
    expect(destinoSeguro(null)).toBe('/');
  });
});
