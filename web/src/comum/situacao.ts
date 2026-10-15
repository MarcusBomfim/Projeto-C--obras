import type { Esquemas } from '../api/cliente';

export type SituacaoDaObra = Esquemas['SituacaoDaObra'];

/** Como cada situação se escreve — o selo nunca é só cor. */
export const NomesDasSituacoes: Readonly<Record<SituacaoDaObra, string>> = {
  SemMedicao: 'Sem medição',
  EmDia: 'Em dia',
  Atencao: 'Atenção',
  Critica: 'Crítica',
};

/** A ordem do painel: o que pede ação vem primeiro. */
export const OrdemDasSituacoes: readonly SituacaoDaObra[] = ['Critica', 'Atencao', 'EmDia', 'SemMedicao'];

/**
 * A mesma régua da API (ClassificacaoDaObra): abaixo de 0,85 é crítico,
 * abaixo de 0,95 pede atenção. Aqui, para colorir um índice sozinho.
 */
export function situacaoDoIndice(indice: number | null): SituacaoDaObra {
  if (indice === null) {
    return 'SemMedicao';
  }

  if (indice < 0.85) {
    return 'Critica';
  }

  return indice < 0.95 ? 'Atencao' : 'EmDia';
}
