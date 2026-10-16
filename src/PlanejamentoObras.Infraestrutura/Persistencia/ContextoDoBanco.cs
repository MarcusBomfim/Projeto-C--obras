using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanejamentoObras.Infraestrutura.Persistencia.CaixaDeSaida;
using PlanejamentoObras.Infraestrutura.Persistencia.Idempotencia;
using PlanejamentoObras.Infraestrutura.Persistencia.Painel;
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

    internal DbSet<RegistroDeMensagem> CaixaDeSaida => Set<RegistroDeMensagem>();

    internal DbSet<RegistroDoPainel> Paineis => Set<RegistroDoPainel>();

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

        // A chave que a referência do painel usa, como a da linha de base.
        acompanhamentos
            .HasAlternateKey(registro => new { registro.EmpresaId, registro.Id })
            .HasName("ak_acompanhamentos_empresa_id_id");

        MapearChavesDeIdempotencia(modelBuilder);
        MapearCaixaDeSaida(modelBuilder);
        MapearPainel(modelBuilder);
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

    /// <summary>A caixa de saída: os eventos publicados, esperando tratamento (ADR-0017).</summary>
    private void MapearCaixaDeSaida(ModelBuilder modelBuilder)
    {
        var mensagens = modelBuilder.Entity<RegistroDeMensagem>();

        mensagens.ToTable("caixa_de_saida", tabelaBuilder =>
        {
            tabelaBuilder.HasCheckConstraint("ck_caixa_de_saida_formato", "formato >= 1");
            tabelaBuilder.HasCheckConstraint("ck_caixa_de_saida_tentativas", "tentativas >= 0");
            tabelaBuilder.HasCheckConstraint("ck_caixa_de_saida_conteudo", "jsonb_typeof(conteudo) = 'object'");
        });

        mensagens.HasKey(registro => registro.Id).HasName("pk_caixa_de_saida");
        mensagens.Property(registro => registro.Id).HasColumnName("id").ValueGeneratedNever();
        mensagens.Property(registro => registro.Sequencia).HasColumnName("sequencia").UseIdentityAlwaysColumn();
        mensagens.Property(registro => registro.EmpresaId).HasColumnName("empresa_id");
        mensagens.Property(registro => registro.Tipo).HasColumnName("tipo").HasMaxLength(100);
        mensagens.Property(registro => registro.Formato).HasColumnName("formato");
        mensagens.Property(registro => registro.Conteudo).HasColumnName("conteudo").HasColumnType("jsonb");
        mensagens.Property(registro => registro.AgregadoId).HasColumnName("agregado_id");
        mensagens.Property(registro => registro.VersaoDoAgregado).HasColumnName("versao_do_agregado");
        mensagens.Property(registro => registro.OcorridaEm).HasColumnName("ocorrida_em");
        mensagens.Property(registro => registro.Tentativas).HasColumnName("tentativas");
        mensagens.Property(registro => registro.ProximaTentativaEm).HasColumnName("proxima_tentativa_em");
        mensagens.Property(registro => registro.TratadaEm).HasColumnName("tratada_em");
        mensagens.Property(registro => registro.AbandonadaEm).HasColumnName("abandonada_em");
        mensagens.Property(registro => registro.UltimoErro).HasColumnName("ultimo_erro");
        mensagens.Property(registro => registro.ContextoDoRastreio).HasColumnName("contexto_do_rastreio").HasMaxLength(256);

        // A fila só olha o que está pendente: o índice só tem isso.
        mensagens
            .HasIndex(registro => new { registro.ProximaTentativaEm, registro.Sequencia })
            .HasDatabaseName("ix_caixa_de_saida_pendentes")
            .HasFilter("tratada_em is null and abandonada_em is null");
        mensagens
            .HasIndex(registro => registro.TratadaEm)
            .HasDatabaseName("ix_caixa_de_saida_tratada_em")
            .HasFilter("tratada_em is not null");

        mensagens.HasQueryFilter(FiltroDaEmpresa, registro => registro.EmpresaId == EmpresaId);
    }

    /// <summary>O painel das obras: uma linha por acompanhamento, refeita a cada fato dele (ADR-0017).</summary>
    private void MapearPainel(ModelBuilder modelBuilder)
    {
        var painel = modelBuilder.Entity<RegistroDoPainel>();

        painel.ToTable("painel_das_obras", tabelaBuilder =>
            tabelaBuilder.HasCheckConstraint(
                "ck_painel_das_obras_situacao",
                "situacao in ('SemMedicao', 'EmDia', 'Atencao', 'Critica')"));

        painel.HasKey(registro => new { registro.EmpresaId, registro.AcompanhamentoId }).HasName("pk_painel_das_obras");
        painel.Property(registro => registro.AcompanhamentoId).HasColumnName("acompanhamento_id").ValueGeneratedNever();
        painel.Property(registro => registro.EmpresaId).HasColumnName("empresa_id");
        painel.Property(registro => registro.LinhaDeBaseId).HasColumnName("linha_de_base_id");
        painel.Property(registro => registro.Obra).HasColumnName("obra").HasMaxLength(160);
        painel.Property(registro => registro.Situacao).HasColumnName("situacao").HasConversion<string>().HasMaxLength(20);
        painel.Property(registro => registro.Medicao).HasColumnName("medicao");
        painel.Property(registro => registro.Mes).HasColumnName("mes");
        painel.Property(registro => registro.OrcamentoNoTermino).HasColumnName("orcamento_no_termino");
        painel.Property(registro => registro.ValorPlanejado).HasColumnName("valor_planejado");
        painel.Property(registro => registro.ValorAgregado).HasColumnName("valor_agregado");
        painel.Property(registro => registro.CustoReal).HasColumnName("custo_real");
        painel.Property(registro => registro.IndiceDeDesempenhoDePrazo).HasColumnName("indice_de_desempenho_de_prazo");
        painel.Property(registro => registro.IndiceDeDesempenhoDeCusto).HasColumnName("indice_de_desempenho_de_custo");
        painel.Property(registro => registro.EstimativaNoTermino).HasColumnName("estimativa_no_termino");
        painel.Property(registro => registro.VariacaoNoTermino).HasColumnName("variacao_no_termino");
        painel.Property(registro => registro.DuracaoPlanejadaEmMeses).HasColumnName("duracao_planejada_em_meses");
        painel.Property(registro => registro.PrazoAgregadoEmMeses).HasColumnName("prazo_agregado_em_meses");
        painel.Property(registro => registro.DuracaoEstimadaEmMeses).HasColumnName("duracao_estimada_em_meses");
        painel.Property(registro => registro.VersaoDoAcompanhamento).HasColumnName("versao_do_acompanhamento");
        painel.Property(registro => registro.AtualizadoEm).HasColumnName("atualizado_em");

        painel.HasIndex(registro => new { registro.EmpresaId, registro.Obra }).HasDatabaseName("ix_painel_das_obras_empresa_id_obra");

        // Saiu o acompanhamento, sai a linha dele: o painel não mostra obra que não existe mais.
        painel
            .HasOne<RegistroDeAcompanhamento>()
            .WithMany()
            .HasForeignKey(registro => new { registro.EmpresaId, registro.AcompanhamentoId })
            .HasPrincipalKey(acompanhamento => new { acompanhamento.EmpresaId, acompanhamento.Id })
            .HasConstraintName("fk_painel_das_obras_acompanhamentos")
            .OnDelete(DeleteBehavior.Cascade);

        painel.HasQueryFilter(FiltroDaEmpresa, registro => registro.EmpresaId == EmpresaId);
    }
}
