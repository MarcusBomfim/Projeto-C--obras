import { Link, useParams } from 'react-router';
import { api } from '../api/cliente';
import { comoProblema } from '../api/problemas';
import { AvisoDeProblema } from '../comum/AvisoDeProblema';
import { Carregando } from '../comum/Carregando';
import { formatarMes } from '../comum/formatacao';
import { SeloDaSituacao } from '../comum/SeloDaSituacao';
import { AnaliseDaObra } from './AnaliseDaObra';
import estilos from './TelaDaObra.module.css';

/**
 * Uma obra: o nome e a situação vêm do painel; os números, do valor agregado
 * calculado na hora, a partir do acompanhamento. O painel é atualizado em
 * segundo plano e pode estar instantes atrás — os números, não.
 */
export function TelaDaObra() {
  const { acompanhamento = '' } = useParams();
  const painel = api.useQuery('get', '/api/painel/{acompanhamento}', {
    params: { path: { acompanhamento } },
  });
  const medida = painel.isSuccess && painel.data.medicao !== null;
  const analise = api.useQuery(
    'get',
    '/api/acompanhamentos/{id}/valor-agregado',
    { params: { path: { id: acompanhamento } } },
    { enabled: medida },
  );

  return (
    <article aria-labelledby="titulo-da-obra">
      <p className={estilos.voltar}>
        <Link to="/">← Painel das obras</Link>
      </p>

      {painel.isPending && <Carregando rotulo="Carregando a obra" />}
      {painel.isError && <AvisoDeProblema problema={comoProblema(painel.error)} />}

      {painel.isSuccess && (
        <>
          <header className={estilos.cabecalho}>
            <h1 id="titulo-da-obra">{painel.data.obra}</h1>
            <SeloDaSituacao situacao={painel.data.situacao} />
          </header>

          {!medida && (
            <p className="subtitulo">Esta obra ainda não tem medição: o valor agregado começa na primeira.</p>
          )}

          {medida && analise.isPending && <Carregando rotulo="Calculando o valor agregado" />}
          {analise.isError && <AvisoDeProblema problema={comoProblema(analise.error)} />}

          {analise.isSuccess && (
            <>
              <p className="subtitulo">
                {`Valor agregado a custo, até a medição ${analise.data.medicao} — ${formatarMes(analise.data.mes)}.`}
              </p>
              <AnaliseDaObra analise={analise.data} />
            </>
          )}
        </>
      )}
    </article>
  );
}
