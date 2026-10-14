# Planejamento e Controle de Obras

[![CI](https://github.com/MarcusBomfim/Projeto-C--obras/actions/workflows/ci.yml/badge.svg)](https://github.com/MarcusBomfim/Projeto-C--obras/actions/workflows/ci.yml)

Planejamento e controle de obra como o engenheiro de planejamento faz: rede de precedências e caminho crítico, orçamento com composições de custo e BDI, o cronograma físico-financeiro que junta os dois — e a obra medida mês a mês contra o plano aprovado, com boletim de medição e valor agregado.

C# 14 · .NET 10 · EF Core 10 · PostgreSQL 17 · Testcontainers · xUnit v3 · GitHub Actions

## O problema

Obra atrasa por causa de poucas atividades — as que seguram a data de entrega. O problema é saber quais são. Uma alvenaria que atrasa três dias pode não mudar nada; uma estrutura que atrasa um dia atrasa a entrega em um dia.

O método do caminho crítico responde três perguntas:

- **quais atividades não podem atrasar** — as críticas;
- **quanto cada uma das outras pode escorregar** — a folga, total e livre;
- **quando cada uma começa e termina** — no calendário real, com fim de semana e feriado.

O orçamento responde **quanto** a obra custa. Junto com o cronograma, responde a pergunta que o cliente e o banco fazem: **quanto se gasta em cada mês** — o cronograma físico-financeiro.

Aprovado, o plano vira régua: a **linha de base**. Todo mês a obra é medida contra ela. O boletim de medição diz quanto se paga; o **valor agregado** diz se a obra está atrasada, se está gastando mais que o orçado e onde vai terminar.

Este projeto é a continuação do [gestao-obras](https://github.com/MarcusBomfim/gestao-obras), que registra o que aconteceu no canteiro e cita, entre as suas limitações, a falta de um cronograma de verdade. Aqui se planeja o que vai acontecer.

## Exemplo

O exemplo inteiro — rede, base de custos, orçamento, físico-financeiro, medições e valor agregado — está em [`ExemploDoReadmeTests`](tests/PlanejamentoObras.Dominio.Testes/ExemploDoReadmeTests.cs). É um teste: se o código mudar e algum número desta página deixar de ser verdade, o build quebra.

### Cronograma

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

### Orçamento

Os preços vêm de uma base de custos: insumos com preço e composições que dizem quanto de cada insumo entra numa unidade de serviço. Uma composição pode usar outra — a argamassa entra na alvenaria e no reboco.

```csharp
var baseDeCustos = new BaseDeCustos("Tabela própria — setembro de 2026", new DateOnly(2026, 9, 1));

baseDeCustos.AdicionarInsumo("BLO", "Bloco cerâmico de vedação 9x19x19 cm", "un", TipoDeInsumo.Material, 1.32m);
baseDeCustos.AdicionarInsumo("PED", "Pedreiro, com encargos sociais", "h", TipoDeInsumo.MaoDeObra, 28.74m);
// … cimento, cal, areia, servente, betoneira

baseDeCustos.AdicionarComposicao("ALV", "Alvenaria de vedação com bloco cerâmico 9x19x19 cm", "m²");
baseDeCustos.AdicionarItem("ALV", "BLO", 13.5m);
baseDeCustos.AdicionarItem("ALV", "ARG", 0.012m); // a argamassa é outra composição
baseDeCustos.AdicionarItem("ALV", "PED", 0.80m);
baseDeCustos.AdicionarItem("ALV", "SER", 0.40m);

var bdi = Bdi.PelaFormulaDoTcu(new ParcelasDoBdi(
    administracaoCentral: 4.00m, seguros: 0.40m, riscos: 1.27m, garantias: 0.40m,
    despesasFinanceiras: 1.23m, lucro: 7.40m, tributos: 6.15m)); // 22,88 %

var orcamento = new Orcamento("Residencial Vista Serra", bdi);
orcamento.AdicionarEtapa("Vedações e revestimentos");      // 3
orcamento.AdicionarItem("3", "ALV", 2430.50m);             // 3.1
orcamento.VincularAtividade("3.1", "ALV");                 // a atividade do cronograma
// … as outras etapas e itens

var planilha = CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos);
```

A composição da alvenaria, aberta — a argamassa entra pelo custo dela, R$ 646,06 o m³:

| Item | Coeficiente | Custo unitário | Custo parcial |
|---|--:|--:|--:|
| Bloco cerâmico 9x19x19 cm (un) | 13,5 | 1,32 | 17,82 |
| Argamassa 1:2:8 (m³) | 0,012 | 646,06 | 7,75 |
| Pedreiro (h) | 0,80 | 28,74 | 22,99 |
| Servente (h) | 0,40 | 21,46 | 8,58 |
| **Alvenaria (m²)** | | | **57,14** |

E a planilha, com BDI de 22,88 % e, no elevador, BDI diferenciado de 12 %:

| | Descrição | Un | Quantidade | Custo un. | Preço un. | Total |
|---|---|---|--:|--:|--:|--:|
| **1** | **Serviços preliminares** | | | | | **4.950,00** |
| 1.1 | Limpeza manual do terreno | m² | 1.250,00 | 3,22 | 3,96 | 4.950,00 |
| **2** | **Infraestrutura e estrutura** | | | | | **627.302,40** |
| 2.1 | Fundações: estacas, blocos e vigas baldrame | vb | 1,00 | 98.500,00 | 121.036,80 | 121.036,80 |
| 2.2 | Estrutura de concreto armado | vb | 1,00 | 412.000,00 | 506.265,60 | 506.265,60 |
| **3** | **Vedações e revestimentos** | | | | | **447.041,86** |
| 3.1 | Alvenaria de vedação com bloco cerâmico 9x19x19 cm | m² | 2.430,50 | 57,14 | 70,21 | 170.645,40 |
| 3.2 | Emboço de parede, argamassa 1:2:8, 2 cm | m² | 4.861,00 | 46,27 | 56,86 | 276.396,46 |
| **4** | **Instalações e acabamentos** | | | | | **184.934,40** |
| 4.1 | Instalações elétricas | vb | 1,00 | 86.300,00 | 106.045,44 | 106.045,44 |
| 4.2 | Pintura interna e externa | vb | 1,00 | 64.200,00 | 78.888,96 | 78.888,96 |
| **5** | **Equipamentos** | | | | | **403.200,00** |
| 5.1 | Elevador de passageiros, 8 pessoas, fornecido e instalado | un | 2,00 | 180.000,00 | 201.600,00 | 403.200,00 |
| | **Total da obra** | | | | | **1.667.428,66** |

Toda linha se refaz com uma calculadora a partir dos números da própria planilha. Na alvenaria, 2.430,50 × 70,21 = 170.645,405 — meio centavo exato, que a NBR 5891 manda para o par: 170.645,40. A regra e os pontos de arredondamento estão na [ADR-0004](docs/adr/0004-dinheiro-e-arredondamento.md).

Pela curva ABC, quatro dos oito itens — estrutura, elevador, reboco e alvenaria — somam 81 % do valor da obra. É neles que a negociação e a auditoria olham primeiro.

### Físico-financeiro

```csharp
var cedo = CalculadoraDoFisicoFinanceiro.Calcular(planilha, obra, DatasDoCronograma.Cedo);
var tarde = CalculadoraDoFisicoFinanceiro.Calcular(planilha, obra, DatasDoCronograma.Tarde);
```

Cada item se distribui pelos dias úteis da atividade a que está vinculado. A estrutura, de 20 dias, tem 4 em outubro e 16 em novembro; o elevador, vinculado à entrega das chaves, entra inteiro em janeiro.

| Mês | Cedo | Acumulado | Tarde | Acumulado |
|---|--:|--:|--:|--:|
| out./2026 | 227.239,92 | 13,6 % | 227.239,92 | 13,6 % |
| nov./2026 | 465.652,92 | 41,6 % | 439.141,56 | 40,0 % |
| dez./2026 | 518.743,18 | 72,7 % | 545.254,54 | 72,7 % |
| jan./2027 | 455.792,64 | 100,0 % | 455.792,64 | 100,0 % |

A diferença entre as duas curvas é a folga da elétrica: pelas datas tarde, os três dias dela em novembro passam para dezembro. A soma dos meses é o total do orçamento ao centavo, nas duas — ver [ADR-0006](docs/adr/0006-fisico-financeiro-derivado-do-cronograma.md).

### Linha de base, medição e valor agregado

Aprovado o plano, ele é congelado numa linha de base, e a obra passa a ser medida contra ela, mês a mês: a quantidade executada de cada item e o custo real dele.

```csharp
var linhaDeBase = LinhaDeBase.Estabelecer("Linha de base 1 — contrato", new DateOnly(2026, 9, 28), planilha, obra);
var acompanhamento = new Acompanhamento(linhaDeBase);

acompanhamento.RegistrarMedicao(new DateOnly(2026, 10, 30),
    new LancamentoDeMedicao("1.1", 1250m, CustoReal: 4_300m),
    new LancamentoDeMedicao("2.1", 1m, CustoReal: 104_900m),
    new LancamentoDeMedicao("2.2", 0.15m, CustoReal: 63_000m));

acompanhamento.RegistrarMedicao(new DateOnly(2026, 11, 30),
    new LancamentoDeMedicao("2.2", 0.55m, CustoReal: 232_000m),
    new LancamentoDeMedicao("3.1", 300m, CustoReal: 18_900m),
    new LancamentoDeMedicao("4.1", 0.10m, CustoReal: 9_600m));

var boletim = EmissaoDoBoletim.Emitir(acompanhamento, medicao: 2);
var analise = CalculadoraDeValorAgregado.Calcular(acompanhamento);
```

O boletim de novembro, a preço de contrato — o valor da medição é o que se paga no mês:

| Item | No mês | Acumulado | Valor no mês | Valor acumulado |
|---|--:|--:|--:|--:|
| 2.2 Estrutura de concreto armado | 0,55 vb | 0,70 de 1,00 | 278.446,08 | 354.385,92 |
| 3.1 Alvenaria de vedação com bloco cerâmico 9x19x19 cm | 300,00 m² | 300,00 de 2.430,50 | 21.063,00 | 21.063,00 |
| 4.1 Instalações elétricas | 0,10 vb | 0,10 de 1,00 | 10.604,54 | 10.604,54 |
| **Medição de novembro** | | | **310.113,62** | **512.040,26** |

Medir além do contratado é recusado — exige aditivo —, e erro numa medição já registrada se corrige com glosa na seguinte, como no boletim de papel. O valor do mês é a diferença entre acumulados, para o item todo medido valer exatamente o contratado ([ADR-0008](docs/adr/0008-medicao-historico-que-so-cresce.md)).

E o valor agregado em novembro, **a custo**: é com o custo orçado do que foi executado que o custo real se compara, e não com o preço de venda, que inclui o BDI ([ADR-0009](docs/adr/0009-valor-agregado-a-custo-e-prazo-agregado.md)).

| Indicador | Novembro |
|---|--:|
| Valor planejado (VP, ou PV) | 563.875,75 |
| Valor agregado (VA, ou EV) | 416.697,00 |
| Custo real (CR, ou AC) | 432.700,00 |
| Índice de desempenho de prazo (IDP, ou SPI) | 0,74 |
| Índice de desempenho de custo (IDC, ou CPI) | 0,96 |
| Estimativa no término (ENT, ou EAC) | 1.442.159,13 |
| Variação no término (VNT, ou VAC) | −53.336,89 |
| Prazo agregado | 1,61 mês, em 2 decorridos |
| Duração estimada | 4,96 meses, de 4 planejados |

A obra está atrasada e acima do custo: em dois meses, executou o que o plano previa para 1,61, e gasta R$ 1,04 para cada real orçado. Mantido o ritmo, termina em cinco meses em vez de quatro, R$ 53 mil acima do orçamento. Por etapa, quem mais segura é instalações e acabamentos, com IDP de 0,40.

O prazo agregado mede o atraso em meses. Ao contrário do IDP clássico, ele não volta a 1 quando a obra termina atrasada — VA e VP chegam os dois ao orçamento no fim, mas o tempo gasto não volta.

### Gravação

Cada agregado é gravado inteiro, como um documento `jsonb` numa tabela própria do PostgreSQL, e volta pelas mesmas regras da escrita: um cronograma com ciclo, editado à mão no banco, é recusado na leitura com o id e o motivo ([ADR-0010](docs/adr/0010-identidade-e-reconstituicao-dos-agregados.md), [ADR-0011](docs/adr/0011-agregados-gravados-como-documentos-jsonb.md)).

```csharp
servicos.AdicionarPersistencia(conexao);                  // contexto e repositórios, um por requisição

await cronogramas.Adicionar(cronograma);                  // versão 1

var lido = await cronogramas.Obter(cronograma.Id);        // o agregado e a versão lida
lido!.Agregado.AlterarDuracaoDaAtividade("FUN", 12);
await cronogramas.Atualizar(lido.Agregado, lido.Versao);  // versão 2
```

Se outra pessoa gravou o mesmo cronograma entre a leitura e a gravação, a gravação é recusada com `ConflitoDeConcorrencia`: "O cronograma … mudou desde a versão 1: está na versão 2. Leia de novo e refaça a alteração." A versão é conferida dentro do próprio `update`: dez gravações simultâneas a partir da mesma versão, e passa exatamente uma ([ADR-0012](docs/adr/0012-concorrencia-otimista-com-versao-explicita.md)).

## Roteiro

O projeto é construído em partes, cada uma entregue funcionando, testada e documentada.

| Parte | Conteúdo | |
|--:|---|---|
| 1 | Cronograma: rede de precedências, caminho crítico, calendário e datas | ✅ |
| 2 | Base de custos e composições, orçamento com EAP e BDI, cronograma físico-financeiro | ✅ |
| 3 | Linha de base, medições, boletim de medição e valor agregado (PV, EV, AC, SPI, CPI, EAC) | ✅ |
| 4 | Persistência com EF Core e PostgreSQL, testes de integração com Testcontainers | ✅ |
| 5 | API REST: JWT, autorização por política, multi-tenant, ProblemDetails, idempotência e concorrência otimista | |
| 6 | Eventos de domínio, outbox e processamento em segundo plano | |
| 7 | Front-end em React: Gantt com caminho crítico, curva S e painel de valor agregado | |
| 8 | Observabilidade com OpenTelemetry, Docker e entrega contínua | |

## O que já está pronto

**Parte 1 — cronograma**

- **Os quatro tipos de vínculo** — término-início, início-início, término-término e início-término —, com defasagem positiva (a cura do concreto antes da desforma) ou negativa (a pintura que começa antes de o reboco acabar).
- **Rede acíclica por construção.** O vínculo que fecharia um ciclo é recusado na hora, com o ciclo inteiro na mensagem: `FUN → EST → ALV → FUN`.
- **Caminho crítico em O(V log V + E):** ordem topológica, ida, volta, folga total, folga livre e os vínculos críticos — vínculos, e não "o caminho", porque pode haver mais de um. O log vem do desempate pela ordem de cadastro, que faz o resultado sair igual a cada cálculo.
- **Calendário de trabalho** com os dias da semana úteis e os feriados.
- **Programação em datas**, com regra explícita para a data dos marcos.

**Parte 2 — orçamento e físico-financeiro**

- **Base de custos** com insumos (material, mão de obra, equipamento) e composições de custo unitário, que podem usar outras composições em quantos níveis for preciso. Composição que usaria a si mesma, direta ou indiretamente, é recusada com o ciclo na mensagem; insumo ou composição em uso não pode ser removido.
- **Composição analítica**, linha a linha, como vai para o anexo do orçamento.
- **Orçamento com EAP** de etapas e subetapas, endereçado pela numeração da planilha (2.1.3). O item pode ser uma composição ou um insumo.
- **BDI pela fórmula do TCU** (Acórdão 2.622/2013) ou informado pronto, e **BDI diferenciado** por item.
- **Curva ABC** dos itens.
- **Cronograma físico-financeiro** mensal, por item e por etapa, pelas datas cedo ou tarde.
- **Tudo fecha ao centavo:** a composição soma as parcelas, a etapa soma os itens, os meses somam o orçamento.

**Parte 3 — linha de base, medição e valor agregado**

- **Linha de base** congelada: quantidade, preço e custo de cada item, e o planejado de cada mês, a preço e a custo. Revisar o orçamento ou o cronograma depois não muda a régua.
- **Medições mensais** por item: quantidade executada e custo real. O histórico só cresce, erro se corrige com glosa, e medir além do contratado é recusado.
- **Boletim de medição** de cada item e etapa — anterior, no mês, acumulado e saldo —, com o valor do mês que se paga.
- **Valor agregado** da obra, de cada etapa e de cada item: VP, VA, CR, variações de prazo e custo, IDP, IDC, três estimativas no término, EPT, VNT e IDPT.
- **Prazo agregado** (earned schedule): o atraso em meses e a duração estimada da obra.
- **Curvas S** de valor planejado, valor agregado e custo real, mês a mês.

**Parte 4 — persistência**

- **Identidade própria em cada agregado** — `CronogramaId`, `OrcamentoId` e os outros —: o id de um orçamento passado no lugar do de um cronograma é erro de compilação. Guid versão 7, que nasce em ordem e não fragmenta o índice.
- **Um documento `jsonb` por agregado**, numa tabela por tipo, com ao lado só o que o banco precisa enxergar: identidade, versão, formato, datas, nome — e a chave estrangeira do acompanhamento para a linha de base, que não sai do banco enquanto houver acompanhamento contra ela.
- **A leitura passa pelas regras.** O agregado é reconstituído pelo domínio, com uma só checagem de ciclo, em O(V + E), para a rede inteira. O BDI e a linha de base voltam como foram gravados, sem recalcular.
- **Leitura estrita e formato com número.** Campo que falta, nulo onde não cabe, campo desconhecido ou documento de outra versão: a leitura falha, com o id e o motivo, em vez de inventar um zero.
- **Concorrência otimista** com a versão conferida no próprio `update`, sem janela entre conferir e gravar; o conflito diz em que versão o agregado está.
- **Exceções com nome** para o que o banco responde: conflito, não encontrado, já existe, em uso, documento inválido.
- **Migrações do EF Core** no repositório, com o `dotnet-ef` fixado como ferramenta local.

## Decisões

As decisões que mudam o desenho do sistema estão registradas em [`docs/adr`](docs/adr):

- [ADR-0001](docs/adr/0001-linguagem-ubiqua-em-portugues.md) — o código fala a língua do domínio: `FolgaTotal`, e não `TotalFloat`.
- [ADR-0002](docs/adr/0002-rede-aciclica-garantida-na-escrita.md) — o ciclo é recusado na escrita, e não descoberto no cálculo.
- [ADR-0003](docs/adr/0003-tempo-em-dias-uteis-e-intervalos-semiabertos.md) — o cálculo é em dias úteis com intervalos semiabertos; datas só na saída; e a regra de em que dia aparece um marco.
- [ADR-0004](docs/adr/0004-dinheiro-e-arredondamento.md) — dinheiro em `decimal`, e arredonda-se o que aparece na planilha, pela NBR 5891.
- [ADR-0005](docs/adr/0005-orcamento-referencia-a-base-de-custos.md) — o orçamento aponta para a base de custos em vez de copiar preços; o BDI entra com as duas casas impressas.
- [ADR-0006](docs/adr/0006-fisico-financeiro-derivado-do-cronograma.md) — o físico-financeiro é calculado do cronograma, e os centavos se rateiam pelo maior resto.
- [ADR-0007](docs/adr/0007-linha-de-base-congelada.md) — a linha de base é uma cópia congelada do plano, a preço e a custo, sem referência a nada vivo.
- [ADR-0008](docs/adr/0008-medicao-historico-que-so-cresce.md) — a medição é um histórico que só cresce, e o valor do mês é a diferença entre acumulados.
- [ADR-0009](docs/adr/0009-valor-agregado-a-custo-e-prazo-agregado.md) — valor agregado a custo, estimativas a partir dos valores, e prazo agregado em meses.
- [ADR-0010](docs/adr/0010-identidade-e-reconstituicao-dos-agregados.md) — cada agregado tem identidade do seu tipo, e a leitura do banco passa pelas regras; o que foi aprovado volta como foi aprovado.
- [ADR-0011](docs/adr/0011-agregados-gravados-como-documentos-jsonb.md) — cada agregado é um documento `jsonb`, lido de forma estrita e com formato numerado; os testes usam PostgreSQL de verdade.
- [ADR-0012](docs/adr/0012-concorrencia-otimista-com-versao-explicita.md) — concorrência otimista com versão explícita, conferida pelo banco num comando só.

E algumas que não precisaram de ADR:

- **O domínio não depende de nada** além da biblioteca padrão do .NET. Um teste de arquitetura falha se ele passar a referenciar qualquer outro assembly.
- **Cálculos são serviços sem estado**, e não métodos dos agregados. Caminho crítico, custo, planilha, físico-financeiro, boletim e valor agregado são derivados — fotografias —, e não estado a manter sincronizado.
- **Objetos de valor são `record` de classe, selados e só com `get`:** nem o `with` nem o `default` produzem um código de atividade inválido. A exceção é `Dinheiro`, um `record struct` — para ele, o `default` é zero real, um valor legítimo.
- **Erros de quem usa vêm todos de uma vez.** Os códigos que faltam na base, os itens sem atividade: uma mensagem com a lista, e não uma rodada de corrigir e recalcular para cada um.
- **Aviso é erro.** Analisadores no nível `latest-recommended`, estilo verificado no build e `dotnet format` conferido no CI.

## Testes

- **Exemplos calculados à mão.** Cada número esperado foi obtido no papel: a obra acima, os quatro tipos de vínculo, folga livre menor que a total, marcos, composições com auxiliares, a fórmula do BDI, a planilha inteira, o físico-financeiro atravessando feriado e virada de mês, o boletim e o valor agregado de uma obra que termina um mês atrasada.
- **Propriedades em 400 casos sorteados**, com sementes fixas, em cinco frentes:
  - **caminho crítico** — nenhum vínculo violado, nem no cedo nem no tarde; nenhuma folga sem motivo; folga livre entre zero e a total;
  - **datas** — toda data é dia útil dentro da obra; nenhum marco antes do que o empurra;
  - **planilha** — cada número se refaz à mão, refeito pelo teste com `decimal.Round`, sem usar o código que está conferindo;
  - **físico-financeiro** — fecha com o orçamento ao centavo, nas duas curvas, e põe cada real no mês em que o serviço acontece.
  - **valor agregado** — com glosas, meses pulados e obras fora do prazo: cada valor refeito a partir dos lançamentos, as etapas somando os itens, e o prazo agregado contando a mesma história do valor agregado.
- **Persistência contra PostgreSQL de verdade**, num contêiner do Testcontainers: o esquema que as migrações criam e as restrições que o banco garante; obras sorteadas que vão ao `jsonb` e voltam calculando o mesmo; a versão que recusa quem grava por cima — dez gravações simultâneas, e passa uma —; e um documento estragado à mão no banco, recusado na leitura. Sem Docker, esses testes são pulados, com o motivo; no CI, falham.
- **O formato do que se grava.** O JSON de cada agregado é comparado com o formato esperado; a leitura estrita recusa campo a mais, campo a menos, nulo, enum desconhecido, tipo de nó desconhecido e documento de outra versão; 400 obras sorteadas fazem ida e volta pelo documento. E um teste sem banco falha se o modelo do EF Core mudar sem migração.
- **Arquitetura.** O domínio só referencia a biblioteca padrão; toda classe pública é selada, estática ou abstrata; nenhuma propriedade tem `set` público — o estado só muda pelos métodos do domínio; todo agregado tem identidade própria; e só a infraestrutura e os testes do domínio enxergam os internos dele.

## Como rodar

Pré-requisitos: [SDK do .NET 10](https://dotnet.microsoft.com/download) e, para os testes de integração, Docker.

```bash
dotnet test --solution PlanejamentoObras.slnx
```

Sem Docker, os testes que precisam do PostgreSQL são pulados, com o motivo, e o resto roda.

Para criar o banco num PostgreSQL local, com o `dotnet-ef` que o repositório fixa:

```bash
docker run -d --name planejamento-obras-db -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17.6-alpine
dotnet tool restore
dotnet ef database update --project src/PlanejamentoObras.Infraestrutura --connection "Host=localhost;Database=planejamento_obras;Username=postgres;Password=postgres"
```

## Estrutura

```text
src/PlanejamentoObras.Dominio/                  regras do negócio, sem dependências
  Comum/                                        dinheiro, identificador e exceção de domínio
  Calendarios/                                  dias úteis e feriados
  Cronogramas/                                  rede, caminho crítico e programação
  Custos/                                       insumos, composições e custo unitário
  Orcamentos/                                   EAP, BDI, planilha e curva ABC
  FisicoFinanceiro/                             o orçamento distribuído nos meses
  LinhasDeBase/                                 o plano aprovado, congelado
  Medicoes/                                     medições mensais e boletim de medição
  ValorAgregado/                                desempenho de prazo e custo contra a linha de base
src/PlanejamentoObras.Infraestrutura/           persistência no PostgreSQL
  Documentos/                                   os agregados em JSON, e de volta
  Persistencia/                                 contexto, migrações, repositórios e concorrência
tests/PlanejamentoObras.Dominio.Testes/         exemplos, propriedades e o exemplo deste README
tests/PlanejamentoObras.Infraestrutura.Testes/  documentos e repositórios, contra PostgreSQL de verdade
tests/PlanejamentoObras.Arquitetura.Testes/     regras que o domínio não pode quebrar
docs/adr/                                       decisões de arquitetura
```
