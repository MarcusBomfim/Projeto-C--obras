import { parseArgs } from 'node:util';
import { emitirToken } from './identidade.ts';

/*
 * Um token para entrar pelo navegador na pilha do Compose, que aceita a
 * chave dos testes (ADR-0020). Roda com o Node 24, sem instalar nada:
 *
 *   node e2e/apoio/token.ts --empresa 0199e3b4-0000-7000-8000-000000000001 --papel gestor
 *
 * Sem --papel, vale para todos os papéis; sem --empresa, para a empresa dos
 * exemplos.
 */

const { values } = parseArgs({
  options: {
    empresa: { type: 'string', default: '0199e3b4-0000-7000-8000-000000000001' },
    nome: { type: 'string', default: 'Pessoa de teste' },
    papel: {
      type: 'string',
      multiple: true,
      default: ['planejador', 'orcamentista', 'gestor', 'engenheiro'],
    },
  },
});

console.log(emitirToken({ empresa: values.empresa, nome: values.nome, papeis: values.papel }));
