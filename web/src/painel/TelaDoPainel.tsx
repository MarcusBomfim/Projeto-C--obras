import { api, type Esquemas } from '../api/cliente';
import { comoProblema } from '../api/problemas';
import { AvisoDeProblema } from '../comum/AvisoDeProblema';
import { Carregando } from '../comum/Carregando';
import { OrdemDasSituacoes, type SituacaoDaObra } from '../comum/situacao';
import { CartaoDaObra } from './CartaoDaObra';
import estilos from './TelaDoPainel.module.css';

/**
 * O painel é atualizado em segundo plano, a cada medição (ADR-0017); a tela
 * pergunta de novo de tempos em tempos, e também ao voltar para a aba.
 */
export const IntervaloDeAtualizacao = 30_000;

type ObraNoPainel = Esquemas['ObraNoPainelResposta'];

/** Como cada situação se conta: uma crítica, duas críticas; em dia não varia. */
const Contagem: Readonly<Record<SituacaoDaObra, readonly [string, string]>> = {
  Critica: ['crítica', 'críticas'],
  Atencao: ['em atenção', 'em atenção'],
  EmDia: ['em dia', 'em dia'],
  SemMedicao: ['sem medição', 'sem medição'],
};

function emOrdem(obras: readonly ObraNoPainel[]): ObraNoPainel[] {
  return [...obras].sort(
    (a, b) =>
      OrdemDasSituacoes.indexOf(a.situacao) - OrdemDasSituacoes.indexOf(b.situacao) ||
      a.obra.localeCompare(b.obra, 'pt-BR'),
  );
}

/** As obras da empresa, a que pede ação primeiro. */
export function TelaDoPainel() {
  const painel = api.useQuery('get', '/api/painel', {}, { refetchInterval: IntervaloDeAtualizacao });

  return (
    <section aria-labelledby="titulo-do-painel">
      <h1 id="titulo-do-painel">Painel das obras</h1>
      <p className="subtitulo">
        A situação de cada obra na última medição, pelo pior dos índices de desempenho de prazo e de custo.
      </p>

      {painel.isPending && <Carregando rotulo="Carregando o painel" />}
      {painel.isError && <AvisoDeProblema problema={comoProblema(painel.error)} />}
      {painel.isSuccess && <Obras obras={emOrdem(painel.data)} />}
    </section>
  );
}

function Obras({ obras }: { readonly obras: readonly ObraNoPainel[] }) {
  if (obras.length === 0) {
    return (
      <p className={estilos.vazio}>
        Nenhuma obra acompanhada ainda. Quando um engenheiro começar a medir uma obra contra a linha de base,
        ela aparece aqui.
      </p>
    );
  }

  return (
    <>
      <ul className={estilos.resumo} aria-label="Obras por situação">
        {OrdemDasSituacoes.map((situacao) => {
          const quantas = obras.filter((obra) => obra.situacao === situacao).length;

          return (
            quantas > 0 && (
              <li key={situacao}>
                <strong>{quantas}</strong> {Contagem[situacao][quantas === 1 ? 0 : 1]}
              </li>
            )
          );
        })}
      </ul>

      <ul className={estilos.obras}>
        {obras.map((obra) => (
          <li key={obra.acompanhamento}>
            <CartaoDaObra obra={obra} />
          </li>
        ))}
      </ul>
    </>
  );
}
