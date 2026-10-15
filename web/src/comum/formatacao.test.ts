import { describe, expect, test } from 'vitest';
import {
  formatarData,
  formatarIndice,
  formatarMes,
  formatarMeses,
  formatarMoeda,
  formatarMoedaCompacta,
  formatarPercentual,
  lerData,
} from './formatacao';

// O Intl separa o símbolo da moeda com um espaço que não quebra.
const r$ = 'R$\u00a0';

describe('formatação', () => {
  test('dinheiro em reais, com centavos, e o traço quando não há valor', () => {
    expect(formatarMoeda(1_442_159.13)).toBe(`${r$}1.442.159,13`);
    expect(formatarMoeda(-53_336.89)).toBe(`-${r$}53.336,89`);
    expect(formatarMoeda(null)).toBe('—');
    expect(formatarMoedaCompacta(1_388_822.24)).toBe(`${r$}1,4\u00a0mi`);
  });

  test('índice com duas casas, meses e porcentagem', () => {
    expect(formatarIndice(0.7389872680284619)).toBe('0,74');
    expect(formatarIndice(null)).toBe('—');
    expect(formatarMeses(1.6116150977402737)).toBe('1,61 meses');
    expect(formatarMeses(1)).toBe('1,00 mês');
    expect(formatarPercentual(0.3000362378989553)).toBe('30,0%');
  });

  test('mês abreviado como a obra escreve, e maio por extenso', () => {
    expect(formatarMes('2026-11-01')).toBe('nov./2026');
    expect(formatarMes('2027-05-01')).toBe('maio/2027');
    expect(formatarMes(null)).toBe('—');
  });

  test('a data da API é a data, e não a meia-noite de UTC', () => {
    // Em UTC−3, new Date('2026-10-01') é 30 de setembro às 21h.
    const data = lerData('2026-10-01');

    expect([data.getFullYear(), data.getMonth(), data.getDate()]).toEqual([2026, 9, 1]);
    expect(formatarData('2026-10-05')).toBe('05/10/2026');
    expect(() => lerData('05/10/2026')).toThrow('aaaa-mm-dd');
  });
});
