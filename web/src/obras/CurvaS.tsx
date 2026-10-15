import { useId } from 'react';
import type { Esquemas } from '../api/cliente';
import { formatarMes, formatarMoeda, formatarMoedaCompacta } from '../comum/formatacao';
import { montarCurva, RotulosDasSeries, type Dimensoes } from './curva';
import estilos from './CurvaS.module.css';

// Do tamanho em que a curva aparece numa tela comum: o texto do gráfico sai
// no corpo do resto da página, e não ampliado.
const Tamanho: Dimensoes = {
  largura: 1100,
  altura: 360,
  margem: { topo: 16, direita: 24, base: 40, esquerda: 88 },
};

/**
 * A curva S: valor planejado, valor agregado e custo real acumulados, mês a
 * mês. VA abaixo de VP é atraso; CR acima de VA é custo acima do orçado. O
 * gráfico é para ver; a tabela logo abaixo dele, para ler — e é ela que um
 * leitor de tela encontra.
 */
export function CurvaS({
  curva,
  mesDaMedicao,
}: {
  readonly curva: readonly Esquemas['PontoDaCurvaResposta'][];
  readonly mesDaMedicao: string;
}) {
  const id = useId();
  const { largura, altura, margem } = Tamanho;
  const geometria = montarCurva(curva, Tamanho);
  const medicao = geometria.marcasDoMes.find((marca) => marca.mes === mesDaMedicao);

  return (
    <figure className={estilos.figura} aria-labelledby={`${id}-legenda`}>
      <figcaption id={`${id}-legenda`} className={estilos.legenda}>
        <span className={estilos.titulo}>Curva S</span>
        <ul>
          {geometria.series.map((serie) => (
            <li key={serie.nome} className={estilos[serie.nome]}>
              {RotulosDasSeries[serie.nome]}
            </li>
          ))}
        </ul>
      </figcaption>

      <svg viewBox={`0 0 ${largura} ${altura}`} className={estilos.grafico} aria-hidden="true">
        {geometria.marcasDoValor.map((marca) => (
          <g key={marca.valor}>
            <line
              x1={margem.esquerda}
              x2={largura - margem.direita}
              y1={marca.y}
              y2={marca.y}
              className={estilos.grade}
            />
            <text x={margem.esquerda - 10} y={marca.y} className={estilos.valor}>
              {formatarMoedaCompacta(marca.valor)}
            </text>
          </g>
        ))}

        {geometria.marcasDoMes.map((marca) => (
          <text key={marca.mes} x={marca.x} y={altura - margem.base + 24} className={estilos.mes}>
            {formatarMes(marca.mes)}
          </text>
        ))}

        {medicao && (
          <line
            x1={medicao.x}
            x2={medicao.x}
            y1={margem.topo}
            y2={altura - margem.base}
            className={estilos.medicao}
          />
        )}

        {geometria.series.map((serie) => (
          <g key={serie.nome} className={estilos[serie.nome]}>
            <path d={serie.caminho} className={estilos.linha} />
            {serie.pontos.map((ponto) => (
              <circle key={ponto.mes} cx={ponto.x} cy={ponto.y} r={4} className={estilos.ponto}>
                <title>{`${RotulosDasSeries[serie.nome]} em ${formatarMes(ponto.mes)}: ${formatarMoeda(ponto.valor)}`}</title>
              </circle>
            ))}
          </g>
        ))}
      </svg>

      <table className={estilos.tabela}>
        <caption className="somente-leitor">Curva S, valores acumulados por mês</caption>
        <thead>
          <tr>
            <th scope="col">Mês</th>
            {geometria.series.map((serie) => (
              <th key={serie.nome} scope="col" className="numero">
                {RotulosDasSeries[serie.nome]}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {curva.map((ponto) => (
            <tr key={ponto.mes}>
              <th scope="row">{formatarMes(ponto.mes)}</th>
              <td className="numero">{formatarMoeda(ponto.valorPlanejado)}</td>
              <td className="numero">{formatarMoeda(ponto.valorAgregado)}</td>
              <td className="numero">{formatarMoeda(ponto.custoReal)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  );
}
