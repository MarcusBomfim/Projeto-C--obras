# ADR-0018 — Front-end tipado pelo contrato da API, com o estado do servidor no cache

**Status:** aceita

## Contexto

Até aqui, quem usa o sistema fala com a API. A Parte 7 traz telas: o painel das obras, a obra com a curva S, o cronograma em Gantt. Uma aplicação no navegador traz problemas que a API não tinha:

- o contrato pode mudar de um lado e não do outro;
- o que veio do servidor envelhece na tela;
- o token tem de chegar a cada pedido;
- um gráfico tem de dizer o mesmo para quem não o enxerga.

## Decisão

**1. Uma aplicação React no próprio repositório**, em `web/`, com Vite e TypeScript estrito. Ela fala com a API na mesma origem: em desenvolvimento, o Vite repassa `/api` para ela, e a API não precisa de CORS.

**2. O contrato da API é a fonte dos tipos.** O documento OpenAPI fica versionado em `docs/api/openapi.json`, e um teste da API falha se ela mudar sem o arquivo. O front-end gera dele os seus tipos, com o openapi-typescript, e o CI falha se o arquivo gerado não bater com o contrato. O cliente HTTP, o openapi-fetch, tipa o caminho, os parâmetros e a resposta de cada chamada: endereço errado, parâmetro que falta e campo que não existe são erro de compilação. Para o contrato dizer de cada número que ele é um número, a API passa a recusar número escrito em texto, coerente com o JSON estrito que ela já exigia.

**3. O que vem da API mora no cache, não num estado global.** O TanStack Query guarda as leituras, junta pedidos iguais, pergunta de novo ao voltar para a aba e, no painel, a cada 30 segundos — o painel é atualizado em segundo plano (ADR-0017). Erro de rede e do servidor são tentados de novo; um 404 ou um 403, não. O único estado da aplicação é a sessão.

**4. A sessão é o token do provedor de identidade, guardado na aba.** Em desenvolvimento, cola-se o token do `dotnet user-jwts`. O front-end só lê o token, para mostrar quem entrou e saber quando ele vence; quem confere a assinatura é a API, a cada pedido. Se a API recusar o token, ou se ele vencer, a sessão acaba, e a tela de entrada diz por quê. Trocar de sessão esvazia o cache: o que uma empresa leu não aparece para a próxima.

**5. Cada área da aplicação se registra sozinha**, como os recursos da API (ADR-0015). Cada pasta com um `modulo.tsx` declara as suas rotas e a sua entrada no menu, e a moldura as encontra na compilação. Tela nova não mexe nas rotas.

**6. Gráficos em SVG feito à mão**, sem biblioteca de gráficos. A geometria — escalas, marcas dos eixos, traços — fica em funções puras, testadas com números. Cada gráfico tem ao lado a tabela dos seus números: é ela que um leitor de tela lê. A situação da obra tem cor, mas nunca só cor: o selo escreve a situação.

**7. Os testes abrem a aplicação inteira.** As rotas, a sessão e o cache são os de verdade; só a API é de mentira, com o MSW, e ela recusa pedido que o teste não previu. Os exemplos são respostas reais da API para a obra do README, tipados pelo contrato.

## Consequências

- **A API mudou e o front-end não acompanhou: quebra o CI, e não a tela em produção.** E quem muda o contrato vê o diff dele no mesmo commit.
- **TypeScript 5.9**, e não o 7: o typescript-eslint e o openapi-typescript ainda não o suportam. A troca é uma linha quando suportarem.
- **Dinheiro chega como número de ponto flutuante.** Basta para mostrar com centavos; o front-end não faz conta de dinheiro, e quem faz é o domínio, em `decimal`.
- **O token fica ao alcance de script da própria página.** O que o protege é não haver script de fora: nenhuma dependência carregada de outro site, e o React escapando o que mostra. Com um provedor de identidade de verdade, o caminho mais seguro é um back-end para o front-end, com o token num cookie que script nenhum lê. A tela de colar token sai; o resto não muda.
- **Em desenvolvimento são dois processos**, a API e o Vite. Em produção, servir os dois na mesma origem fica com quem implanta.
