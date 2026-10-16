# ADR-0020 — A pilha em contêineres: imagens sem shell nem root, e o banco com os papéis de produção

**Status:** aceita

## Contexto

Até aqui, rodar o sistema era rodar cinco coisas à mão: o PostgreSQL, as migrações, a API, o processador e o front-end — cada um com a sua configuração, e o banco conectado como `postgres`, um superusuário que passa por cima da segurança por linha (ADR-0013). O que ia para um servidor nunca tinha sido montado como iria.

E os testes de ponta a ponta (ADR-0018) sobem a pilha a partir do código compilado, e não das imagens que seriam implantadas.

## Decisão

**1. Um Dockerfile, quatro imagens:** a API, o processador, o migrador e o front-end. O .NET compila uma vez, e cada alvo copia o que é seu. As dependências são restauradas numa camada só delas, que só se refaz quando um `.csproj` ou o `Directory.Packages.props` muda.

**2. Imagens sem shell e sem root.** A API e o processador rodam sobre a imagem *chiseled* do ASP.NET Core — sem gerenciador de pacotes, sem shell, com o usuário `app` —, e o migrador sobre a de dependências de runtime. Sem ICU: o sistema não depende da cultura do servidor — o formato brasileiro é montado à mão, e tudo o mais é invariável. O front-end vai no nginx sem privilégios, na porta 8080, com a política de segurança de conteúdo que só deixa rodar o que é da própria origem.

**3. As bases são fixadas pelo digest**, e não só pela tag: a mesma tag aponta para outra imagem amanhã. Atualizar é trocar o digest, numa linha, e ver os testes passarem com a base nova.

**4. As migrações são um executável à parte**, o pacote de migrações do EF Core, que roda num contêiner próprio e sai. Ninguém migra o banco ao subir a API: com duas réplicas, seriam duas migrações ao mesmo tempo, e a API teria de conectar como dono das tabelas. O migrador conecta como dono; a API e o processador só sobem depois que ele termina bem.

**5. O banco com os papéis de produção.** Na criação do volume, um script cria o papel da API e o do processador, e os privilégios padrão concedem a eles o que o dono criar depois: as tabelas para os dois, as funções só para o processador. Nenhum dos dois é dono de nada, e a segurança por linha vale para os dois — como em produção, e não como em desenvolvimento.

**6. O Compose sobe a pilha inteira**, com o painel do .NET Aspire recebendo os rastros, as métricas e os logs por OTLP (ADR-0019). Sem shell nas imagens, a saúde é perguntada de fora: `/saude/pronta` de cada serviço, como faria um orquestrador.

**7. Os testes de ponta a ponta rodam também contra as imagens.** Os mesmos testes, sem o Playwright subir nada: a pilha é a do Compose, a API vem pela mesma origem, pelo nginx, e o provedor de identidade é o dos testes — a chave pública deles, que só a pilha do Compose aceita. O que o navegador não enxerga é conferido à parte, antes dos testes: que a API e o processador estão conectados com os papéis deles, que nenhum dos dois passa por cima da segurança por linha, que a API não alcança as funções que atravessam empresas e que o nginx manda os cabeçalhos de segurança.

## Consequências

- **O que vai para produção é o que os testes de ponta a ponta usaram**: as mesmas imagens, o mesmo nginx, os mesmos papéis no banco.
- **Uma imagem sem shell não se depura por dentro.** Não há `docker exec` com `sh`: o diagnóstico é pelo que ela publica — os rastros, as métricas, os logs e as verificações de saúde.
- **Sem verificação de saúde no Dockerfile.** Ela precisaria de um programa dentro da imagem só para isso; o orquestrador pergunta por HTTP.
- **O script dos papéis roda só na criação do volume.** Banco que já existia não ganha os papéis: quem implanta os cria, como fazia antes. O script é a receita.
- **A chave do provedor de identidade da pilha do Compose é pública.** Serve para a máquina de quem desenvolve e para o CI; em qualquer outro lugar, o provedor é o da empresa, e a configuração vem do ambiente.
- **O primeiro build é lento** — restaurar o .NET e o npm do zero —; os seguintes reaproveitam as camadas.
