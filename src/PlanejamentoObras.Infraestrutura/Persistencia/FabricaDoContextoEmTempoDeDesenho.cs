using Microsoft.EntityFrameworkCore.Design;

namespace PlanejamentoObras.Infraestrutura.Persistencia;

/// <summary>
/// O contexto que o dotnet-ef usa para gerar migrações — e que o pacote de
/// migrações usa para aplicá-las. Gerar migração não abre conexão: a cadeia de
/// conexão aqui só diz que o banco é PostgreSQL. Aplicar abre, e a cadeia vem
/// de <c>--connection</c> ou, no contêiner do migrador, da mesma variável de
/// ambiente que a API e o processador leem. E não lê nem grava agregado, por
/// isso nenhuma empresa.
/// </summary>
public sealed class FabricaDoContextoEmTempoDeDesenho : IDesignTimeDbContextFactory<ContextoDoBanco>
{
    public ContextoDoBanco CreateDbContext(string[] args) =>
        ContextoDoBanco.Criar(
            Environment.GetEnvironmentVariable("ConnectionStrings__Planejamento") ?? "Host=localhost;Database=planejamento_obras",
            EmpresaFixa.Nenhuma);
}
