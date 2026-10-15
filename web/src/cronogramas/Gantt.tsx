import { useId } from 'react';
import type { Esquemas } from '../api/cliente';
import { formatarDiaDaObra, type ParametrosDaProgramacao } from './calendario';
import { montarGantt, type Escala } from './geometriaDoGantt';
import estilos from './Gantt.module.css';

const Tamanho: Escala = { larguraDoDia: 14, alturaDaLinha: 32, esquerda: 270, topo: 46, direita: 16 };

const meio = Tamanho.alturaDaLinha / 2;

function encurtar(texto: string, maximo: number): string {
  return texto.length > maximo ? `${texto.slice(0, maximo - 1)}…` : texto;
}

function losango(x: number, y: number, raio: number): string {
  return `M${x},${y - raio} L${x + raio},${y} L${x},${y + raio} L${x - raio},${y} Z`;
}

/**
 * O cronograma no calendário: a barra de cada atividade nas datas cedo, a
 * folga total até as datas tarde, os marcos, os vínculos — o caminho crítico
 * em destaque — e os dias em que não se trabalha. A tabela abaixo dele diz o
 * mesmo em números, para quem não enxerga o desenho.
 */
export function Gantt({
  programacao,
  dependencias,
  parametros,
  hoje,
}: {
  readonly programacao: Esquemas['ProgramacaoResposta'];
  readonly dependencias: readonly Esquemas['DependenciaResposta'][];
  readonly parametros: ParametrosDaProgramacao;
  readonly hoje: string;
}) {
  const id = useId();
  const gantt = montarGantt(programacao, dependencias, parametros, Tamanho, hoje);
  const seta = `${id}-seta`;

  return (
    <figure className={estilos.figura} aria-labelledby={`${id}-legenda`}>
      <figcaption id={`${id}-legenda`} className={estilos.legenda}>
        <span className={estilos.titulo}>Gantt</span>
        <ul>
          <li className={estilos.amostraCritica}>Atividade crítica</li>
          <li className={estilos.amostraComFolga}>Atividade com folga</li>
          <li className={estilos.amostraFolga}>Folga total</li>
          <li className={estilos.amostraMarco}>Marco</li>
          <li className={estilos.amostraNaoUtil}>Dia não útil</li>
          {gantt.hoje !== null && <li className={estilos.amostraHoje}>Hoje</li>}
        </ul>
      </figcaption>

      <div className={estilos.rolagem}>
        <svg width={gantt.largura} height={gantt.altura} className={estilos.grafico} aria-hidden="true">
          <defs>
            {(['normal', 'critica'] as const).map((tipo) => (
              <marker
                key={tipo}
                id={`${seta}-${tipo}`}
                viewBox="0 0 8 8"
                refX="7"
                refY="4"
                markerWidth="7"
                markerHeight="7"
                orient="auto"
              >
                <path
                  d="M0,0 L8,4 L0,8 Z"
                  className={tipo === 'critica' ? estilos.setaCritica : estilos.seta}
                />
              </marker>
            ))}
          </defs>

          {gantt.linhas.map(
            (linha, indice) =>
              indice % 2 === 1 && (
                <rect
                  key={linha.codigo}
                  x={0}
                  y={linha.y}
                  width={gantt.largura}
                  height={Tamanho.alturaDaLinha}
                  className={estilos.faixa}
                />
              ),
          )}

          {gantt.dias
            .filter((dia) => !dia.util)
            .map((dia) => (
              <rect
                key={dia.data}
                x={dia.x}
                y={Tamanho.topo}
                width={Tamanho.larguraDoDia}
                height={gantt.altura - Tamanho.topo}
                className={estilos.naoUtil}
              />
            ))}

          {gantt.meses.map((mes) => (
            <g key={mes.rotulo}>
              <line x1={mes.x} x2={mes.x} y1={0} y2={gantt.altura} className={estilos.divisao} />
              <text x={mes.x + 6} y={17} className={estilos.mes}>
                {mes.rotulo}
              </text>
            </g>
          ))}

          {gantt.dias
            .filter((dia) => dia.segunda)
            .map((dia) => (
              <text key={dia.data} x={dia.x + 2} y={37} className={estilos.dia}>
                {dia.data.slice(8)}
              </text>
            ))}

          {gantt.hoje !== null && (
            <line
              x1={gantt.hoje}
              x2={gantt.hoje}
              y1={Tamanho.topo - 4}
              y2={gantt.altura}
              className={estilos.hoje}
            />
          )}

          {gantt.ligacoes.map((ligacao) => (
            <path
              key={`${ligacao.predecessora}-${ligacao.sucessora}-${ligacao.tipo}`}
              d={ligacao.caminho}
              className={ligacao.critica ? estilos.ligacaoCritica : estilos.ligacao}
              markerEnd={`url(#${seta}-${ligacao.critica ? 'critica' : 'normal'})`}
            />
          ))}

          {gantt.linhas.map((linha) => (
            <g key={linha.codigo}>
              <text x={14} y={linha.y + meio} className={estilos.codigo}>
                {linha.codigo}
              </text>
              <text x={66} y={linha.y + meio} className={estilos.descricao}>
                {encurtar(linha.descricao, 27)}
              </text>

              {linha.folga && (
                <rect
                  x={linha.folga.x}
                  y={linha.y + meio - 2}
                  width={linha.folga.largura}
                  height={4}
                  className={estilos.folga}
                />
              )}

              {linha.barra && (
                <rect
                  x={linha.barra.x + 1}
                  y={linha.y + 8}
                  width={linha.barra.largura - 2}
                  height={Tamanho.alturaDaLinha - 16}
                  rx={3}
                  className={linha.critica ? estilos.barraCritica : estilos.barra}
                >
                  <title>{`${linha.codigo} — ${linha.descricao}`}</title>
                </rect>
              )}

              {linha.marco && (
                <path
                  d={losango(linha.marco.x, linha.y + meio, 7)}
                  className={linha.critica ? estilos.marcoCritico : estilos.marco}
                >
                  <title>{`${linha.codigo} — ${linha.descricao}`}</title>
                </path>
              )}
            </g>
          ))}
        </svg>
      </div>

      <div className={estilos.rolagem}>
        <table className={estilos.tabela}>
          <caption className="somente-leitor">Atividades do cronograma, com as datas cedo e tarde</caption>
          <thead>
            <tr>
              <th scope="col">Atividade</th>
              <th scope="col" className="numero">
                Dias úteis
              </th>
              <th scope="col">Início</th>
              <th scope="col">Término</th>
              <th scope="col">Início tarde</th>
              <th scope="col">Término tarde</th>
              <th scope="col" className="numero">
                Folga total
              </th>
              <th scope="col" className="numero">
                Folga livre
              </th>
              <th scope="col">Crítica</th>
            </tr>
          </thead>
          <tbody>
            {programacao.atividades.map((atividade) => (
              <tr key={atividade.codigo} className={atividade.ehCritica ? estilos.linhaCritica : undefined}>
                <th scope="row">
                  {atividade.codigo} — {atividade.descricao}
                </th>
                <td className="numero">{atividade.ehMarco ? 'marco' : atividade.duracaoEmDiasUteis}</td>
                <td>{formatarDiaDaObra(atividade.inicioCedo)}</td>
                <td>{formatarDiaDaObra(atividade.terminoCedo)}</td>
                <td>{formatarDiaDaObra(atividade.inicioTarde)}</td>
                <td>{formatarDiaDaObra(atividade.terminoTarde)}</td>
                <td className="numero">{atividade.folgaTotal}</td>
                <td className="numero">{atividade.folgaLivre}</td>
                <td>{atividade.ehCritica ? 'sim' : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </figure>
  );
}
