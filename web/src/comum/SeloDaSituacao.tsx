import { NomesDasSituacoes, type SituacaoDaObra } from './situacao';
import estilos from './SeloDaSituacao.module.css';

export function SeloDaSituacao({ situacao }: { readonly situacao: SituacaoDaObra }) {
  return (
    <span className={`${estilos.selo ?? ''} ${estilos[situacao] ?? ''}`}>{NomesDasSituacoes[situacao]}</span>
  );
}
