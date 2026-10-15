import type { Modulo } from '../aplicacao/modulos';
import { TelaDaObra } from './TelaDaObra';

/** A obra, aberta a partir do painel — sem entrada no menu. */
export const modulo: Modulo = {
  rotas: [{ path: 'obras/:acompanhamento', element: <TelaDaObra /> }],
};
