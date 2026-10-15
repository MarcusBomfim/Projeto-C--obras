import { describe, expect, test } from 'vitest';
import { valorAgregadoDoReadme } from '../testes/exemplos';
import { montarCurva, passoRedondo, type Dimensoes } from './curva';

const Tamanho: Dimensoes = {
  largura: 400,
  altura: 230,
  margem: { topo: 10, direita: 20, base: 20, esquerda: 80 },
};

describe('a geometria da curva S', () => {
  test('o passo do eixo é redondo: 1, 2, 2,5 ou 5 vezes uma potência de dez', () => {
    expect(passoRedondo(1_388_822.24)).toBe(500_000);
    expect(passoRedondo(1_000)).toBe(200);
    expect(passoRedondo(12)).toBe(2.5);
    expect(passoRedondo(0)).toBe(1);
  });

  test('a obra do README: eixo até R$ 1,5 milhão em passos de R$ 500 mil', () => {
    const curva = montarCurva(valorAgregadoDoReadme.curva, Tamanho);

    expect(curva.teto).toBe(1_500_000);
    expect(curva.marcasDoValor.map((marca) => [marca.valor, marca.y])).toEqual([
      [0, 210],
      [500_000, 143.3],
      [1_000_000, 76.7],
      [1_500_000, 10],
    ]);
  });

  test('os meses se espalham de uma ponta à outra; VA e CR param no mês medido', () => {
    const curva = montarCurva(valorAgregadoDoReadme.curva, Tamanho);
    const [planejado, agregado, real] = curva.series;

    expect(curva.marcasDoMes.map((marca) => marca.x)).toEqual([80, 180, 280, 380]);
    expect(planejado?.pontos).toHaveLength(4);
    expect(agregado?.pontos.map((ponto) => ponto.mes)).toEqual(['2026-10-01', '2026-11-01']);
    expect(real?.pontos.map((ponto) => ponto.valor)).toEqual([172_200, 432_700]);
    expect(agregado?.caminho).toBe('M80,188.1 L180,154.4');
  });

  test('um mês só fica no meio, e uma obra sem valor não divide por zero', () => {
    const curva = montarCurva(
      [{ mes: '2026-10-01', valorPlanejado: 0, valorAgregado: null, custoReal: null }],
      Tamanho,
    );

    expect(curva.marcasDoMes).toEqual([{ mes: '2026-10-01', x: 230 }]);
    expect(curva.teto).toBe(1);
    expect(curva.series[0]?.caminho).toBe('M230,210');
  });
});
