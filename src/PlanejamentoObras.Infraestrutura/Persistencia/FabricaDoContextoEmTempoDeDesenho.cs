using Microsoft.EntityFrameworkCore.Design;

namespace PlanejamentoObras.Infraestrutura.Persistencia;

/// <summary>
/// O contexto que o dotnet-ef usa para gerar migrações. Gerar migração não
/// abre conexão: a cadeia de conexão aqui só diz que o banco é PostgreSQL. E
/// não lê nem grava agregado, por isso nenhuma empresa.
/// </summary>
public sealed class FabricaDoContextoEmTempoDeDesenho : IDesignTimeDbContextFactory<ContextoDoBanco>
{
    public ContextoDoBanco CreateDbContext(string[] args) =>
        ContextoDoBanco.Criar("Host=localhost;Database=planejamento_obras", EmpresaFixa.Nenhuma);
}
