import { describe, expect, test } from 'vitest';
import { escreverParametros, formatarDiaDaObra, lerFeriados, lerParametros } from './calendario';

describe('o calendário da programação', () => {
  test('feriados em dd/mm/aaaa ou aaaa-mm-dd, em ordem e sem repetir', () => {
    expect(lerFeriados('02/11/2026, 12/10/2026;2026-11-02\n20/11/2026')).toEqual({
      datas: ['2026-10-12', '2026-11-02', '2026-11-20'],
      invalidas: [],
    });
  });

  test('o que não é data — ou é uma data que não existe — fica de fora, para a tela avisar', () => {
    expect(lerFeriados('30/02/2026, amanhã, 2026-13-01')).toEqual({
      datas: [],
      invalidas: ['30/02/2026', 'amanhã', '2026-13-01'],
    });
  });

  test('o endereço guarda o calendário, e sem início a obra começa hoje', () => {
    const parametros = { inicio: '2026-10-05', feriados: ['2026-10-12', '2026-11-02'], sabadoUtil: true };
    const endereco = escreverParametros(parametros);

    expect(endereco.toString()).toBe('inicio=2026-10-05&feriados=2026-10-12%2C2026-11-02&sabado=util');
    expect(lerParametros(endereco, '2026-10-15')).toEqual(parametros);
    expect(lerParametros(new URLSearchParams('inicio=ontem'), '2026-10-15')).toEqual({
      inicio: '2026-10-15',
      feriados: [],
      sabadoUtil: false,
    });
  });

  test('o dia da obra com o dia da semana, como no cronograma impresso', () => {
    expect(formatarDiaDaObra('2026-10-05')).toBe('seg. 05/10');
    expect(formatarDiaDaObra('2027-01-07')).toBe('qui. 07/01');
  });
});
