using PlanejamentoObras.Dominio.Medicoes;

namespace PlanejamentoObras.Dominio.Testes.ValorAgregado;

/// <summary>
/// Medições sorteadas para uma linha de base, para os testes por propriedade.
/// </summary>
internal static class MedicoesAleatorias
{
    /// <summary>
    /// De um mês antes do início planejado a dois depois do fim, pulando um
    /// mês de vez em quando. Cada item anda, para ou volta (glosa) — sem
    /// nunca sair de zero a contratado —, com custos quaisquer. Em metade das
    /// sementes, a última medição termina a obra.
    /// </summary>
    /// <returns>Se a última medição terminou todos os itens.</returns>
    public static bool Registrar(Acompanhamento acompanhamento, int semente)
    {
        var sorteio = new Random(semente);
        var linhaDeBase = acompanhamento.LinhaDeBase;
        var acumuladas = linhaDeBase.Itens.ToDictionary(item => item.Numero, _ => 0m);
        var terminar = Sortear(sorteio, 0, 2) == 0;

        var meses = new List<DateOnly>();

        for (var mes = linhaDeBase.Meses[0].AddMonths(-1); mes <= linhaDeBase.Meses[^1].AddMonths(2); mes = mes.AddMonths(1))
        {
            if (Sortear(sorteio, 0, 5) > 0)
            {
                meses.Add(mes);
            }
        }

        for (var i = 0; i < meses.Count; i++)
        {
            var ultima = i == meses.Count - 1;
            var lancamentos = new List<LancamentoDeMedicao>();

            foreach (var item in linhaDeBase.Itens)
            {
                var acumulada = acumuladas[item.Numero];
                decimal quantidade;

                if (ultima && terminar)
                {
                    quantidade = item.Quantidade - acumulada;
                }
                else if (Sortear(sorteio, 0, 2) == 0)
                {
                    continue;
                }
                else if (acumulada > 0 && Sortear(sorteio, 0, 7) == 0)
                {
                    quantidade = -Sortear(sorteio, 1, (int)(acumulada * 100) + 1) / 100m;
                }
                else
                {
                    quantidade = Sortear(sorteio, 0, (int)((item.Quantidade - acumulada) * 100) + 1) / 100m;
                }

                acumuladas[item.Numero] = acumulada + quantidade;
                lancamentos.Add(new LancamentoDeMedicao(item.Numero.Valor, quantidade, Sortear(sorteio, 0, 100_000) / 100m));
            }

            acompanhamento.RegistrarMedicao(meses[i], lancamentos);
        }

        return terminar && meses.Count > 0;
    }

#pragma warning disable CA5394 // Aleatório não criptográfico de propósito: o que se quer é reprodutibilidade.
    private static int Sortear(Random sorteio, int de, int ateAntesDe) => sorteio.Next(de, ateAntesDe);
#pragma warning restore CA5394
}
