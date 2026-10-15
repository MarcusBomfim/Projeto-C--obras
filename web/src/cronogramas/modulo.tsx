import type { Modulo } from '../aplicacao/modulos';
import { TelaDoCronograma } from './TelaDoCronograma';
import { TelaDosCronogramas } from './TelaDosCronogramas';

export const modulo: Modulo = {
  rotas: [
    { path: 'cronogramas', element: <TelaDosCronogramas /> },
    { path: 'cronogramas/:id', element: <TelaDoCronograma /> },
  ],
  menu: { rotulo: 'Cronogramas', caminho: '/cronogramas', ordem: 2 },
};
