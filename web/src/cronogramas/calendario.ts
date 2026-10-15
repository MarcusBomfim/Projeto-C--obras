import { lerData } from '../comum/formatacao';

/*
 * O calendário da programação mora no endereço da página — início, feriados,
 * sábado útil —: quem manda o link manda a mesma programação. Sem início no
 * endereço, a obra começa hoje.
 */

export interface ParametrosDaProgramacao {
  readonly inicio: string;
  readonly feriados: readonly string[];
  readonly sabadoUtil: boolean;
}

const Iso = /^\d{4}-\d{2}-\d{2}$/;

const Brasileira = /^(\d{2})\/(\d{2})\/(\d{4})$/;

const DiasDaSemana = ['dom.', 'seg.', 'ter.', 'qua.', 'qui.', 'sex.', 'sáb.'] as const;

/** Uma data local escrita como a API escreve: "aaaa-mm-dd". */
export function emIso(data: Date): string {
  const mes = String(data.getMonth() + 1).padStart(2, '0');
  const dia = String(data.getDate()).padStart(2, '0');

  return `${data.getFullYear()}-${mes}-${dia}`;
}

/** Hoje, no fuso de quem usa. */
export function hojeEmIso(): string {
  return emIso(new Date());
}

/** Se a data existe: 30/02 vira 02/03 na ida, e não volta igual. */
function existe(iso: string): boolean {
  return Iso.test(iso) && emIso(lerData(iso)) === iso;
}

/** Os feriados digitados — "12/10/2026" ou "2026-10-12", separados por vírgula, espaço ou linha. */
export function lerFeriados(texto: string): { readonly datas: string[]; readonly invalidas: string[] } {
  const datas: string[] = [];
  const invalidas: string[] = [];

  for (const parte of texto.split(/[\s,;]+/).filter((parte) => parte !== '')) {
    const brasileira = Brasileira.exec(parte);
    const iso = brasileira ? `${brasileira[3] ?? ''}-${brasileira[2] ?? ''}-${brasileira[1] ?? ''}` : parte;

    if (existe(iso)) {
      datas.push(iso);
    } else {
      invalidas.push(parte);
    }
  }

  return { datas: [...new Set(datas)].sort(), invalidas };
}

export function lerParametros(endereco: URLSearchParams, hoje: string): ParametrosDaProgramacao {
  const inicio = endereco.get('inicio');

  return {
    inicio: inicio && existe(inicio) ? inicio : hoje,
    feriados: lerFeriados(endereco.get('feriados') ?? '').datas,
    sabadoUtil: endereco.get('sabado') === 'util',
  };
}

export function escreverParametros(parametros: ParametrosDaProgramacao): URLSearchParams {
  const endereco = new URLSearchParams({ inicio: parametros.inicio });

  if (parametros.feriados.length > 0) {
    endereco.set('feriados', parametros.feriados.join(','));
  }

  if (parametros.sabadoUtil) {
    endereco.set('sabado', 'util');
  }

  return endereco;
}

/** "2026-10-05" → "seg. 05/10", como o planejamento escreve no cronograma. */
export function formatarDiaDaObra(texto: string): string {
  const data = lerData(texto);
  const dia = String(data.getDate()).padStart(2, '0');
  const mes = String(data.getMonth() + 1).padStart(2, '0');

  return `${DiasDaSemana[data.getDay()] ?? ''} ${dia}/${mes}`;
}
