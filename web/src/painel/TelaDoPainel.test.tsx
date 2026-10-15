import { act, screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, onTestFinished, test, vi } from 'vitest';
import type { Esquemas } from '../api/cliente';
import { abrir } from '../testes/abrir';
import { obraDoReadmeNoPainel } from '../testes/exemplos';
import { servidor } from '../testes/servidor';
import { IntervaloDeAtualizacao } from './TelaDoPainel';

// O Intl separa o R$ do número com um espaço que não quebra; a Testing
// Library o troca por espaço comum antes de comparar o texto da tela.

type ObraNoPainel = Esquemas['ObraNoPainelResposta'];

const obraEmDia: ObraNoPainel = {
  ...obraDoReadmeNoPainel,
  acompanhamento: '0199e3b4-0000-7000-8000-0000000000e1',
  obra: 'Anexo administrativo',
  situacao: 'EmDia',
  indiceDeDesempenhoDePrazo: 1.02,
  indiceDeDesempenhoDeCusto: 0.98,
  variacaoNoTermino: 12_000,
};

const obraSemMedicao: ObraNoPainel = {
  ...obraDoReadmeNoPainel,
  acompanhamento: '0199e3b4-0000-7000-8000-0000000000e2',
  obra: 'Casa térrea',
  situacao: 'SemMedicao',
  medicao: null,
  mes: null,
  valorPlanejado: null,
  valorAgregado: null,
  custoReal: null,
  indiceDeDesempenhoDePrazo: null,
  indiceDeDesempenhoDeCusto: null,
  estimativaNoTermino: null,
  variacaoNoTermino: null,
  prazoAgregadoEmMeses: null,
  duracaoEstimadaEmMeses: null,
  versaoDoAcompanhamento: 1,
};

const outraObraCritica: ObraNoPainel = {
  ...obraDoReadmeNoPainel,
  acompanhamento: '0199e3b4-0000-7000-8000-0000000000e3',
  obra: 'Galpão logístico',
  indiceDeDesempenhoDePrazo: 0.81,
  indiceDeDesempenhoDeCusto: 0.9,
};

function responder(obras: readonly ObraNoPainel[]) {
  servidor.use(http.get('/api/painel', () => HttpResponse.json(obras)));
}

describe('o painel das obras', () => {
  test('mostra cada obra com a situação e os índices: a que pede ação primeiro e, empatadas, pelo nome', async () => {
    responder([obraEmDia, obraSemMedicao, obraDoReadmeNoPainel, outraObraCritica]);
    abrir('/');

    const cartoes = await screen.findAllByRole('article');

    expect(cartoes.map((cartao) => within(cartao).getByRole('heading').textContent)).toEqual([
      'Galpão logístico',
      'Linha de base 1 — contrato',
      'Anexo administrativo',
      'Casa térrea',
    ]);

    const doReadme = within(cartoes[1] ?? document.body);
    expect(doReadme.getByText('Crítica')).toBeInTheDocument();
    expect(doReadme.getByText('Medição 2 · nov./2026')).toBeInTheDocument();
    expect(doReadme.getByText('0,74')).toBeInTheDocument();
    expect(doReadme.getByText('0,96')).toBeInTheDocument();
    expect(doReadme.getByText('-R$ 53.336,89')).toBeInTheDocument();
    expect(doReadme.getByRole('link')).toHaveAttribute(
      'href',
      `/obras/${obraDoReadmeNoPainel.acompanhamento}`,
    );

    expect(within(cartoes[3] ?? document.body).getByText('Ainda sem medição.')).toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Obras por situação' })).toHaveTextContent(
      '2 críticas1 em dia1 sem medição',
    );
  });

  test('pergunta de novo de tempos em tempos: a medição que o processador projetou aparece sozinha', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    onTestFinished(() => {
      vi.useRealTimers();
    });
    let pedidos = 0;
    servidor.use(
      http.get('/api/painel', () => {
        pedidos += 1;

        return HttpResponse.json([
          pedidos === 1 ? obraDoReadmeNoPainel : { ...obraDoReadmeNoPainel, medicao: 3, mes: '2026-12-01' },
        ]);
      }),
    );
    abrir('/');
    expect(await screen.findByText('Medição 2 · nov./2026')).toBeInTheDocument();

    await act(() => vi.advanceTimersByTimeAsync(IntervaloDeAtualizacao));

    expect(await screen.findByText('Medição 3 · dez./2026')).toBeInTheDocument();
  });

  test('pede o painel com o token de quem entrou', async () => {
    let autorizacao: string | null = null;
    servidor.use(
      http.get('/api/painel', ({ request }) => {
        autorizacao = request.headers.get('Authorization');

        return HttpResponse.json([]);
      }),
    );
    abrir('/');

    expect(await screen.findByText(/Nenhuma obra acompanhada ainda/)).toBeInTheDocument();
    expect(autorizacao).toMatch(/^Bearer .+\..+\..+$/);
  });

  test('o problema da API aparece como ela explicou', async () => {
    servidor.use(
      http.get('/api/painel', () =>
        HttpResponse.json(
          {
            title: 'Erro inesperado no servidor.',
            detail: 'Informe o código 00-4bf9 ao suporte.',
            status: 500,
          },
          { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    abrir('/');

    const aviso = await screen.findByRole('alert');

    expect(aviso).toHaveTextContent('Erro inesperado no servidor.');
    expect(aviso).toHaveTextContent('Informe o código 00-4bf9 ao suporte.');
  });

  test('sem a API no ar, diz isso em vez de ficar carregando', async () => {
    servidor.use(http.get('/api/painel', () => HttpResponse.error()));
    abrir('/');

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível falar com a API.');
  });
});
