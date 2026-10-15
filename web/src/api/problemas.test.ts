import { describe, expect, test } from 'vitest';
import { comoProblema } from './problemas';

describe('o problema de uma consulta', () => {
  test('vem da API em ProblemDetails, com título e detalhe prontos', () => {
    expect(
      comoProblema({
        type: 'https://github.com/MarcusBomfim/Projeto-C--obras/blob/main/docs/api/problemas.md#nao-encontrado',
        title: 'O recurso não existe.',
        status: 404,
        detail: 'Não existe o acompanhamento 0199.',
      }),
    ).toEqual({ titulo: 'O recurso não existe.', detalhe: 'Não existe o acompanhamento 0199.', status: 404 });
  });

  test('a falha antes da resposta é a API fora do alcance', () => {
    expect(comoProblema(new TypeError('Failed to fetch')).titulo).toBe('Não foi possível falar com a API.');
  });

  test('o resto é um problema sem nome, com o que se souber', () => {
    expect(comoProblema(new Error('inesperado'))).toEqual({
      titulo: 'Algo deu errado.',
      detalhe: 'inesperado',
    });
    expect(comoProblema('texto')).toEqual({ titulo: 'Algo deu errado.', detalhe: undefined });
  });
});
