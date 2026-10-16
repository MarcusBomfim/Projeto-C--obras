using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanejamentoObras.Infraestrutura.Persistencia.Migracoes
{
    /// <summary>
    /// O rastro de quem publicou cada mensagem, no formato <c>traceparent</c>:
    /// o tratamento em segundo plano continua o rastro do pedido que gravou o
    /// fato (ADR-0019). Nulo nas mensagens que já estavam na caixa.
    /// </summary>
    public partial class ContextoDoRastreioNaCaixaDeSaida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contexto_do_rastreio",
                schema: "planejamento",
                table: "caixa_de_saida",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contexto_do_rastreio",
                schema: "planejamento",
                table: "caixa_de_saida");
        }
    }
}
