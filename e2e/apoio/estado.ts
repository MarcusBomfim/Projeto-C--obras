import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/** O que a preparação criou pela API, para os testes encontrarem na tela. */
export interface ObraCriada {
  readonly empresa: string;
  readonly token: string;
  readonly acompanhamento: string;
  readonly cronograma: string;
}

const Arquivo = join(dirname(fileURLToPath(import.meta.url)), '..', '.estado', 'obra.json');

export function guardarObra(obra: ObraCriada): void {
  mkdirSync(dirname(Arquivo), { recursive: true });
  writeFileSync(Arquivo, JSON.stringify(obra, null, 2));
}

export function lerObra(): ObraCriada {
  return JSON.parse(readFileSync(Arquivo, 'utf8')) as ObraCriada;
}
