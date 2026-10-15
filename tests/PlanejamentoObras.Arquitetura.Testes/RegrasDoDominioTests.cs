using System.Reflection;
using System.Runtime.CompilerServices;
using PlanejamentoObras.Dominio;
using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Orcamentos;

namespace PlanejamentoObras.Arquitetura.Testes;

/*
 * Regras de arquitetura verificadas por teste, não por convenção.
 *
 * Diagrama de camadas e revisão de código combinam o que deveria acontecer;
 * estes testes garantem que acontece. Quem acrescentar uma referência ao
 * Entity Framework no domínio "só para resolver rápido" descobre no build,
 * não seis meses depois.
 */
public sealed class RegrasDoDominioTests
{
    private static readonly Assembly Dominio = typeof(ReferenciaDoAssembly).Assembly;

    [Fact]
    public void O_dominio_depende_apenas_da_biblioteca_padrao()
    {
        /*
         * Nem pacote de terceiro, nem projeto da própria solução. É o que
         * mantém as regras de cronograma testáveis sem banco, sem servidor
         * e sem rede — e o que permite trocar qualquer coisa de fora sem
         * tocar nelas.
         */
        var externas = Dominio
            .GetReferencedAssemblies()
            .Select(referencia => referencia.Name ?? string.Empty)
            .Where(nome => !EhDaBibliotecaPadrao(nome))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(externas);
    }

    [Fact]
    public void As_classes_do_dominio_sao_seladas()
    {
        /*
         * Herança é um ponto de extensão, e ponto de extensão precisa ser
         * projetado. Classe aberta por omissão convida a sobrescrever um
         * comportamento que a entidade assume ser fixo — e aí a invariante
         * que ela protege deixa de valer.
         *
         * Classe estática conta como selada: o compilador a emite como
         * `abstract sealed`.
         */
        var abertas = Dominio
            .GetExportedTypes()
            .Where(tipo => tipo.IsClass && !tipo.IsSealed && !tipo.IsAbstract)
            .Select(tipo => tipo.FullName)
            .ToArray();

        Assert.Empty(abertas);
    }

    [Fact]
    public void O_estado_do_dominio_nao_tem_setter_publico()
    {
        /*
         * O estado de uma entidade muda por método com nome de negócio —
         * AlterarDuracao, AdicionarDependencia —, que valida antes. Um setter
         * público deixaria qualquer camada escrever um valor que o método
         * recusaria.
         *
         * `init` é permitido: só vale durante a construção, que é onde a
         * validação acontece.
         */
        var comSetterPublico = Dominio
            .GetExportedTypes()
            .SelectMany(tipo => tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(propriedade => propriedade.SetMethod is { IsPublic: true } setter && !EhInit(setter))
            .Select(propriedade => $"{propriedade.DeclaringType?.Name}.{propriedade.Name}")
            .ToArray();

        Assert.Empty(comSetterPublico);
    }

    [Fact]
    public void So_a_infraestrutura_e_os_testes_enxergam_os_internos_do_dominio()
    {
        /*
         * Os internos do domínio incluem a reconstituição dos agregados — o
         * caminho pelo qual a persistência refaz um cronograma gravado. Se a
         * API web pudesse usá-lo, teria um jeito de criar agregados por fora
         * da porta da frente. A lista é fechada, e a mudança dela passa por
         * este teste.
         */
        var visiveisPara = Dominio
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(atributo => atributo.AssemblyName)
            .Order(StringComparer.Ordinal);

        Assert.Equal(["PlanejamentoObras.Dominio.Testes", "PlanejamentoObras.Infraestrutura"], visiveisPara);
    }

    [Fact]
    public void Todo_agregado_tem_identidade_propria()
    {
        /*
         * Raiz de agregado é o que a persistência grava e a API endereça: tem
         * um Id do seu próprio tipo de identificador, e não um Guid solto.
         */
        Type[] raizes =
        [
            typeof(Cronograma),
            typeof(BaseDeCustos),
            typeof(Orcamento),
            typeof(LinhaDeBase),
            typeof(Acompanhamento),
        ];

        var semIdentidade = raizes
            .Where(raiz => raiz.GetProperty("Id")?.PropertyType is not { } tipo
                || !tipo.IsSubclassOf(typeof(Identificador))
                || !tipo.Name.StartsWith(raiz.Name, StringComparison.Ordinal))
            .Select(raiz => raiz.Name);

        Assert.Empty(semIdentidade);
    }

    [Fact]
    public void Todo_evento_de_dominio_e_um_record_selado()
    {
        /*
         * Evento é fato consumado: imutável, comparado pelo valor, e sem
         * subclasse que o reinterprete depois.
         */
        var eventos = Dominio.GetTypes()
            .Where(tipo => tipo.IsSubclassOf(typeof(EventoDeDominio)) && !tipo.IsAbstract)
            .ToList();

        Assert.NotEmpty(eventos);
        Assert.All(eventos, evento => Assert.True(
            evento.IsSealed && evento.GetMethod("<Clone>$") is not null,
            $"{evento.Name} deveria ser um record selado."));
    }

    private static bool EhDaBibliotecaPadrao(string nome) =>
        nome is "System" or "netstandard" or "mscorlib"
        || nome.StartsWith("System.", StringComparison.Ordinal);

    private static bool EhInit(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));
}
