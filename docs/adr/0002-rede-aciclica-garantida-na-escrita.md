# ADR-0002 — A rede é acíclica por construção

**Status:** aceita

## Contexto

O caminho crítico só se calcula numa rede sem ciclos: o algoritmo percorre as atividades numa ordem em que toda predecessora vem antes da sucessora — a ordem topológica —, e essa ordem não existe se uma atividade depender, direta ou indiretamente, de si mesma.

Havia duas formas de lidar com isso:

1. **Validar na leitura.** O cronograma aceita qualquer vínculo; quem calcula detecta o ciclo e falha.
2. **Garantir na escrita.** O cronograma recusa o vínculo que fecharia um ciclo, no momento em que ele é criado.

## Decisão

Garantir na escrita. `Cronograma.AdicionarDependencia` procura um caminho da sucessora até a predecessora antes de aceitar o vínculo; se encontrar, recusa e mostra o ciclo inteiro na mensagem.

O ciclo é recusado **qualquer que seja o tipo de vínculo**. Matematicamente, um laço formado só por vínculos início-início pode ter solução; nenhum software de planejamento de mercado o aceita, e a ordem topológica que o cálculo exige não distingue tipos.

## Consequências

- **Não existe instante em que o agregado contenha um ciclo.** O cálculo do caminho crítico pode assumir a rede acíclica como dado, em vez de tratar o caso em todo lugar que a percorre. A checagem que ele mantém é defensiva e nunca deveria disparar.
- **O erro aparece onde foi cometido.** Com validação na leitura, o engenheiro cria o vínculo errado, segue cadastrando, e só descobre o problema ao calcular — sem saber qual dos últimos vinte vínculos foi o culpado. Aqui, a mensagem chega na hora e diz o caminho: `FUN → EST → ALV → FUN`.
- **Custo:** cada vínculo novo faz uma busca em largura, O(V + E). Para um cronograma de obra — centenas a poucos milhares de atividades —, é imperceptível. Se um dia houver importação em lote de dezenas de milhares de vínculos, o caminho é validar o lote inteiro de uma vez com uma única ordenação topológica, e não relaxar a regra.
- **Vínculo é imutável.** Mudar tipo ou defasagem é remover e recriar. A validação acontece num lugar só, e não existe vínculo que tenha sido válido um dia e deixado de ser.
