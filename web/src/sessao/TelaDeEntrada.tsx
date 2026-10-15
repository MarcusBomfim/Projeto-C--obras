import { useState, type SubmitEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { useSessao } from './contexto';
import { destinoSeguro } from './destino';
import estilos from './TelaDeEntrada.module.css';

/**
 * Em produção, a entrada é o provedor de identidade da empresa. Em
 * desenvolvimento, cola-se aqui o token gerado pelo `dotnet user-jwts`, com
 * a empresa e os papéis de quem se quer ser (ADR-0018).
 */
export function TelaDeEntrada() {
  const { entrar, aviso } = useSessao();
  const navegar = useNavigate();
  const [parametros] = useSearchParams();
  const [token, definirToken] = useState('');
  const [erro, definirErro] = useState<string | null>(null);

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();

    const motivo = entrar(token);

    if (motivo) {
      definirErro(motivo);

      return;
    }

    void navegar(destinoSeguro(parametros.get('de')), { replace: true });
  }

  return (
    <main className={estilos.pagina}>
      <form className={estilos.cartao} onSubmit={enviar} aria-labelledby="titulo-entrada">
        <h1 id="titulo-entrada">Planejamento de Obras</h1>
        <p className={estilos.chamada}>Entre com o token de acesso da sua empresa.</p>

        {aviso && (
          <p className={estilos.aviso} role="status">
            {aviso}
          </p>
        )}

        <label htmlFor="token">Token de acesso (JWT)</label>
        <textarea
          id="token"
          rows={5}
          spellCheck={false}
          autoComplete="off"
          value={token}
          onChange={(evento) => {
            definirToken(evento.target.value);
            definirErro(null);
          }}
          aria-invalid={erro !== null}
          aria-describedby={erro ? 'erro-do-token' : 'ajuda-do-token'}
        />

        {erro && (
          <p id="erro-do-token" className={estilos.erro} role="alert">
            {erro}
          </p>
        )}

        <button type="submit" disabled={token.trim() === ''}>
          Entrar
        </button>

        <p id="ajuda-do-token" className={estilos.ajuda}>
          Em desenvolvimento, gere um com o papel e a empresa que quiser:
          <code>
            dotnet user-jwts create --project src/PlanejamentoObras.Api --role gestor --claim
            empresa=0199e3b4-0000-7000-8000-000000000001
          </code>
        </p>
      </form>
    </main>
  );
}
