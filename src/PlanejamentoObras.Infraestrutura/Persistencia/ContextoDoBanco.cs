using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;

namespace PlanejamentoObras.Infraestrutura.Persistencia;

/// <summary>
/// O banco: uma tabela por tipo de agregado, cada linha um documento
/// <c>jsonb</c> com identidade, versão e o que se consulta sem abri-lo
/// (ADR-0011).
/// </summary>
/// <remarks>
/// <para>
/// O que o banco consegue garantir sem duplicar regra de negócio, ele
/// garante: identidade única, versão e formato válidos, documento que é um
/// objeto JSON, e a referência do acompanhamento à linha de base — que não
/// some enquanto alguém mede contra ela. As regras do negócio ficam no
/// domínio.
/// </para>
/// <para>
/// Nomes em snake_case, no esquema <c>planejamento</c>, como se escreve SQL
/// à mão — quem consulta o banco direto não precisa de aspas.
/// </para>
/// </remarks>
public sealed class ContextoDoBanco(DbContextOptions<ContextoDoBanco> opcoes) : DbContext(opcoes)
{
    public const string Esquema = "planejamento";

    internal DbSet<RegistroDeCronograma> Cronogramas => Set<RegistroDeCronograma>();

    internal DbSet<RegistroDeBaseDeCustos> BasesDeCustos => Set<RegistroDeBaseDeCustos>();

    internal DbSet<RegistroDeOrcamento> Orcamentos => Set<RegistroDeOrcamento>();

    internal DbSet<RegistroDeLinhaDeBase> LinhasDeBase => Set<RegistroDeLinhaDeBase>();

    internal DbSet<RegistroDeAcompanhamento> Acompanhamentos => Set<RegistroDeAcompanhamento>();

    /// <summary>
    /// O Npgsql configurado como as migrações esperam — inclusive o histórico
    /// delas dentro do esquema do projeto. Usado pela aplicação, pelo dotnet-ef
    /// e pelos testes, para os três verem o mesmo banco.
    /// </summary>
    public static void Configurar(DbContextOptionsBuilder opcoes, string conexao)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        opcoes.UseNpgsql(conexao, npgsql => npgsql.MigrationsHistoryTable("historico_de_migracoes", Esquema));
    }

    /// <summary>Um contexto avulso para a cadeia de conexão — para ferramentas e testes, fora da injeção de dependência.</summary>
    public static ContextoDoBanco Criar(string conexao)
    {
        var opcoes = new DbContextOptionsBuilder<ContextoDoBanco>();
        Configurar(opcoes, conexao);

        return new ContextoDoBanco(opcoes.Options);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);

        var cronogramas = Agregado<RegistroDeCronograma>(modelBuilder, "cronogramas");
        ComNome(cronogramas, "cronogramas");

        var bases = Agregado<RegistroDeBaseDeCustos>(modelBuilder, "bases_de_custos");
        ComNome(bases, "bases_de_custos");
        bases.Property(registro => registro.DataBase).HasColumnName("data_base");

        var orcamentos = Agregado<RegistroDeOrcamento>(modelBuilder, "orcamentos");
        ComNome(orcamentos, "orcamentos");

        var linhasDeBase = Agregado<RegistroDeLinhaDeBase>(modelBuilder, "linhas_de_base");
        ComNome(linhasDeBase, "linhas_de_base");
        linhasDeBase.Property(registro => registro.AprovadaEm).HasColumnName("aprovada_em");

        var acompanhamentos = Agregado<RegistroDeAcompanhamento>(modelBuilder, "acompanhamentos");
        acompanhamentos.Property(registro => registro.LinhaDeBaseId).HasColumnName("linha_de_base_id");
        acompanhamentos.HasIndex(registro => registro.LinhaDeBaseId).HasDatabaseName("ix_acompanhamentos_linha_de_base_id");
        acompanhamentos
            .HasOne<RegistroDeLinhaDeBase>()
            .WithMany()
            .HasForeignKey(registro => registro.LinhaDeBaseId)
            .HasConstraintName("fk_acompanhamentos_linhas_de_base")
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>As colunas que toda tabela de agregado tem.</summary>
    private static EntityTypeBuilder<T> Agregado<T>(ModelBuilder modelBuilder, string tabela)
        where T : RegistroDeAgregado
    {
        var entidade = modelBuilder.Entity<T>();

        entidade.ToTable(tabela, tabelaBuilder =>
        {
            tabelaBuilder.HasCheckConstraint($"ck_{tabela}_versao", "versao >= 1");
            tabelaBuilder.HasCheckConstraint($"ck_{tabela}_formato", "formato >= 1");
            tabelaBuilder.HasCheckConstraint($"ck_{tabela}_documento", "jsonb_typeof(documento) = 'object'");
        });

        entidade.HasKey(registro => registro.Id).HasName($"pk_{tabela}");
        entidade.Property(registro => registro.Id).HasColumnName("id").ValueGeneratedNever();
        entidade.Property(registro => registro.Versao).HasColumnName("versao").IsConcurrencyToken();
        entidade.Property(registro => registro.Formato).HasColumnName("formato");
        entidade.Property(registro => registro.Documento).HasColumnName("documento").HasColumnType("jsonb");
        entidade.Property(registro => registro.CriadoEm).HasColumnName("criado_em");
        entidade.Property(registro => registro.AtualizadoEm).HasColumnName("atualizado_em");

        return entidade;
    }

    /// <summary>A coluna <c>nome</c>, indexada — para listar e procurar sem abrir documento.</summary>
    private static void ComNome<T>(EntityTypeBuilder<T> entidade, string tabela)
        where T : RegistroComNome
    {
        entidade.Property(registro => registro.Nome).HasColumnName("nome").HasMaxLength(160);
        entidade.HasIndex(registro => registro.Nome).HasDatabaseName($"ix_{tabela}_nome");
    }
}
