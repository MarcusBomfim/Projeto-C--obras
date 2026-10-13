# ADR-0001 — Linguagem ubíqua em português

**Status:** aceita

## Contexto

O domínio deste sistema é planejamento de obra no Brasil. Quem o descreve — engenheiro de planejamento, mestre de obras, orçamentista — fala em *caminho crítico*, *folga livre*, *dia útil*, *composição de custo*, *BDI*, *medição*. Alguns desses termos não têm tradução exata; outros têm, mas a tradução muda o sentido. "Medição", em obra, não é *measurement*: é o documento que autoriza o pagamento de um período.

A convenção do ecossistema .NET é nomear tudo em inglês.

## Decisão

O código do domínio usa os termos do domínio, em português: `Cronograma`, `Atividade`, `FolgaTotal`, `CalendarioDeTrabalho`. O que é mecanismo do framework continua com o nome do framework — `IReadOnlyList`, `DateOnly`, `ArgumentNullException`.

A regra de análise **CA1710**, que exige o sufixo `Exception` em exceções, foi desligada no `.editorconfig`: ela pressupõe nomes em inglês, e `ExcecaoDeDominio` já diz o que é.

## Consequências

- Uma regra de negócio lida em voz alta numa reunião é reconhecível no código. A distância entre o que o engenheiro diz e o que o programa faz é a menor possível — que é o ponto da linguagem ubíqua.
- Quem não lê português terá mais dificuldade com o domínio. Para um sistema cujos usuários e especialistas são brasileiros, é a troca certa.
- Mistura de idiomas na mesma linha (`Atividades.Where(...)`) é inevitável e aceita. O critério para decidir é simples: se o termo vem do negócio, português; se vem da biblioteca, como ela nomeou.
- Acentos ficam fora dos identificadores (`Dependencia`, não `Dependência`): o C# os aceita, mas eles quebram busca, ferramenta de linha de comando e teclado de quem não está no padrão ABNT.
