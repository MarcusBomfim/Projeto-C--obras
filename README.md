# Planejamento e Controle de Obras

[![CI](https://github.com/MarcusBomfim/planejamento-obras/actions/workflows/ci.yml/badge.svg)](https://github.com/MarcusBomfim/planejamento-obras/actions/workflows/ci.yml)

Planejamento de obra como o engenheiro de planejamento faz: rede de precedências, caminho crítico, calendário com feriados — e, nas próximas partes, orçamento, cronograma físico-financeiro e valor agregado.

C# 14 · .NET 10 · xUnit v3 · GitHub Actions

## O problema

Obra atrasa por causa de poucas atividades — as que seguram a data de entrega. O problema é saber quais são. Uma alvenaria que atrasa três dias pode não mudar nada; uma estrutura que atrasa um dia atrasa a entrega em um dia.

O método do caminho crítico responde três perguntas:

- **quais atividades não podem atrasar** — as críticas;
- **quanto cada uma das outras pode escorregar** — a folga, total e livre;
- **quando cada uma começa e termina** — no calendário real, com fim de semana e feriado.

Este projeto é a continuação do [gestao-obras](https://github.com/MarcusBomfim/gestao-obras), que registra o que aconteceu no canteiro e cita, entre as suas limitações, a falta de um cronograma de verdade. Aqui se planeja o que vai acontecer.

## Exemplo

```csharp
var cronograma = new Cronograma("Residencial Vista Serra");

cronograma.AdicionarAtividade("MOB", "Mobilização do canteiro", 5);
cronograma.AdicionarAtividade("FUN", "Fundações", 10);
cronograma.AdicionarAtividade("EST", "Estrutura", 20);
cronograma.AdicionarAtividade("ALV", "Alvenaria", 15);
cronograma.AdicionarAtividade("ELE", "Instalações elétricas", 12);
cronograma.AdicionarAtividade("REB", "Reboco", 8);
cronograma.AdicionarAtividade("PIN", "Pintura", 6);
cronograma.AdicionarAtividade("ENT", "Entrega das chaves", 0);

cronograma.AdicionarDependencia("MOB", "FUN");
cronograma.AdicionarDependencia("FUN", "EST");
cronograma.AdicionarDependencia("EST", "ALV");
cronograma.AdicionarDependencia("EST", "ELE");
cronograma.AdicionarDependencia("ALV", "REB");
cronograma.AdicionarDependencia("ELE", "REB");
cronograma.AdicionarDependencia("REB", "PIN");
cronograma.AdicionarDependencia("PIN", "ENT");

var calendario = CalendarioDeTrabalho.DeSegundaASexta(
[
    new DateOnly(2026, 10, 12), new DateOnly(2026, 11, 2),
    new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 25),
    new DateOnly(2027, 1, 1),
]);

var obra = ProgramacaoDoCronograma.Programar(cronograma, new DateOnly(2026, 10, 5), calendario);
```

Resultado: de 05/10/2026 a 07/01/2027, 64 dias úteis. Em negrito, o caminho crítico.

| | Atividade | Dias | Início | Término | Folga |
|---|---|--:|---|---|--:|
| **MOB** | Mobilização do canteiro | 5 | seg. 05/10 | sex. 09/10 | — |
| **FUN** | Fundações | 10 | ter. 13/10 | seg. 26/10 | — |
| **EST** | Estrutura | 20 | ter. 27/10 | qua. 25/11 | — |
| **ALV** | Alvenaria | 15 | qui. 26/11 | qua. 16/12 | — |
| ELE | Instalações elétricas | 12 | qui. 26/11 | sex. 11/12 | 3 (até qua. 16/12) |
| **REB** | Reboco | 8 | qui. 17/12 | ter. 29/12 | — |
| **PIN** | Pintura | 6 | qua. 30/12 | qui. 07/01 | — |
| **ENT** | Entrega das chaves | 0 | qui. 07/01 | qui. 07/01 | — |

As fundações começam na terça porque a segunda, 12/10, é feriado. A elétrica pode atrasar três dias úteis sem mexer na entrega; a alvenaria, nenhum. E a entrega das chaves aparece no último dia da pintura, não no dia seguinte — o porquê está na [ADR-0003](docs/adr/0003-tempo-em-dias-uteis-e-intervalos-semiabertos.md).

## Roteiro

O projeto é construído em partes, cada uma entregue funcionando, testada e documentada.

| Parte | Conteúdo | |
|--:|---|---|
| 1 | Cronograma: rede de precedências, caminho crítico, calendário e datas | ✅ |
| 2 | EAP, orçamento com composições de custo e BDI, cronograma físico-financeiro | |
| 3 | Linha de base, avanço físico e valor agregado (PV, EV, AC, SPI, CPI, EAC) | |
| 4 | Persistência com EF Core e PostgreSQL, testes de integração com Testcontainers | |
| 5 | API REST: JWT, autorização por política, multi-tenant, ProblemDetails, idempotência e concorrência otimista | |
| 6 | Eventos de domínio, outbox e processamento em segundo plano | |
| 7 | Front-end em React: Gantt com caminho crítico, curva S e painel de valor agregado | |
| 8 | Observabilidade com OpenTelemetry, Docker e entrega contínua | |

## O que a Parte 1 entrega

- **Os quatro tipos de vínculo** — término-início, início-início, término-término e início-término —, com defasagem positiva (a cura do concreto antes da desforma) ou negativa (a pintura que começa antes de o reboco acabar).
- **Rede acíclica por construção.** O vínculo que fecharia um ciclo é recusado na hora, com o ciclo inteiro na mensagem: `FUN → EST → ALV → FUN`.
- **Caminho crítico em O(V log V + E):** ordem topológica, ida, volta, folga total, folga livre e os vínculos críticos — vínculos, e não "o caminho", porque pode haver mais de um. O log vem do desempate pela ordem de cadastro, que faz o resultado sair igual a cada cálculo.
- **Calendário de trabalho** com os dias da semana úteis e os feriados.
- **Programação em datas**, com regra explícita para a data dos marcos.

## Decisões

As decisões que mudam o desenho do sistema estão registradas em [`docs/adr`](docs/adr):

- [ADR-0001](docs/adr/0001-linguagem-ubiqua-em-portugues.md) — o código fala a língua do domínio: `FolgaTotal`, e não `TotalFloat`.
- [ADR-0002](docs/adr/0002-rede-aciclica-garantida-na-escrita.md) — o ciclo é recusado na escrita, e não descoberto no cálculo.
- [ADR-0003](docs/adr/0003-tempo-em-dias-uteis-e-intervalos-semiabertos.md) — o cálculo é em dias úteis com intervalos semiabertos; datas só na saída; e a regra de em que dia aparece um marco.

E algumas que não precisaram de ADR:

- **O domínio não depende de nada** além da biblioteca padrão do .NET. Um teste de arquitetura falha se ele passar a referenciar qualquer outro assembly.
- **O cálculo é um serviço sem estado**, e não um método do `Cronograma`. A análise é derivada da rede — uma fotografia —, e não estado a manter sincronizado.
- **Objetos de valor são `record` de classe, selados e só com `get`:** nem o `with` nem o `default` produzem um código de atividade inválido.
- **Aviso é erro.** Analisadores no nível `latest-recommended`, estilo verificado no build e `dotnet format` conferido no CI.

## Testes

- **Exemplos calculados à mão.** Cada número esperado foi obtido com ida e volta no papel: a obra acima, os quatro tipos de vínculo, folga livre menor que a total, marcos e calendário com feriado.
- **Propriedades em 400 redes sorteadas.** Sementes fixas, até 30 atividades, um terço de marcos, os quatro tipos de vínculo e defasagens negativas. Em cada rede se confere o que todo cálculo correto tem que satisfazer: nenhum vínculo violado, nem no cedo nem no tarde; nenhuma folga sem motivo; folga livre entre zero e a total; toda data é dia útil dentro da obra; nenhum marco antes do que o empurra.
- **Arquitetura.** O domínio só referencia a biblioteca padrão; toda classe pública é selada, estática ou abstrata; nenhuma propriedade tem `set` público — o estado só muda pelos métodos do domínio.

## Como rodar

Pré-requisito: [SDK do .NET 10](https://dotnet.microsoft.com/download).

```bash
dotnet test --solution PlanejamentoObras.slnx
```

## Estrutura

```text
src/PlanejamentoObras.Dominio/               regras do negócio, sem dependências
  Calendarios/                               dias úteis e feriados
  Cronogramas/                               rede, caminho crítico e programação
tests/PlanejamentoObras.Dominio.Testes/      exemplos e propriedades
tests/PlanejamentoObras.Arquitetura.Testes/  regras que o domínio não pode quebrar
docs/adr/                                    decisões de arquitetura
```
