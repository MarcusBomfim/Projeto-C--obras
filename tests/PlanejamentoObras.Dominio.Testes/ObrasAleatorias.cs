using PlanejamentoObras.Dominio.Calendarios;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Dominio.Testes.Cronogramas;
using PlanejamentoObras.Dominio.Testes.Orcamentos;

namespace PlanejamentoObras.Dominio.Testes;

/// <summary>
/// Uma obra sorteada inteira: a rede programada no calendário e o orçamento
/// com cada item vinculado a uma atividade — marcos e atividades com folga
/// inclusive.
/// </summary>
/// <remarks>
/// Também compilado no projeto de testes da infraestrutura, que grava e lê
/// as mesmas obras sorteadas.
/// </remarks>
internal static class ObrasAleatorias
{
    /// <summary>Segunda a sexta, com os feriados nacionais do período.</summary>
    public static readonly CalendarioDeTrabalho Calendario = CalendarioDeTrabalho.DeSegundaASexta(
    [
        new(2026, 10, 12), new(2026, 11, 2), new(2026, 11, 20), new(2026, 12, 25),
        new(2027, 1, 1), new(2027, 3, 26), new(2027, 4, 21), new(2027, 9, 7),
    ]);

    /// <remarks>Cada semente começa a obra num dia diferente, fim de semana e feriado inclusive.</remarks>
    public static (CronogramaProgramado Obra, OrcamentoCalculado Planilha) Gerar(int semente)
    {
        var (cronograma, baseDeCustos, orcamento) = Agregados(semente);
        var obra = ProgramacaoDoCronograma.Programar(cronograma, new DateOnly(2026, 10, 1).AddDays(semente % 365), Calendario);

        return (obra, CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos));
    }

    /// <summary>Os agregados por trás de <see cref="Gerar"/>: a rede, a base e o orçamento já vinculado a ela.</summary>
    public static (Cronograma Cronograma, BaseDeCustos BaseDeCustos, Orcamento Orcamento) Agregados(int semente)
    {
        var cronograma = RedesAleatorias.Gerar(semente);
        var (baseDeCustos, orcamento) = OrcamentosAleatorios.Gerar(semente);
        var codigos = cronograma.Atividades.Select(atividade => atividade.Codigo.Valor).ToList();
        var sorteio = new Random(semente);

        foreach (var item in CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos).Itens)
        {
#pragma warning disable CA5394 // Aleatório não criptográfico de propósito: o que se quer é reprodutibilidade.
            orcamento.VincularAtividade(item.Numero.Valor, codigos[sorteio.Next(codigos.Count)]);
#pragma warning restore CA5394
        }

        return (cronograma, baseDeCustos, orcamento);
    }
}
