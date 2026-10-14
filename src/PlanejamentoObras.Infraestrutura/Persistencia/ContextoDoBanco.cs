using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanejamentoObras.Infraestrutura.Persistencia.Idempotencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;

namespace PlanejamentoObras.Infraestrutura.Persistencia;

/// <summary>
/// O banco: uma tabela por tipo de agregado, cada linha um documento
/// <c>jsonb</c> com identidade, versão e o que se consulta sem abri-lo
/// (ADR-0011) — e a empresa dona dele (ADR-0013).
/// </summary>
/// <remarks>
/// <para>
/// O que o banco consegue garantir sem duplicar regra de negócio, ele
/// garante: identidade única, versão e formato válidos, documento que é um
/// objeto JSON, e a referência do acompanhamento a uma linha de base da mesma
/// empresa — que não some enquanto alguém mede contra ela. As regras do
/// negócio ficam no domínio.
/// </para>
/// <para>
/// Toda consulta sai filtrada pela empresa do contexto, e o PostgreSQL
/// confere de novo, com segurança por linha: uma consulta que escape do
/// filtro — SQL escrito à mão, um <c>IgnoreQueryFilters</c> — ainda não vê o
/// que é de outra empresa.
/// </para>
/// <para>
/// Nomes em snake_case, no esquema <c>planejamento</c>, como se escreve SQL
/// à mão — quem consulta o banco direto não precisa de aspas.
/// </para>
/// </remarks>
public sealed class ContextoDoBanco(DbContextOptions<ContextoDoBanco> opcoes, IEmpresaAtual empresa) : DbContext(opcoes)
{
    public const string Esquema = "planejamento";

    private const string FiltroDaEmpresa = "empresa";

    internal DbSet<RegistroDeCronograma> Cronogramas => Set<RegistroDeCronograma>();

    internal DbSet<RegistroDeBaseDeCustos> BasesDeCustos => Set<RegistroDeBaseDeCustos>();

    internal DbSet<RegistroDeOrcamento> Orcamentos => Set<RegistroDeOrcamento>();

    internal DbSet<RegistroDeLinhaDeBase> LinhasDeBase => Set<RegistroDeLinhaDeBase>();

    internal DbSet<RegistroDeAcompanhamento> Acompanhamentos => Set<RegistroDeAcompanhamento>();

    internal DbSet<RegistroDeIdempotencia> ChavesDeIdempotencia => Set<RegistroDeIdempotencia>();

    /// <summary>A empresa em nome de quem este contexto lê e grava; <see langword="null"/> se nenhuma.</summary>
    internal Guid? EmpresaId => empresa.Id;

    /// <summary>A empresa de quem grava. Sem empresa, nada se grava.</summary>
    internal Guid EmpresaQueGrava =>
        EmpresaId ?? throw new InvalidOperationException(
            "Sem empresa, nada se grava: quem hospeda a persistência precisa dizer em nome de qual empresa ela trabalha.");

