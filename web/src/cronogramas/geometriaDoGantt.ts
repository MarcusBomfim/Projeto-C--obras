import type { Esquemas } from '../api/cliente';
import { formatarMes } from '../comum/formatacao';

/*
 * A geometria do Gantt, sem desenho: cada dia do calendário é uma coluna de
 * largura fixa, e cada atividade, uma linha. As datas da API são inclusivas
 * — a atividade ocupa do começo do primeiro dia ao fim do último — e os dias
 * não úteis ficam dentro da barra, como no canteiro: a alvenaria para no
 * sábado, mas continua sendo a mesma alvenaria.
 *
 * O marco aparece no meio do dia em que a API o põe. O lado dele — começo ou
 * fim do dia (ADR-0003) — a API não diz, e o desenho não inventa.
 */

type Programacao = Esquemas['ProgramacaoResposta'];
type Dependencia = Esquemas['DependenciaResposta'];

export interface Escala {
  readonly larguraDoDia: number;
  readonly alturaDaLinha: number;
  /** A largura da coluna dos nomes, à esquerda do calendário. */
  readonly esquerda: number;
  /** A altura do cabeçalho dos meses e dos dias. */
  readonly topo: number;
  readonly direita: number;
}

export interface Calendario {
  readonly feriados: readonly string[];
  readonly sabadoUtil: boolean;
}

export interface Dia {
  readonly data: string;
  readonly x: number;
  readonly util: boolean;
  readonly segunda: boolean;
}

export interface Mes {
  readonly rotulo: string;
  readonly x: number;
  readonly largura: number;
}

export interface Trecho {
  readonly x: number;
  readonly largura: number;
}

export interface LinhaDoGantt {
  readonly codigo: string;
  readonly descricao: string;
  readonly y: number;
  readonly critica: boolean;
  /** As datas cedo; nula no marco, que não tem duração. */
  readonly barra: Trecho | null;
  /** O losango do marco, no meio do dia em que ele aparece. */
  readonly marco: { readonly x: number } | null;
  /** A folga total: do fim das datas cedo ao fim das datas tarde. */
  readonly folga: Trecho | null;
  readonly inicio: number;
  readonly fim: number;
}

export interface Ligacao {
  readonly predecessora: string;
  readonly sucessora: string;
  readonly tipo: Dependencia['tipo'];
  readonly critica: boolean;
  /** O traço da seta, no formato do atributo `d` do SVG. */
  readonly caminho: string;
}

export interface GeometriaDoGantt {
  readonly largura: number;
  readonly altura: number;
  readonly dias: readonly Dia[];
  readonly meses: readonly Mes[];
  readonly linhas: readonly LinhaDoGantt[];
  readonly ligacoes: readonly Ligacao[];
  /** A linha de hoje, se hoje cai dentro da obra. */
  readonly hoje: number | null;
}

const MilissegundosPorDia = 86_400_000;

/** Quanto a seta anda para o lado antes de descer, para sair da barra e não por dentro dela. */
const Recuo = 6;

/** O número de uma data "aaaa-mm-dd", contado em UTC: sem fuso nem horário de verão no meio da conta. */
export function numeroDoDia(data: string): number {
  const partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec(data);

  if (!partes) {
    throw new Error(`Data fora do formato aaaa-mm-dd: ${data}`);
  }

  return Date.UTC(Number(partes[1]), Number(partes[2]) - 1, Number(partes[3])) / MilissegundosPorDia;
}

function dataDoNumero(numero: number): string {
  return new Date(numero * MilissegundosPorDia).toISOString().slice(0, 10);
}

function chave(dependencia: Dependencia): string {
  return `${dependencia.predecessora}→${dependencia.sucessora}:${dependencia.tipo}`;
}

