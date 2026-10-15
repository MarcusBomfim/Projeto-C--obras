using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlanejamentoObras.Infraestrutura.Persistencia.Migracoes
{
    /// <summary>
    /// A caixa de saída dos eventos e o painel das obras que ela alimenta, com
    /// as funções que atravessam empresas — cada uma para uma coisa só — e o
    /// aviso de mensagem nova (ADR-0017).
    /// </summary>
    public partial class CaixaDeSaidaEPainel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_acompanhamentos_empresa_id_id",
                schema: "planejamento",
                table: "acompanhamentos",
                columns: new[] { "empresa_id", "id" });

            migrationBuilder.CreateTable(
                name: "caixa_de_saida",
                schema: "planejamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequencia = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    conteudo = table.Column<string>(type: "jsonb", nullable: false),
                    agregado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_do_agregado = table.Column<int>(type: "integer", nullable: false),
                    ocorrida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    proxima_tentativa_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tratada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    abandonada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_erro = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caixa_de_saida", x => x.id);
                    table.CheckConstraint("ck_caixa_de_saida_conteudo", "jsonb_typeof(conteudo) = 'object'");
                    table.CheckConstraint("ck_caixa_de_saida_formato", "formato >= 1");
                    table.CheckConstraint("ck_caixa_de_saida_tentativas", "tentativas >= 0");
                });

            migrationBuilder.CreateTable(
                name: "painel_das_obras",
                schema: "planejamento",
                columns: table => new
                {
                    acompanhamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linha_de_base_id = table.Column<Guid>(type: "uuid", nullable: false),
                    obra = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    medicao = table.Column<int>(type: "integer", nullable: true),
                    mes = table.Column<DateOnly>(type: "date", nullable: true),
                    orcamento_no_termino = table.Column<decimal>(type: "numeric", nullable: false),
                    valor_planejado = table.Column<decimal>(type: "numeric", nullable: true),
                    valor_agregado = table.Column<decimal>(type: "numeric", nullable: true),
                    custo_real = table.Column<decimal>(type: "numeric", nullable: true),
                    indice_de_desempenho_de_prazo = table.Column<decimal>(type: "numeric", nullable: true),
                    indice_de_desempenho_de_custo = table.Column<decimal>(type: "numeric", nullable: true),
                    estimativa_no_termino = table.Column<decimal>(type: "numeric", nullable: true),
                    variacao_no_termino = table.Column<decimal>(type: "numeric", nullable: true),
                    duracao_planejada_em_meses = table.Column<int>(type: "integer", nullable: false),
                    prazo_agregado_em_meses = table.Column<decimal>(type: "numeric", nullable: true),
                    duracao_estimada_em_meses = table.Column<decimal>(type: "numeric", nullable: true),
                    versao_do_acompanhamento = table.Column<int>(type: "integer", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_painel_das_obras", x => new { x.empresa_id, x.acompanhamento_id });
                    table.CheckConstraint("ck_painel_das_obras_situacao", "situacao in ('SemMedicao', 'EmDia', 'Atencao', 'Critica')");
                    table.ForeignKey(
                        name: "fk_painel_das_obras_acompanhamentos",
                        columns: x => new { x.empresa_id, x.acompanhamento_id },
                        principalSchema: "planejamento",
                        principalTable: "acompanhamentos",
                        principalColumns: new[] { "empresa_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_caixa_de_saida_pendentes",
                schema: "planejamento",
                table: "caixa_de_saida",
                columns: new[] { "proxima_tentativa_em", "sequencia" },
                filter: "tratada_em is null and abandonada_em is null");

            migrationBuilder.CreateIndex(
                name: "ix_caixa_de_saida_tratada_em",
                schema: "planejamento",
                table: "caixa_de_saida",
                column: "tratada_em",
                filter: "tratada_em is not null");

            migrationBuilder.CreateIndex(
                name: "ix_painel_das_obras_empresa_id_obra",
                schema: "planejamento",
                table: "painel_das_obras",
                columns: new[] { "empresa_id", "obra" });

            // Segurança por linha nas duas tabelas novas, como nas outras (ADR-0013).
            foreach (var tabela in new[] { "caixa_de_saida", "painel_das_obras" })
            {
                migrationBuilder.Sql($"""
                    alter table planejamento.{tabela} enable row level security;
                    alter table planejamento.{tabela} force row level security;
                    create policy isolamento_por_empresa on planejamento.{tabela}
                        using (empresa_id = nullif(current_setting('planejamento.empresa_id', true), '')::uuid)
                        with check (empresa_id = nullif(current_setting('planejamento.empresa_id', true), '')::uuid);
                    """);
            }

            /*
             * As únicas portas que atravessam empresas, e cada uma faz uma coisa
             * só. Rodam com os direitos de quem as criou — o papel das
             * migrações, que passa por cima da segurança por linha —, e com o
             * search_path fixo, para ninguém pôr outra tabela no lugar.
             *
             * A reserva entrega só o id e a empresa da próxima mensagem, e a
             * trava até o fim da transação de quem chamou: o conteúdo, cada um
             * lê o seu, em nome da empresa dele. As limpezas só apagam o que é
             * anterior a uma data.
             *
             * E não são de todo mundo: o PostgreSQL dá EXECUTE a PUBLIC em
             * toda função nova, e aqui isso é revogado. Quem implanta concede
             * ao papel do processador — a API, que está na frente da
             * internet, não alcança nada que atravesse empresas.
             */
            migrationBuilder.Sql("""
                create function planejamento.reservar_mensagens(limite integer, agora timestamptz)
                returns table (id uuid, empresa_id uuid)
                language sql
                security definer
                set search_path = planejamento, pg_temp
                as $$
                    select m.id, m.empresa_id
                    from planejamento.caixa_de_saida m
                    where m.tratada_em is null and m.abandonada_em is null and m.proxima_tentativa_em <= agora
                    order by m.proxima_tentativa_em, m.sequencia
                    limit limite
                    for update skip locked
                $$;

                create function planejamento.apagar_mensagens_tratadas_antes_de(limite timestamptz)
                returns integer
                language sql
                security definer
                set search_path = planejamento, pg_temp
                as $$
                    with apagadas as (delete from planejamento.caixa_de_saida where tratada_em < limite returning 1)
                    select count(*)::integer from apagadas
                $$;

                create function planejamento.apagar_chaves_de_idempotencia_antes_de(limite timestamptz)
                returns integer
                language sql
                security definer
                set search_path = planejamento, pg_temp
                as $$
                    with apagadas as (delete from planejamento.chaves_de_idempotencia where criada_em < limite returning 1)
                    select count(*)::integer from apagadas
                $$;

                revoke execute on function planejamento.reservar_mensagens(integer, timestamptz) from public;
                revoke execute on function planejamento.apagar_mensagens_tratadas_antes_de(timestamptz) from public;
                revoke execute on function planejamento.apagar_chaves_de_idempotencia_antes_de(timestamptz) from public;
                """);

            // Mensagem nova acorda quem escuta, na hora em que a transação que
            // a gravou é confirmada — e não na próxima volta do processador.
            migrationBuilder.Sql("""
                create function planejamento.avisar_mensagem_nova()
                returns trigger
                language plpgsql
                as $$
                begin
                    perform pg_notify('planejamento_caixa_de_saida', '');
                    return null;
                end
                $$;

                create trigger avisar_mensagem_nova
                    after insert on planejamento.caixa_de_saida
                    for each statement
                    execute function planejamento.avisar_mensagem_nova();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                drop trigger avisar_mensagem_nova on planejamento.caixa_de_saida;
                drop function planejamento.avisar_mensagem_nova();
                drop function planejamento.apagar_chaves_de_idempotencia_antes_de(timestamptz);
                drop function planejamento.apagar_mensagens_tratadas_antes_de(timestamptz);
                drop function planejamento.reservar_mensagens(integer, timestamptz);
                """);

            migrationBuilder.DropTable(
                name: "caixa_de_saida",
                schema: "planejamento");

            migrationBuilder.DropTable(
                name: "painel_das_obras",
                schema: "planejamento");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_acompanhamentos_empresa_id_id",
                schema: "planejamento",
                table: "acompanhamentos");
        }
    }
}
