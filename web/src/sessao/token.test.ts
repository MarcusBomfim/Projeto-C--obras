import { describe, expect, test } from 'vitest';
import { EmpresaDosTestes, tokenDeTeste } from '../testes/tokens';
import { lerSessao, type LeituraDoToken, type Sessao } from './token';

function sessao(leitura: LeituraDoToken): Sessao {
  if ('motivo' in leitura) {
    throw new Error(`Esperava uma sessão, e veio: ${leitura.motivo}`);
  }

  return leitura.sessao;
}

function motivo(leitura: LeituraDoToken): string {
  if ('sessao' in leitura) {
    throw new Error('Esperava a recusa do token.');
  }

  return leitura.motivo;
}

describe('o token de quem entra', () => {
  test('diz o nome, a empresa, os papéis e até quando vale', () => {
    const lida = sessao(lerSessao(tokenDeTeste({ exp: 1_893_456_000, role: ['gestor', 'engenheiro'] })));

    expect(lida.nome).toBe('Ana Engenheira');
    expect(lida.empresa).toBe(EmpresaDosTestes);
    expect(lida.papeis).toEqual(['gestor', 'engenheiro']);
    expect(lida.venceEm).toEqual(new Date(1_893_456_000 * 1000));
  });

  test('aceita o papel único em texto e o token com "Bearer" na frente', () => {
    const token = tokenDeTeste({ role: 'gestor', name: undefined, unique_name: 'bruno' });
    const lida = sessao(lerSessao(`Bearer ${token}\n`));

    expect(lida.papeis).toEqual(['gestor']);
    expect(lida.nome).toBe('bruno');
    expect(lida.token).toBe(token);
  });

  test('recusa o que não é JWT, o que não diz a empresa e o vencido', () => {
    const agora = new Date('2026-10-15T12:00:00Z');

    expect(motivo(lerSessao('não-é-token'))).toContain('três partes');
    expect(motivo(lerSessao(tokenDeTeste({ empresa: undefined })))).toContain('empresa');
    expect(motivo(lerSessao(tokenDeTeste({ exp: agora.getTime() / 1000 - 1 }), agora))).toContain('venceu');
  });

  test('vale até o segundo antes do exp: no exp, já venceu (RFC 7519)', () => {
    const agora = new Date('2026-10-15T12:00:00Z');
    const exp = agora.getTime() / 1000;

    expect(motivo(lerSessao(tokenDeTeste({ exp }), agora))).toContain('venceu');
    expect(sessao(lerSessao(tokenDeTeste({ exp: exp + 1 }), agora)).venceEm).toEqual(
      new Date((exp + 1) * 1000),
    );
  });
});