export function montarGantt(
  programacao: Programacao,
  dependencias: readonly Dependencia[],
  calendario: Calendario,
  escala: Escala,
  hoje?: string,
): GeometriaDoGantt {
  const { larguraDoDia, alturaDaLinha, esquerda, topo, direita } = escala;
  const primeiro = numeroDoDia(programacao.dataDeInicio);
  const ultimo = numeroDoDia(programacao.dataDeTermino);
  const feriados = new Set(calendario.feriados);
  const x = (data: string) => esquerda + (numeroDoDia(data) - primeiro) * larguraDoDia;

  const dias = Array.from({ length: ultimo - primeiro + 1 }, (_, indice): Dia => {
    const numero = primeiro + indice;
    const data = dataDoNumero(numero);
    const semana = new Date(numero * MilissegundosPorDia).getUTCDay();

    return {
      data,
      x: esquerda + indice * larguraDoDia,
      util: semana !== 0 && (semana !== 6 || calendario.sabadoUtil) && !feriados.has(data),
      segunda: semana === 1,
    };
  });

  const meses: Mes[] = [];

  for (const dia of dias) {
    const rotulo = formatarMes(`${dia.data.slice(0, 7)}-01`);
    const atual = meses.at(-1);

    if (atual?.rotulo === rotulo) {
      meses[meses.length - 1] = { ...atual, largura: atual.largura + larguraDoDia };
    } else {
      meses.push({ rotulo, x: dia.x, largura: larguraDoDia });
    }
  }

  const linhas = programacao.atividades.map((atividade, indice): LinhaDoGantt => {
    const fimCedo = x(atividade.terminoCedo) + larguraDoDia;
    const marco = atividade.ehMarco ? { x: x(atividade.inicioCedo) + larguraDoDia / 2 } : null;
    const barra = marco ? null : { x: x(atividade.inicioCedo), largura: fimCedo - x(atividade.inicioCedo) };
    const fim = marco ? marco.x : fimCedo;
    const fimTarde = marco
      ? x(atividade.inicioTarde) + larguraDoDia / 2
      : x(atividade.terminoTarde) + larguraDoDia;

    return {
      codigo: atividade.codigo,
      descricao: atividade.descricao,
      y: topo + indice * alturaDaLinha,
      critica: atividade.ehCritica,
      barra,
      marco,
      folga: atividade.folgaTotal > 0 ? { x: fim, largura: fimTarde - fim } : null,
      inicio: barra ? barra.x : fim,
      fim,
    };
  });

  const porCodigo = new Map(linhas.map((linha) => [linha.codigo, linha]));
  const criticas = new Set(programacao.dependenciasCriticas.map(chave));
  const meio = alturaDaLinha / 2;

  // A seta sai do lado da predecessora que o vínculo amarra — o término, no
  // término-início — e chega ao lado da sucessora que ele amarra.
  const ligacoes = dependencias.flatMap((dependencia): Ligacao[] => {
    const de = porCodigo.get(dependencia.predecessora);
    const para = porCodigo.get(dependencia.sucessora);

    if (!de || !para) {
      return [];
    }

    const saiDoTermino = dependencia.tipo === 'TerminoInicio' || dependencia.tipo === 'TerminoTermino';
    const chegaNoInicio = dependencia.tipo === 'TerminoInicio' || dependencia.tipo === 'InicioInicio';
    const origem = saiDoTermino ? de.fim : de.inicio;
    const destino = chegaNoInicio ? para.inicio : para.fim;
    const desvio = origem + (saiDoTermino ? Recuo : -Recuo);

    return [
      {
        predecessora: dependencia.predecessora,
        sucessora: dependencia.sucessora,
        tipo: dependencia.tipo,
        critica: criticas.has(chave(dependencia)),
        caminho: `M${origem},${de.y + meio} H${desvio} V${para.y + meio} H${destino}`,
      },
    ];
  });

  const dentroDaObra = (data: string) => numeroDoDia(data) >= primeiro && numeroDoDia(data) <= ultimo;

  return {
    largura: esquerda + dias.length * larguraDoDia + direita,
    altura: topo + linhas.length * alturaDaLinha,
    dias,
    meses,
    linhas,
    ligacoes,
    hoje: hoje !== undefined && dentroDaObra(hoje) ? x(hoje) : null,
  };
}
