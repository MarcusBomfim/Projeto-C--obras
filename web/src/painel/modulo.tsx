import type { Modulo } from '../aplicacao/modulos';
import { TelaDoPainel } from './TelaDoPainel';

export const modulo: Modulo = {
  rotas: [{ index: true, element: <TelaDoPainel /> }],
  menu: { rotulo: 'Painel', caminho: '/', ordem: 1 },
};
