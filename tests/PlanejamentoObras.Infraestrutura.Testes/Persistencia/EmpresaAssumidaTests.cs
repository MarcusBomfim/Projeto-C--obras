using PlanejamentoObras.Infraestrutura.Persistencia;

namespace PlanejamentoObras.Infraestrutura.Testes.Persistencia;

/*
 * A empresa de uma tarefa em segundo plano: começa nenhuma, assume a da
 * mensagem que pegou da fila, e não troca no meio do caminho (ADR-0017).
 */
public sealed class EmpresaAssumidaTests
{
    [Fact]
    public void Comeca_sem_empresa_e_assume_a_da_mensagem()
    {
        var empresa = Guid.CreateVersion7();
        var assumida = new EmpresaAssumida();
        Assert.Null(assumida.Id);

        assumida.Assumir(empresa);
        assumida.Assumir(empresa);

        Assert.Equal(empresa, assumida.Id);
    }

    [Fact]
    public void Nao_troca_de_empresa_no_meio_da_tarefa()
    {
        var primeira = Guid.CreateVersion7();
        var assumida = new EmpresaAssumida();
        assumida.Assumir(primeira);

        Assert.Throws<InvalidOperationException>(() => assumida.Assumir(Guid.CreateVersion7()));
        Assert.Equal(primeira, assumida.Id);
    }

    [Fact]
    public void O_guid_vazio_nao_e_empresa()
    {
        Assert.Throws<ArgumentException>(() => new EmpresaAssumida().Assumir(Guid.Empty));
    }
}
