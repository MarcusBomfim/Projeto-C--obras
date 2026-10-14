# ADR-0015 — O contrato HTTP: token com a empresa, política por papel, versão na ETag, erro em ProblemDetails

**Status:** aceita

## Contexto

A Parte 5 abre o sistema pela rede. Um cliente — o front-end da Parte 7, um aplicativo de campo, uma integração — precisa saber, sem ler o código: como se identifica, o que pode fazer, como sabe que o que leu ainda vale e, quando algo dá errado, o quê. Cada pergunta tem resposta padronizada na web; o trabalho é escolher uma e aplicá-la igual em todo endpoint.

## Decisão

**1. Autenticação por JWT, emitido por um provedor de identidade.** A API não guarda senha nem emite token: confere assinatura, emissor, audiência e validade, configurados em `Authentication:Schemes:Bearer` — em produção, um provedor OIDC; em desenvolvimento, o `dotnet user-jwts`. O token traz quem é (`sub`, `name`), os papéis (`role`) e a empresa (`empresa`).

**2. A empresa vem do token, e de nenhum outro lugar.** Não há id de empresa na rota nem no corpo: o cliente não escolhe em nome de quem pede. Token sem empresa válida recebe 403 em todo endpoint (ADR-0013).

**3. Autorização por política, e a política por papel.** Cada endpoint declara o que exige — `consultar`, `planejar`, `orcar`, `aprovar-linha-de-base`, `medir` —, e cada política diz qual papel a satisfaz:

| Política | Papel | Para |
|---|---|---|
| `consultar` | qualquer um da empresa | ler |
| `planejar` | `planejador` | alterar cronogramas |
| `orcar` | `orcamentista` | alterar bases de custos e orçamentos |
| `aprovar-linha-de-base` | `gestor` | aprovar e remover linhas de base |
| `medir` | `engenheiro` | acompanhar a obra e registrar medições |

Quem tem mais de uma função tem mais de um papel. A política de reserva exige ser da empresa: endpoint que esquecer de declarar política — ou rota que nem existe — não fica aberto, e sem token a resposta é 401, que não revela o que existe.

**4. Um recurso por agregado; alteração pelo nome do que se faz.** `GET` para ler, `POST` na coleção para criar, `DELETE` para remover — e cada alteração como um pedido com o nome dela: `POST /cronogramas/{id}/atividades`, `PATCH /cronogramas/{id}/atividades/{codigo}`, `DELETE /cronogramas/{id}/dependencias/{de}/{para}`. Não há `PUT` do agregado inteiro: é o domínio que confere cada passo, e o erro fala do passo que falhou. Toda alteração devolve o agregado como ficou.

**5. A versão do agregado é a ETag** — forte: `"3"` —, em toda leitura e toda alteração. Alterar e remover exigem `If-Match` com a versão lida: sem ele, 428; com uma versão que já não é a atual, 412, com a atual no corpo do problema. A versão é conferida antes de aplicar a alteração — quem leu uma versão velha fica sabendo que o agregado mudou, e não recebe um erro sobre um estado que nunca viu — e de novo pelo banco, ao gravar (ADR-0012). Na leitura de um agregado, `If-None-Match` com a versão atual dá 304.

**6. Criar exige `Idempotency-Key`** (ADR-0014). Alterar não: o `If-Match` já impede que a mesma alteração se aplique duas vezes.

**7. Todo erro sai em ProblemDetails** (RFC 9457), de um catálogo. O `type` aponta para a explicação em [`docs/api/problemas.md`](../api/problemas.md), o `title` é fixo por tipo, o `detail` diz o que houve naquele pedido, e o `traceId` acha o pedido nos registros. Os endpoints não tratam erro: lançam, e a tradução é uma só.

| Status | Quando |
|---|---|
| 400 | o pedido não é o combinado: JSON malformado, campo que falta ou sobra, cabeçalho inválido |
| 401 | sem token válido |
| 403 | o token não satisfaz a política do endpoint |
| 404 | não existe — ou é de outra empresa |
| 409 | já existe, ou está em uso |
| 412 | a versão informada já não é a atual |
| 415 | o corpo não é JSON |
| 422 | regra do negócio, com a mensagem do domínio; ou chave de idempotência usada em outro pedido |
| 428 | alteração sem a versão lida |
| 500 | erro interno: o detalhe fica nos registros; quem pediu recebe só o `traceId` |

**8. JSON estrito na entrada**, como na leitura dos documentos (ADR-0011): campo obrigatório que falta, nulo onde não cabe, campo que o contrato não tem e enum em número dão 400, com a mensagem dizendo qual. Pedidos e respostas são tipos próprios da API, separados do domínio.

**9. Cada recurso se registra sozinho.** O `Program` mapeia toda classe de recurso que encontra; um recurso novo não mexe em registro central. Em troca, um teste percorre os endpoints mapeados e falha se algum ficar sem política, se uma alteração não exigir `If-Match` ou se uma criação não exigir `Idempotency-Key` — e confere que o documento OpenAPI descreve esses cabeçalhos.

## Consequências

- **O cliente sabe o que fazer com cada resposta** sem ler o código: o `type` do problema é estável, a ETag diz a versão, a criação repetida é segura.
- **Trocar o provedor de identidade é configuração.** A API depende do formato do token, não de quem o emite.
- **Mais pedidos do que com um `PUT` do agregado inteiro.** Montar uma rede passo a passo são vários pedidos, cada um com a sua versão. Para criar a rede inteira de uma vez, o `POST` aceita as atividades e os vínculos junto.
- **A listagem devolve tudo**, sem paginação: uma empresa tem dezenas de obras, não milhares. Quando precisar, a paginação entra como parâmetro opcional, sem quebrar o contrato.
- **Erro do servidor não diz nada a quem pediu** além do `traceId` — de propósito.
