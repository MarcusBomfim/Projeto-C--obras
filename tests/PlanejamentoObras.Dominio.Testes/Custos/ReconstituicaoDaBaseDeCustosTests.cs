using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Custos;

namespace PlanejamentoObras.Dominio.Testes.Custos;

public sealed class ReconstituicaoDaBaseDeCustosTests
{
    private static BaseDeCustos Copia(BaseDeCustos original) =>
        BaseDeCustos.Reconstituir(
            original.Id,
            original.Nome,
            original.DataBase,
            original.Insumos.Select(insumo => (insumo.Codigo.Valor, insumo.Descricao, insumo.Unidade.Valor, insumo.Tipo, insumo.Preco.Valor)),
            original.Composicoes.Select(composicao => (
                composicao.Codigo.Valor,
                composicao.Descricao,
                composicao.Unidade.Valor,
                composicao.Itens.Select(item => (item.Codigo.Valor, item.Coeficiente)))));

    private static (string, string, string, IEnumerable<(string, decimal)>) Composicao(string codigo, params (string, decimal)[] itens) =>
        (codigo, $"Composição {codigo}", "m²", itens);

    [Fact]
    public void Devolve_a_mesma_base_na_mesma_ordem_e_com_os_mesmos_custos()
    {
        var original = BaseDeExemplo.Criar();
        var copia = Copia(original);

        Assert.Equal(original.Id, copia.Id);
        Assert.Equal((original.Nome, original.DataBase), (copia.Nome, copia.DataBase));
        Assert.Equal(original.Insumos.Select(insumo => insumo.Codigo), copia.Insumos.Select(insumo => insumo.Codigo));
        Assert.Equal(original.Composicao("ALV").Itens, copia.Composicao("ALV").Itens);
        Assert.Equal(Dinheiro.Reais(57.14m), CalculadoraDeCustos.Calcular(copia, "ALV").CustoUnitario);
    }

    [Fact]
    public void Composicao_pode_usar_outra_gravada_depois_dela()
    {
        // Na base viva, PAR passou a usar ALV depois de ALV existir — mas PAR
        // foi cadastrada antes. Gravadas na ordem de cadastro, PAR vem
        // primeiro e usa uma composição que ainda "não existe".
        var copia = BaseDeCustos.Reconstituir(
            BaseDeCustosId.Novo(),
            "Base",
            new DateOnly(2026, 9, 1),
            [("SER", "Servente", "h", TipoDeInsumo.MaoDeObra, 21.46m)],
            [Composicao("PAR", ("ALV", 1m)), Composicao("ALV", ("SER", 0.5m))]);

        Assert.Equal(Dinheiro.Reais(10.73m), CalculadoraDeCustos.Calcular(copia, "PAR").CustoUnitario);
    }

    [Fact]
    public void Recusa_composicoes_que_se_usam_em_laco()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => BaseDeCustos.Reconstituir(
            BaseDeCustosId.Novo(),
            "Corrompida",
            new DateOnly(2026, 9, 1),
            [("SER", "Servente", "h", TipoDeInsumo.MaoDeObra, 21.46m)],
            [Composicao("A", ("B", 1m)), Composicao("B", ("C", 1m)), Composicao("C", ("B", 1m), ("D", 1m)), Composicao("D", ("SER", 1m))]));

        // B e C estão no laço; D está abaixo dele. A, acima, não é citada.
        Assert.Equal(
            "A base Corrompida tem composições que usam a si mesmas: B, C, D estão num laço ou abaixo dele.",
            erro.Message);
    }

    [Fact]
    public void Recusa_item_que_nao_esta_na_base()
    {
        Assert.Throws<ExcecaoDeDominio>(() => BaseDeCustos.Reconstituir(
            BaseDeCustosId.Novo(),
            "Base",
            new DateOnly(2026, 9, 1),
            [],
            [Composicao("A", ("FALTA", 1m))]));
    }
}