    /// <summary>
    /// O Npgsql configurado como as migrações esperam — inclusive o histórico
    /// delas dentro do esquema do projeto —, e a empresa passada ao banco a
    /// cada conexão aberta. Usado pela aplicação, pelo dotnet-ef e pelos
    /// testes, para os três verem o mesmo banco.
    /// </summary>
    public static void Configurar(DbContextOptionsBuilder opcoes, string conexao)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        opcoes
            .UseNpgsql(conexao, npgsql => npgsql.MigrationsHistoryTable("historico_de_migracoes", Esquema))
            .AddInterceptors(EmpresaNaConexao.Instancia);
    }

    /// <summary>Um contexto avulso para a cadeia de conexão — para ferramentas e testes, fora da injeção de dependência.</summary>
    public static ContextoDoBanco Criar(string conexao, IEmpresaAtual empresa)
    {
        var opcoes = new DbContextOptionsBuilder<ContextoDoBanco>();
        Configurar(opcoes, conexao);

        return new ContextoDoBanco(opcoes.Options, empresa);
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

        // A chave que a referência do acompanhamento usa: o id sozinho já é
        // único, mas é o par que amarra a referência à mesma empresa.
        linhasDeBase
            .HasAlternateKey(registro => new { registro.EmpresaId, registro.Id })
            .HasName("ak_linhas_de_base_empresa_id_id");

        var acompanhamentos = Agregado<RegistroDeAcompanhamento>(modelBuilder, "acompanhamentos");
        acompanhamentos.Property(registro => registro.LinhaDeBaseId).HasColumnName("linha_de_base_id");
        acompanhamentos
            .HasIndex(registro => new { registro.EmpresaId, registro.LinhaDeBaseId })
            .HasDatabaseName("ix_acompanhamentos_empresa_id_linha_de_base_id");
        acompanhamentos
            .HasOne<RegistroDeLinhaDeBase>()
            .WithMany()
            .HasForeignKey(registro => new { registro.EmpresaId, registro.LinhaDeBaseId })
            .HasPrincipalKey(linhaDeBase => new { linhaDeBase.EmpresaId, linhaDeBase.Id })
            .HasConstraintName("fk_acompanhamentos_linhas_de_base")
            .OnDelete(DeleteBehavior.Restrict);

        MapearChavesDeIdempotencia(modelBuilder);
    }

    /// <summary>As colunas que toda tabela de agregado tem, e o filtro pela empresa.</summary>
    private EntityTypeBuilder<T> Agregado<T>(ModelBuilder modelBuilder, string tabela)
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
        entidade.Property(registro => registro.EmpresaId).HasColumnName("empresa_id");
        entidade.Property(registro => registro.Versao).HasColumnName("versao").IsConcurrencyToken();
        entidade.Property(registro => registro.Formato).HasColumnName("formato");
        entidade.Property(registro => registro.Documento).HasColumnName("documento").HasColumnType("jsonb");
        entidade.Property(registro => registro.CriadoEm).HasColumnName("criado_em");
        entidade.Property(registro => registro.AtualizadoEm).HasColumnName("atualizado_em");

        entidade.HasQueryFilter(FiltroDaEmpresa, registro => registro.EmpresaId == EmpresaId);

        return entidade;
    }

    /// <summary>A coluna <c>nome</c>, indexada com a empresa — para listar e procurar sem abrir documento.</summary>
    private static void ComNome<T>(EntityTypeBuilder<T> entidade, string tabela)
        where T : RegistroComNome
    {
        entidade.Property(registro => registro.Nome).HasColumnName("nome").HasMaxLength(160);
        entidade.HasIndex(registro => new { registro.EmpresaId, registro.Nome }).HasDatabaseName($"ix_{tabela}_empresa_id_nome");
    }

    /// <summary>A chave de idempotência de cada pedido de criação, com a resposta dada a ele (ADR-0014).</summary>
    private void MapearChavesDeIdempotencia(ModelBuilder modelBuilder)
    {
        var chaves = modelBuilder.Entity<RegistroDeIdempotencia>();

        chaves.ToTable("chaves_de_idempotencia", tabelaBuilder =>
            tabelaBuilder.HasCheckConstraint("ck_chaves_de_idempotencia_chave", "length(chave) between 1 and 255"));

        chaves.HasKey(registro => new { registro.EmpresaId, registro.Chave }).HasName("pk_chaves_de_idempotencia");
        chaves.Property(registro => registro.EmpresaId).HasColumnName("empresa_id");
        chaves.Property(registro => registro.Chave).HasColumnName("chave").HasMaxLength(Idempotencia.ChavesDeIdempotencia.TamanhoMaximoDaChave);
        chaves.Property(registro => registro.ImpressaoDoPedido).HasColumnName("impressao_do_pedido");
        chaves.Property(registro => registro.Status).HasColumnName("status");
        chaves.Property(registro => registro.Cabecalhos).HasColumnName("cabecalhos").HasColumnType("jsonb");
        chaves.Property(registro => registro.Corpo).HasColumnName("corpo");
        chaves.Property(registro => registro.CriadaEm).HasColumnName("criada_em");

        chaves.HasQueryFilter(FiltroDaEmpresa, registro => registro.EmpresaId == EmpresaId);
    }
}
