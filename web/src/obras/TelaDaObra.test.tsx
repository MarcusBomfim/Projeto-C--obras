import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, test } from 'vitest';
import { abrir } from '../testes/abrir';
import { obraDoReadmeNoPainel, valorAgregadoDoReadme } from '../testes/exemplos';
import { servidor } from '../testes/servidor';

// O espaço que não quebra, do Intl, a Testing Library compara como espaço comum.
const id = obraDoReadmeNoPainel.acompanhamento;

describe('a obra', () => {
  test('a obra do README: índices, estimativas, a curva S e a tabela por etapa', async () => {
    servidor.use(
      http.get(`/api/painel/${id}`, () => HttpResponse.json(obraDoReadmeNoPainel)),
      http.get(`/api/acompanhamentos/${id}/valor-agregado`, () => HttpResponse.json(valorAgregadoDoReadme)),
    );
    abrir(`/obras/${id}`);

    expect(await screen.findByText(/até a medição 2 — nov.\/2026/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Linha de base 1 — contrato');

    // O nome do indicador também está na legenda e na tabela da curva: o do cartão é o termo da lista.
    const indicador = (nome: string) =>
      screen.getAllByText(nome).find((elemento) => elemento.tagName === 'DT')?.nextElementSibling;
    expect(indicador('IDP — desempenho de prazo')).toHaveTextContent('0,74');
    expect(indicador('IDC — desempenho de custo')).toHaveTextContent('0,96');
    expect(indicador('Prazo agregado')).toHaveTextContent('1,61 meses de 2 decorridos');
    expect(indicador('Duração estimada')).toHaveTextContent('4,96 meses de 4 planejados');
    expect(indicador('Valor planejado (VP)')).toHaveTextContent('R$ 563.875,75');
    expect(indicador('Valor agregado (VA)')).toHaveTextContent('R$ 416.697,00');
    expect(indicador('Custo real (CR)')).toHaveTextContent('R$ 432.700,00');
    expect(indicador('Estimativa no término (ENT)')).toHaveTextContent('R$ 1.442.159,13');
    expect(indicador('Variação no término (VNT)')).toHaveTextContent('-R$ 53.336,89');

    const curva = screen.getByRole('figure', { name: /Curva S/ });
    const novembro = within(curva).getByRole('row', { name: /nov.\/2026/ });
    expect(novembro).toHaveTextContent('R$ 563.875,75R$ 416.697,00R$ 432.700,00');
    expect(within(curva).getByRole('row', { name: /jan.\/2027/ })).toHaveTextContent('——');

    const etapas = screen.getByRole('table', { name: 'Por etapa e item' });
    expect(within(etapas).getAllByRole('row')).toHaveLength(valorAgregadoDoReadme.linhas.length + 1);
    expect(within(etapas).getByRole('row', { name: /^4 Instalações e acabamentos/ })).toHaveTextContent(
      '0,40',
    );
  });

  test('a obra ainda sem medição nem pede o valor agregado', async () => {
    let pedidosDoValorAgregado = 0;
    servidor.use(
      http.get(`/api/painel/${id}`, () =>
        HttpResponse.json({ ...obraDoReadmeNoPainel, situacao: 'SemMedicao', medicao: null, mes: null }),
      ),
      http.get(`/api/acompanhamentos/${id}/valor-agregado`, () => {
        pedidosDoValorAgregado += 1;

        return HttpResponse.json(valorAgregadoDoReadme);
      }),
    );
    abrir(`/obras/${id}`);

    expect(await screen.findByText(/ainda não tem medição/)).toBeInTheDocument();
    expect(screen.getByText('Sem medição')).toBeInTheDocument();
    expect(pedidosDoValorAgregado).toBe(0);
  });

  test('a obra que ainda não está no painel mostra o motivo da API', async () => {
    servidor.use(
      http.get(`/api/painel/${id}`, () =>
        HttpResponse.json(
          {
            title: 'O recurso não existe.',
            detail: `O acompanhamento ${id} não está no painel. Se acabou de ser criado, o painel ainda não chegou nele.`,
            status: 404,
          },
          { status: 404 },
        ),
      ),
    );
    abrir(`/obras/${id}`);

    expect(await screen.findByRole('alert')).toHaveTextContent('o painel ainda não chegou nele');
  });
});
