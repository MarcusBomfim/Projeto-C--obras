import { Link } from 'react-router';
import type { Esquemas } from '../api/cliente';
import { formatarDataEHora, formatarIndice, formatarMes, formatarMoeda } from '../comum/formatacao';
import { SeloDaSituacao } from '../comum/SeloDaSituacao';
import { situacaoDoIndice } from '../comum/situacao';
import estilos from './CartaoDaObra.module.css';

/**
 * Uma obra no painel. O cartão inteiro leva à obra, mas o link é só o nome:
 * quem usa leitor de tela ouve "Linha de base 1 — contrato, link", e não o
 * cartão todo.
 */
export function CartaoDaObra({ obra }: { readonly obra: Esquemas['ObraNoPainelResposta'] }) {
  const variacaoNegativa = obra.variacaoNoTermino !== null && obra.variacaoNoTermino < 0;

  return (
    <article className={estilos.cartao}>
      <header className={estilos.cabecalho}>
        <h2>
          <Link to={`/obras/${obra.acompanhamento}`} className={estilos.link}>
            {obra.obra}
          </Link>
        </h2>
        <SeloDaSituacao situacao={obra.situacao} />
      </header>

      {obra.medicao === null ? (
        <p className={estilos.semMedicao}>Ainda sem medição.</p>
      ) : (
        <>
          <p className={estilos.medicao}>
            Medição {obra.medicao} · {formatarMes(obra.mes)}
          </p>
          <dl className={estilos.indicadores}>
            <div>
              <dt>IDP</dt>
              <dd className={estilos[situacaoDoIndice(obra.indiceDeDesempenhoDePrazo)]}>
                {formatarIndice(obra.indiceDeDesempenhoDePrazo)}
              </dd>
            </div>
            <div>
              <dt>IDC</dt>
              <dd className={estilos[situacaoDoIndice(obra.indiceDeDesempenhoDeCusto)]}>
                {formatarIndice(obra.indiceDeDesempenhoDeCusto)}
              </dd>
            </div>
            <div>
              <dt>Estimativa no término</dt>
              <dd>{formatarMoeda(obra.estimativaNoTermino)}</dd>
            </div>
            <div>
              <dt>Variação no término</dt>
              <dd className={variacaoNegativa ? estilos.Critica : undefined}>
                {formatarMoeda(obra.variacaoNoTermino)}
              </dd>
            </div>
          </dl>
        </>
      )}

      <p className={estilos.rodape}>
        Atualizado em {formatarDataEHora(obra.atualizadoEm)}, da versão {obra.versaoDoAcompanhamento} do
        acompanhamento
      </p>
    </article>
  );
}
