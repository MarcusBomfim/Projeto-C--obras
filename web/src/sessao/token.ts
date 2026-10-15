/*
 * O token de acesso: um JWT emitido pelo provedor de identidade, com a
 * empresa e os papéis de quem entrou. Em desenvolvimento, o do
 * `dotnet user-jwts` (ADR-0018).
 *
 * Aqui ele só é lido, para mostrar quem entrou e saber quando vence. Quem
 * confere a assinatura é a API, a cada pedido — o que o front-end decide com
 * o token é só o que mostrar.
 */

const Chave = 'planejamento-obras.token';

export interface Sessao {
  readonly token: string;
  readonly nome: string;
  readonly empresa: string;
  readonly papeis: readonly string[];
  readonly venceEm: Date | undefined;
}

export type LeituraDoToken = { readonly sessao: Sessao } | { readonly motivo: string };

/**
 * Guardado na aba, e não no navegador: fechar a aba é sair. Sem acesso ao
 * armazenamento — navegação privada, armazenamento bloqueado —, a sessão só
 * não sobrevive a recarregar a página.
 */
export function lerToken(): string | null {
  try {
    return sessionStorage.getItem(Chave);
  } catch {
    return null;
  }
}

export function guardarToken(token: string): void {
  try {
    sessionStorage.setItem(Chave, token);
  } catch {
    // Sem armazenamento, a sessão vive só na memória.
  }
}

export function esquecerToken(): void {
  try {
    sessionStorage.removeItem(Chave);
  } catch {
    // Nada guardado, nada a esquecer.
  }
}

function lerCarga(token: string): Record<string, unknown> | undefined {
  const partes = token.split('.');

  if (partes.length !== 3 || !partes[1]) {
    return undefined;
  }

  try {
    const base64 = partes[1].replaceAll('-', '+').replaceAll('_', '/');
    const bytes = Uint8Array.from(atob(base64), (caractere) => caractere.charCodeAt(0));
    const carga: unknown = JSON.parse(new TextDecoder().decode(bytes));

    return typeof carga === 'object' && carga !== null ? (carga as Record<string, unknown>) : undefined;
  } catch {
    return undefined;
  }
}

function textos(valor: unknown): string[] {
  if (typeof valor === 'string') {
    return [valor];
  }

  return Array.isArray(valor) ? valor.filter((item): item is string => typeof item === 'string') : [];
}

/** Lê o token colado por quem entra; o motivo, se ele não serve. */
export function lerSessao(token: string, agora: Date = new Date()): LeituraDoToken {
  const limpo = token.trim().replace(/^Bearer\s+/i, '');
  const carga = lerCarga(limpo);

  if (!carga) {
    return { motivo: 'Isso não é um token JWT: são três partes separadas por ponto.' };
  }

  const [empresa] = textos(carga.empresa);

  if (!empresa) {
    return { motivo: 'O token não diz de que empresa você é: falta a reivindicação empresa.' };
  }

  const venceEm = typeof carga.exp === 'number' ? new Date(carga.exp * 1000) : undefined;

  if (venceEm && venceEm <= agora) {
    return { motivo: `O token venceu em ${venceEm.toLocaleString('pt-BR')}. Gere outro.` };
  }

  const [nome] = [...textos(carga.name), ...textos(carga.unique_name), ...textos(carga.sub)];

  return {
    sessao: {
      token: limpo,
      nome: nome ?? 'Sem nome',
      empresa,
      papeis: textos(carga.role),
      venceEm,
    },
  };
}
