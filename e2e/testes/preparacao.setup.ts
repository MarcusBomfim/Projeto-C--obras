import { request, test as preparacao } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { criar, esperarPainel, lerDados, medir } from '../apoio/api';
import { guardarObra } from '../apoio/estado';
import { emitirToken } from '../apoio/identidade';
import { Api } from '../apoio/pilha';

/*
 * A obra do README, criada pela API numa empresa nova — cada execução tem a
 * sua, e não enxerga as anteriores (ADR-0013) —, com as duas medições, e o
 * painel esperado até o processador tratar as mensagens delas.
 */
preparacao('a obra do README entra pela API e chega ao painel', async () => {
  const empresa = randomUUID();
  const token = emitirToken({
    empresa,
    nome: 'Equipe da obra',
    papeis: ['orcamentista', 'planejador', 'gestor', 'engenheiro'],
  });
  const api = await request.newContext({
    baseURL: Api,
    extraHTTPHeaders: { Authorization: `Bearer ${token}` },
  });

  const baseDeCustos = await criar(api, '/api/bases-de-custos', lerDados('base-de-custos'));
  const orcamento = await criar(api, '/api/orcamentos', lerDados('orcamento'));
  const cronograma = await criar(api, '/api/cronogramas', lerDados('cronograma'));
  const linhaDeBase = await criar(api, '/api/linhas-de-base', {
    ...lerDados('linha-de-base'),
    orcamento,
    baseDeCustos,
    cronograma,
  });
  const acompanhamento = await criar(api, '/api/acompanhamentos', { linhaDeBase });

  await medir(api, acompanhamento, 1, lerDados('medicao-outubro'));
  await medir(api, acompanhamento, 2, lerDados('medicao-novembro'));
  await esperarPainel(api, acompanhamento, 3);

  guardarObra({ empresa, token, acompanhamento, cronograma });
  await api.dispose();
});
