import { useState, type SubmitEvent } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import { api } from '../api/cliente';
import { comoProblema } from '../api/problemas';
import { AvisoDeProblema } from '../comum/AvisoDeProblema';
import { Carregando } from '../comum/Carregando';
import { formatarData } from '../comum/formatacao';
import {
  escreverParametros,
  hojeEmIso,
  lerFeriados,
  lerParametros,
  type ParametrosDaProgramacao,
} from './calendario';
import { Gantt } from './Gantt';
import estilos from './TelaDoCronograma.module.css';

const ListaDeCodigos = new Intl.ListFormat('pt-BR', { type: 'conjunction' });

/**
 * O cronograma programado: o calendário vem do endereço, o cálculo, da API
 * (caminho crítico e datas, ADR-0003), e o desenho é desta tela.
 */
export function TelaDoCronograma() {
  const { id = '' } = useParams();
  const [endereco, definirEndereco] = useSearchParams();
  const hoje = hojeEmIso();
  const parametros = lerParametros(endereco, hoje);

  const cronograma = api.useQuery('get', '/api/cronogramas/{id}', { params: { path: { id } } });
  const programacao = api.useQuery('get', '/api/cronogramas/{id}/programacao', {
    params: {
      path: { id },
      query: {
        inicio: parametros.inicio,
        feriados: [...parametros.feriados],
        sabadoUtil: parametros.sabadoUtil,
      },
    },
  });

  return (
    <article aria-labelledby="titulo-do-cronograma">
      <p className={estilos.voltar}>
        <Link to="/cronogramas">← Cronogramas</Link>
      </p>

      <h1 id="titulo-do-cronograma">{cronograma.data?.nome ?? 'Cronograma'}</h1>

      <Calendario
        key={endereco.toString()}
        atual={parametros}
        aoProgramar={(novos) => {
          definirEndereco(escreverParametros(novos));
        }}
      />

      {(cronograma.isPending || programacao.isPending) && <Carregando rotulo="Programando o cronograma" />}
      {cronograma.isError && <AvisoDeProblema problema={comoProblema(cronograma.error)} />}
      {programacao.isError && <AvisoDeProblema problema={comoProblema(programacao.error)} />}

      {cronograma.isSuccess && programacao.isSuccess && (
        <>
          <p className={estilos.resumo}>
            De {formatarData(programacao.data.dataDeInicio)} a {formatarData(programacao.data.dataDeTermino)},{' '}
            {programacao.data.duracaoEmDiasUteis} dias úteis. Atividades críticas:{' '}
            {ListaDeCodigos.format(
              programacao.data.atividades
                .filter((atividade) => atividade.ehCritica)
                .map((atividade) => atividade.codigo),
            )}
            .
          </p>
          <Gantt
            programacao={programacao.data}
            dependencias={cronograma.data.dependencias}
            parametros={parametros}
            hoje={hoje}
          />
        </>
      )}
    </article>
  );
}

/** O calendário da programação: o dia de começar, os feriados e se o sábado é útil. */
function Calendario({
  atual,
  aoProgramar,
}: {
  readonly atual: ParametrosDaProgramacao;
  readonly aoProgramar: (parametros: ParametrosDaProgramacao) => void;
}) {
  const [inicio, definirInicio] = useState(atual.inicio);
  const [feriados, definirFeriados] = useState(atual.feriados.map(formatarData).join(', '));
  const [sabadoUtil, definirSabadoUtil] = useState(atual.sabadoUtil);
  const [invalidas, definirInvalidas] = useState<string[]>([]);

  function programar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();

    const lidos = lerFeriados(feriados);
    definirInvalidas(lidos.invalidas);

    if (lidos.invalidas.length === 0 && inicio !== '') {
      aoProgramar({ inicio, feriados: lidos.datas, sabadoUtil });
    }
  }

  return (
    <form className={estilos.calendario} onSubmit={programar} aria-label="Calendário da programação">
      <label>
        Início da obra
        <input
          type="date"
          required
          value={inicio}
          onChange={(evento) => {
            definirInicio(evento.target.value);
          }}
        />
      </label>

      <label className={estilos.feriados}>
        Feriados
        <input
          type="text"
          value={feriados}
          placeholder="12/10/2026, 02/11/2026"
          aria-invalid={invalidas.length > 0}
          aria-describedby="ajuda-dos-feriados"
          onChange={(evento) => {
            definirFeriados(evento.target.value);
          }}
        />
      </label>

      <label className={estilos.sabado}>
        <input
          type="checkbox"
          checked={sabadoUtil}
          onChange={(evento) => {
            definirSabadoUtil(evento.target.checked);
          }}
        />
        Sábado é dia útil
      </label>

      <button type="submit">Programar</button>

      <p
        id="ajuda-dos-feriados"
        className={invalidas.length > 0 ? estilos.erro : estilos.ajuda}
        role={invalidas.length > 0 ? 'alert' : undefined}
      >
        {invalidas.length > 0
          ? `Não são datas: ${invalidas.join(', ')}. Use dd/mm/aaaa.`
          : 'Datas em dd/mm/aaaa, separadas por vírgula. Feriado e fim de semana não contam como dia útil.'}
      </p>
    </form>
  );
}
