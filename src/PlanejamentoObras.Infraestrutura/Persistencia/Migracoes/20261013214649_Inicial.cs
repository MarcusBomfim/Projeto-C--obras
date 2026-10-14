using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanejamentoObras.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "planejamento");

            migrationBuilder.CreateTable(
                name: "bases_de_custos",
                schema: "planejamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    data_base = table.Column<DateOnly>(type: "date", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    documento = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bases_de_custos", x => x.id);
                    table.CheckConstraint("ck_bases_de_custos_documento", "jsonb_typeof(documento) = 'object'");
                    table.CheckConstraint("ck_bases_de_custos_formato", "formato >= 1");
                    table.CheckConstraint("ck_bases_de_custos_versao", "versao >= 1");
                });

            migrationBuilder.CreateTable(
                name: "cronogramas",
                schema: "planejamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    documento = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cronogramas", x => x.id);
                    table.CheckConstraint("ck_cronogramas_documento", "jsonb_typeof(documento) = 'object'");
                    table.CheckConstraint("ck_cronogramas_formato", "formato >= 1");
                    table.CheckConstraint("ck_cronogramas_versao", "versao >= 1");
                });

            migrationBuilder.CreateTable(
                name: "linhas_de_base",
                schema: "planejamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    aprovada_em = table.Column<DateOnly>(type: "date", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    documento = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_linhas_de_base", x => x.id);
                    table.CheckConstraint("ck_linhas_de_base_documento", "jsonb_typeof(documento) = 'object'");
                    table.CheckConstraint("ck_linhas_de_base_formato", "formato >= 1");
                    table.CheckConstraint("ck_linhas_de_base_versao", "versao >= 1");
                });

            migrationBuilder.CreateTable(
                name: "orcamentos",
                schema: "planejamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    documento = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orcamentos", x => x.id);
                    table.CheckConstraint("ck_orcamentos_documento", "jsonb_typeof(documento) = 'object'");
                    table.CheckConstraint("ck_orcamentos_formato", "formato >= 1");
                    table.CheckConstraint("ck_orcamentos_versao", "versao >= 1");
                });

            migrationBuilder.CreateTable(
                name: "acompanhamentos",
                schema: "planejamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    linha_de_base_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    documento = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acompanhamentos", x => x.id);
                    table.CheckConstraint("ck_acompanhamentos_documento", "jsonb_typeof(documento) = 'object'");
                    table.CheckConstraint("ck_acompanhamentos_formato", "formato >= 1");
                    table.CheckConstraint("ck_acompanhamentos_versao", "versao >= 1");
                    table.ForeignKey(
                        name: "fk_acompanhamentos_linhas_de_base",
                        column: x => x.linha_de_base_id,
                        principalSchema: "planejamento",
                        principalTable: "linhas_de_base",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acompanhamentos_linha_de_base_id",
                schema: "planejamento",
                table: "acompanhamentos",
                column: "linha_de_base_id");

            migrationBuilder.CreateIndex(
                name: "ix_bases_de_custos_nome",
                schema: "planejamento",
                table: "bases_de_custos",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ix_cronogramas_nome",
                schema: "planejamento",
                table: "cronogramas",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ix_linhas_de_base_nome",
                schema: "planejamento",
                table: "linhas_de_base",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_nome",
                schema: "planejamento",
                table: "orcamentos",
                column: "nome");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acompanhamentos",
                schema: "planejamento");

            migrationBuilder.DropTable(
                name: "bases_de_custos",
                schema: "planejamento");

            migrationBuilder.DropTable(
                name: "cronogramas",
                schema: "planejamento");

            migrationBuilder.DropTable(
                name: "orcamentos",
                schema: "planejamento");

            migrationBuilder.DropTable(
                name: "linhas_de_base",
                schema: "planejamento");
        }
    }
}
