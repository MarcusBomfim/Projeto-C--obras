import type { Esquemas } from '../api/cliente';

/*
 * A geometria da curva S, sem desenho: onde fica cada ponto, cada marca do
 * eixo e cada linha, para um gráfico de um tamanho dado. Separada do
 * componente para ser testada com números, e não com pixels de tela.
 */

type PontoDaCurva = Esquemas['PontoDaCurvaResposta'];

export type NomeDaSerie = 'valorPlanejado' | 'valorAgregado' | 'custoReal';

export const NomesDasSeries: readonly NomeDaSerie[] = ['valorPlanejado', 'valorAgregado', 'custoReal'];

export interface Dimensoes {
  readonly largura: number;
  readonly altura: number;
  readonly margem: {
    readonly topo: number;
    readonly direita: number;
    readonly base: number;
    readonly esquerda: number;
  };
}

export interface PontoNoGrafico {
  readonly x: number;
  readonly y: number;
  readonly mes: string;
  readonly valor: number;
}

export interface Serie {
  readonly nome: NomeDaSerie;
  readonly pontos: readonly PontoNoGrafico[];
  /** O traço da linha, no formato do atributo `d` do SVG. */
  readonly caminho: string;
}

export interface MarcaDoValor {
  readonly valor: number;
  readonly y: number;
}

export interface MarcaDoMes {
  readonly mes: string;
  readonly x: number;
}

export interface GeometriaDaCurva {
  readonly series: readonly Serie[];
  readonly marcasDoValor: readonly MarcaDoValor[];
  readonly marcasDoMes: readonly MarcaDoMes[];
  /** O valor no alto do eixo: um múltiplo redondo do passo, acima do maior valor. */
  readonly teto: number;
}

/** O passo do eixo: 1, 2, 2,5 ou 5 vezes uma potência de dez, para umas cinco marcas. */
export function passoRedondo(maximo: number, marcas = 5): number {
  if (maximo <= 0) {
    return 1;
  }

  const bruto = maximo / marcas;
  const potencia = 10 ** Math.floor(Math.log10(bruto));
  const fator = [1, 2, 2.5, 5, 10].find((candidato) => candidato * potencia >= bruto) ?? 10;

  return fator * potencia;
}

const umaCasa = (valor: number) => Math.round(valor * 10) / 10;

export function montarCurva(
  curva: readonly PontoDaCurva[],
  { largura, altura, margem }: Dimensoes,
): GeometriaDaCurva {
  const larguraUtil = largura - margem.esquerda - margem.direita;
  const alturaUtil = altura - margem.topo - margem.base;

  const valores = curva.flatMap((ponto) =>
    NomesDasSeries.map((nome) => ponto[nome]).filter((valor): valor is number => valor !== null),
  );
  const maior = Math.max(0, ...valores);
  const passo = passoRedondo(maior);
  const teto = Math.max(passo, Math.ceil(maior / passo) * passo);

  // Um mês só fica no meio; vários se espalham de uma ponta à outra.
  const x = (indice: number) =>
    umaCasa(
      margem.esquerda + (curva.length > 1 ? (indice * larguraUtil) / (curva.length - 1) : larguraUtil / 2),
    );
  const y = (valor: number) => umaCasa(margem.topo + alturaUtil - (valor / teto) * alturaUtil);

  // Valor agregado e custo real param no mês medido: depois dele, a API manda nulo.
  const series = NomesDasSeries.map((nome): Serie => {
    const pontos = curva.flatMap((ponto, indice) => {
      const valor = ponto[nome];

      return valor === null ? [] : [{ x: x(indice), y: y(valor), mes: ponto.mes, valor }];
    });

    return {
      nome,
      pontos,
      caminho: pontos.map((ponto, indice) => `${indice === 0 ? 'M' : 'L'}${ponto.x},${ponto.y}`).join(' '),
    };
  });

  return {
    series,
    marcasDoValor: Array.from({ length: Math.round(teto / passo) + 1 }, (_, indice) => ({
      valor: indice * passo,
      y: y(indice * passo),
    })),
    marcasDoMes: curva.map((ponto, indice) => ({ mes: ponto.mes, x: x(indice) })),
    teto,
  };
}

export const RotulosDasSeries: Readonly<Record<NomeDaSerie, string>> = {
  valorPlanejado: 'Valor planejado (VP)',
  valorAgregado: 'Valor agregado (VA)',
  custoReal: 'Custo real (CR)',
};
