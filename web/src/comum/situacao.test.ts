import { describe, expect, test } from 'vitest';
import { situacaoDoIndice } from './situacao';

describe('a cor de um índice', () => {
  test('a régua da API: abaixo de 0,85 é crítico, abaixo de 0,95 pede atenção', () => {
    expect(situacaoDoIndice(0.84)).toBe('Critica');
    expect(situacaoDoIndice(0.85)).toBe('Atencao');
    expect(situacaoDoIndice(0.94)).toBe('Atencao');
    expect(situacaoDoIndice(0.95)).toBe('EmDia');
    expect(situacaoDoIndice(1.12)).toBe('EmDia');
  });

  test('o índice sem base não tem cor', () => {
    expect(situacaoDoIndice(null)).toBe('SemMedicao');
  });
});
