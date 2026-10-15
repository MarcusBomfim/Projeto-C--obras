import { expect, type APIRequestContext } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/*
 * A API usada como um cliente usaria: criar com chave de idempotência,
 * alterar com a versão lida. É assim que a obra do README chega ao banco
 * antes de o navegador abrir.
 */

const Dados = join(dirname(fileURLToPath(import.meta.url)), '..', 'dados');

export function lerDados(nome: string): Record<string, unknown> {
  return JSON.parse(readFileSync(join(Dados, `${nome}.json`), 'utf8')) as Record<string, unknown>;
}

/** Cria e devolve o id tirado do Location. */
export async function criar(api: APIRequestContext, caminho: string, corpo: unknown): Promise<string> {
  const resposta = await api.post(caminho, { data: corpo, headers: { 'Idempotency-Key': randomUUID() } });

  expect(resposta.status(), `POST ${caminho}: ${await resposta.text()}`).toBe(201);

  const id = resposta.headers().location?.split('/').at(-1);
  expect(id, `POST ${caminho} sem Location`).toBeTruthy();

  return id ?? '';
}

/** Registra uma medição sobre a versão lida do acompanhamento. */
export async function medir(
  api: APIRequestContext,
  acompanhamento: string,
  versao: number,
  medicao: unknown,
) {
  const resposta = await api.post(`/api/acompanhamentos/${acompanhamento}/medicoes`, {
    data: medicao,
    headers: { 'If-Match': `"${versao}"` },
  });

  expect(resposta.status(), `medição sobre a versão ${versao}: ${await resposta.text()}`).toBe(200);
}

/** Espera o painel mostrar a versão do acompanhamento: o processador tratou as mensagens dela. */
export async function esperarPainel(api: APIRequestContext, acompanhamento: string, versao: number) {
  await expect
    .poll(
      async () => {
        const resposta = await api.get(`/api/painel/${acompanhamento}`);

        return resposta.ok()
          ? ((await resposta.json()) as { versaoDoAcompanhamento: number }).versaoDoAcompanhamento
          : 0;
      },
      { message: `o painel chegar à versão ${versao} do acompanhamento`, timeout: 30_000 },
    )
    .toBe(versao);
}
