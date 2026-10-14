using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PlanejamentoObras.Infraestrutura.Persistencia;

/// <summary>
/// Diz ao PostgreSQL, a cada conexão aberta, em nome de que empresa ela
/// trabalha. É o parâmetro que as políticas de segurança por linha leem
/// (ADR-0013).
/// </summary>
/// <remarks>
/// Toda abertura define o parâmetro — inclusive para nenhuma empresa, com o
/// valor vazio. Uma conexão que volta ao pool nunca leva a empresa de um
/// pedido para o pedido seguinte.
/// </remarks>
internal sealed class EmpresaNaConexao : DbConnectionInterceptor
{
    public static EmpresaNaConexao Instancia { get; } = new();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var comando = DefinirEmpresa(connection, eventData);
        comando.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var comando = DefinirEmpresa(connection, eventData);
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand DefinirEmpresa(DbConnection conexao, ConnectionEndEventData dados)
    {
        var empresa = (dados.Context as ContextoDoBanco)?.EmpresaId;

        var comando = conexao.CreateCommand();
        comando.CommandText = "select set_config('planejamento.empresa_id', @empresa, false)";

        var parametro = comando.CreateParameter();
        parametro.ParameterName = "empresa";
        parametro.Value = empresa?.ToString() ?? string.Empty;
        comando.Parameters.Add(parametro);

        return comando;
    }
}
