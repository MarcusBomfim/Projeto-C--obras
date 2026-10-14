using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanejamentoObras.Infraestrutura.Persistencia.Migracoes
{
    /// <summary>
    /// Cada linha passa a ter dona: a empresa. O banco filtra por ela também,
    /// com segurança por linha (ADR-0013).
    /// </summary>
    /// <remarks>
    /// A coluna <c>empresa_id</c> entra obrigatória e sem valor padrão, de
    /// propósito: num banco que já tenha agregados, esta migração falha em vez
    /// de dar todos a uma empresa inventada. Quem tiver dados preenche a
    /// coluna antes, sabendo de quem é cada obra.
    /// </remarks>
    public partial class IsolamentoPorEmpresa : Migration
    {
        /// <summary>As tabelas com dados de empresa — todas as do esquema, menos o histórico de migrações.</summary>
        private static readonly string[] TabelasDeEmpresa =
        [
            "cronogramas", "bases_de_custos", "orcamentos", "linhas_de_base", "acompanhamentos", "chaves_de_idempotencia",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_acompanhamentos_linhas_de_base",
                schema: "planejamento",
                table: "acompanhamentos");

            migrationBuilder.DropIndex(
                name: "ix_orcamentos_nome",
                schema: "planejamento",
                table: "orcamentos");

            migrationBuilder.DropIndex(
                name: "ix_linhas_de_base_nome",
                schema: "planejamento",
                table: "linhas_de_base");

            migrationBuilder.DropIndex(
                name: "ix_cronogramas_nome",
                schema: "planejamento",
                table: "cronogramas");

            migrationBuilder.DropIndex(
                name: "ix_bases_de_custos_nome",
                schema: "planejamento",
                table: "bases_de_custos");

            migrationBuilder.DropIndex(
                name: "ix_acompanhamentos_linha_de_base_id",
                schema: "planejamento",
                table: "acompanhamentos");

            migrationBuilder.AddColumn<Guid>(
                name: "empresa_id",
                schema: "planejamento",
                table: "orcamentos",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "empresa_id",
                schema: "planejamento",
                table: "linhas_de_base",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "empresa_id",
                schema: "planejamento",
                table: "cronogramas",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "empresa_id",
                schema: "planejamento",
                table: "bases_de_custos",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "empresa_id",
                schema: "planejamento",
                table: "acompanhamentos",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_linhas_de_base_empresa_id_id",
                schema: "planejamento",
                table: "linhas_de_base",
                columns: new[] { "empresa_id", "id" });

            migrationBuilder.CreateTable(
                name: "chaves_de_idempotencia",
                schema: "planejamento",
                columns: table => new
                {
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    impressao_do_pedido = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: true),
                    cabecalhos = table.Column<string>(type: "jsonb", nullable: true),
                    corpo = table.Column<string>(type: "text", nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chaves_de_idempotencia", x => new { x.empresa_id, x.chave });
                    table.CheckConstraint("ck_chaves_de_idempotencia_chave", "length(chave) between 1 and 255");
                });

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_empresa_id_nome",
                schema: "planejamento",
                table: "orcamentos",
                columns: new[] { "empresa_id", "nome" });

            migrationBuilder.CreateIndex(
                name: "ix_linhas_de_base_empresa_id_nome",
                schema: "planejamento",
                table: "linhas_de_base",
                columns: new[] { "empresa_id", "nome" });

            migrationBuilder.CreateIndex(
                name: "ix_cronogramas_empresa_id_nome",
                schema: "planejamento",
                table: "cronogramas",
                columns: new[] { "empresa_id", "nome" });

            migrationBuilder.CreateIndex(
                name: "ix_bases_de_custos_empresa_id_nome",
                schema: "planejamento",
                table: "bases_de_custos",
                columns: new[] { "empresa_id", "nome" });

            migrationBuilder.CreateIndex(
                name: "ix_acompanhamentos_empresa_id_linha_de_base_id",
                schema: "planejamento",
                table: "acompanhamentos",
                columns: new[] { "empresa_id", "linha_de_base_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_acompanhamentos_linhas_de_base",
                schema: "planejamento",
                table: "acompanhamentos",
                columns: new[] { "empresa_id", "linha_de_base_id" },
                principalSchema: "planejamento",
                principalTable: "linhas_de_base",
                principalColumns: new[] { "empresa_id", "id" },
                onDelete: ReferentialAction.Restrict);

            /*
             * Segurança por linha: cada conexão só vê e só grava as linhas da
             * empresa que ela declarou em planejamento.empresa_id. Sem empresa
             * declarada, não vê nada. FORCE vale a regra também para o dono
             * das tabelas; só superusuário e papel com BYPASSRLS passam por
             * cima — que é como as migrações rodam.
             */
            foreach (var tabela in TabelasDeEmpresa)
            {
                migrationBuilder.Sql($"""
                    alter table planejamento.{tabela} enable row level security;
                    alter table planejamento.{tabela} force row level security;
                    create policy isolamento_por_empresa on planejamento.{tabela}
                        using (empresa_id = nullif(current_setting('planejamento.empresa_id', true), '')::uuid)
                        with check (empresa_id = nullif(current_setting('planejamento.empresa_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tabela in TabelasDeEmpresa)
            {
                migrationBuilder.Sql($"""
                    drop policy isolamento_por_empresa on planejamento.{tabela};
                    alter table planejamento.{tabela} no force row level security;
                    alter table planejamento.{tabela} disable row level security;
                    """);
            }

            migrationBuilder.DropForeignKey(
                name: "fk_acompanhamentos_linhas_de_base",
                schema: "planejamento",
                table: "acompanhamentos");

            migrationBuilder.DropTable(
                name: "chaves_de_idempotencia",
                schema: "planejamento");

            migrationBuilder.DropIndex(
                name: "ix_orcamentos_empresa_id_nome",
                schema: "planejamento",
                table: "orcamentos");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_linhas_de_base_empresa_id_id",
                schema: "planejamento",
                table: "linhas_de_base");

            migrationBuilder.DropIndex(
                name: "ix_linhas_de_base_empresa_id_nome",
                schema: "planejamento",
                table: "linhas_de_base");

            migrationBuilder.DropIndex(
                name: "ix_cronogramas_empresa_id_nome",
                schema: "planejamento",
                table: "cronogramas");

            migrationBuilder.DropIndex(
                name: "ix_bases_de_custos_empresa_id_nome",
                schema: "planejamento",
                table: "bases_de_custos");

            migrationBuilder.DropIndex(
                name: "ix_acompanhamentos_empresa_id_linha_de_base_id",
                schema: "planejamento",
                table: "acompanhamentos");

            migrationBuilder.DropColumn(
                name: "empresa_id",
                schema: "planejamento",
                table: "orcamentos");

            migrationBuilder.DropColumn(
                name: "empresa_id",
                schema: "planejamento",
                table: "linhas_de_base");

            migrationBuilder.DropColumn(
                name: "empresa_id",
                schema: "planejamento",
                table: "cronogramas");

            migrationBuilder.DropColumn(
                name: "empresa_id",
                schema: "planejamento",
                table: "bases_de_custos");

            migrationBuilder.DropColumn(
                name: "empresa_id",
                schema: "planejamento",
                table: "acompanhamentos");

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_nome",
                schema: "planejamento",
                table: "orcamentos",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ix_linhas_de_base_nome",
                schema: "planejamento",
                table: "linhas_de_base",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ix_cronogramas_nome",
                schema: "planejamento",
                table: "cronogramas",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ix_bases_de_custos_nome",
                schema: "planejamento",
                table: "bases_de_custos",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ix_acompanhamentos_linha_de_base_id",
                schema: "planejamento",
                table: "acompanhamentos",
                column: "linha_de_base_id");

            migrationBuilder.AddForeignKey(
                name: "fk_acompanhamentos_linhas_de_base",
                schema: "planejamento",
                table: "acompanhamentos",
                column: "linha_de_base_id",
                principalSchema: "planejamento",
                principalTable: "linhas_de_base",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
