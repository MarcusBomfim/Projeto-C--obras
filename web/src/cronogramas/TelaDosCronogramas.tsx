import { Link } from 'react-router';
import { api } from '../api/cliente';
import { comoProblema } from '../api/problemas';
import { AvisoDeProblema } from '../comum/AvisoDeProblema';
import { Carregando } from '../comum/Carregando';
import { formatarDataEHora } from '../comum/formatacao';
import estilos from './TelaDosCronogramas.module.css';

/** Os cronogramas da empresa, em ordem de nome — cada um leva ao seu Gantt. */
export function TelaDosCronogramas() {
  const cronogramas = api.useQuery('get', '/api/cronogramas');

  return (
    <section aria-labelledby="titulo-dos-cronogramas">
      <h1 id="titulo-dos-cronogramas">Cronogramas</h1>
      <p className="subtitulo">A rede de cada obra no calendário, com o caminho crítico em destaque.</p>

      {cronogramas.isPending && <Carregando rotulo="Carregando os cronogramas" />}
      {cronogramas.isError && <AvisoDeProblema problema={comoProblema(cronogramas.error)} />}

      {cronogramas.isSuccess &&
        (cronogramas.data.length === 0 ? (
          <p className={estilos.vazio}>Nenhum cronograma ainda. O planejador cria os cronogramas pela API.</p>
        ) : (
          <ul className={estilos.lista}>
            {cronogramas.data.map((cronograma) => (
              <li key={cronograma.id}>
                <Link to={`/cronogramas/${cronograma.id}`}>{cronograma.nome}</Link>
                <span className={estilos.versao}>
                  versão {cronograma.versao}, de {formatarDataEHora(cronograma.atualizadoEm)}
                </span>
              </li>
            ))}
          </ul>
        ))}
    </section>
  );
}
