/*
 * Os números e as datas como a obra os escreve: real com centavos, índice
 * com duas casas, mês abreviado — "nov./2026". O que a API manda sem valor
 * (o índice sem base, a medição que ainda não houve) aparece como um traço.
 *
 * Data sem hora — o DateOnly da API, "2026-11-01" — é lida como data, e não
 * como meia-noite em UTC: no Brasil, isso cairia no dia anterior.
 */

const SemValor = '—';

const Moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

const MoedaCompacta = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  notation: 'compact',
  maximumFractionDigits: 1,
});

const DuasCasas = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const Percentual = new Intl.NumberFormat('pt-BR', {
  style: 'percent',
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
});

const Data = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' });

const DataEHora = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

const Meses = [
  'jan.',
  'fev.',
  'mar.',
  'abr.',
  'maio',
  'jun.',
  'jul.',
  'ago.',
  'set.',
  'out.',
  'nov.',
  'dez.',
] as const;

/** "2026-11-01" — a data como a API manda — numa data local, sem fuso no meio. */
export function lerData(texto: string): Date {
  const partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec(texto);

  if (!partes) {
    throw new Error(`Data fora do formato aaaa-mm-dd: ${texto}`);
  }

  return new Date(Number(partes[1]), Number(partes[2]) - 1, Number(partes[3]));
}

export function formatarMoeda(valor: number | null): string {
  return valor === null ? SemValor : Moeda.format(valor);
}

/** R$ 1,4 mi: para eixos de gráfico, onde os centavos só atrapalham. */
export function formatarMoedaCompacta(valor: number): string {
  return MoedaCompacta.format(valor);
}

/** Índice de desempenho com duas casas: 0,74. */
export function formatarIndice(valor: number | null): string {
  return valor === null ? SemValor : DuasCasas.format(valor);
}

export function formatarMeses(valor: number | null): string {
  return valor === null ? SemValor : `${DuasCasas.format(valor)} ${valor === 1 ? 'mês' : 'meses'}`;
}

/** Uma fração como porcentagem: 0,306 → 30,6 %. */
export function formatarPercentual(fracao: number): string {
  return Percentual.format(fracao);
}

/** "2026-11-01" → "nov./2026". */
export function formatarMes(texto: string | null): string {
  if (texto === null) {
    return SemValor;
  }

  const data = lerData(texto);

  return `${Meses[data.getMonth()] ?? ''}/${data.getFullYear()}`;
}

/** "2026-10-05" → "05/10/2026". */
export function formatarData(texto: string): string {
  return Data.format(lerData(texto));
}

/** Um instante da API — com fuso — na hora de quem lê: "15/10/2026, 01:21". */
export function formatarDataEHora(texto: string): string {
  return DataEHora.format(new Date(texto));
}
