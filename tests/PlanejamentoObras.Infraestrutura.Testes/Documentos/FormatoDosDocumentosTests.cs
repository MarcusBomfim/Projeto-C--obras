using System.Text.Json.Nodes;
using PlanejamentoObras.Dominio.Calendarios;
using PlanejamentoObras.Dominio.Cronogramas;
using PlanejamentoObras.Dominio.Custos;
using PlanejamentoObras.Dominio.LinhasDeBase;
using PlanejamentoObras.Dominio.Medicoes;
using PlanejamentoObras.Dominio.Orcamentos;
using PlanejamentoObras.Infraestrutura.Persistencia.Registros;

namespace PlanejamentoObras.Infraestrutura.Testes.Documentos;

/*
 * O formato dos documentos é um contrato com o que já está gravado. Renomear
 * uma propriedade C# — Dependencias para Vinculos, digamos — renomearia o
 * campo no JSON, e todo cronograma gravado deixaria de ser lido.
 *
 * Estes testes travam o formato: nomes, estrutura e valores. Se um deles
 * quebrar, a pergunta não é "como conserto o teste", e sim "o formato mudou
 * de propósito?". Se mudou, o número do formato sobe e entra uma migração
 * dos documentos gravados (ADR-0011).
 *
 * A comparação é de JSON, e não de texto: a ordem das chaves e o jeito de
 * escapar acentos não fazem parte do contrato — o jsonb reordena uma e
 * normaliza o outro.
 */
public sealed class FormatoDosDocumentosTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static void MesmoJson(string esperado, string gravado) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(esperado), JsonNode.Parse(gravado)), $"Formato mudou. Gravado: {gravado}");

    [Fact]
    public void Formato_do_cronograma()
    {
        var cronograma = new Cronograma("Casa");
        cronograma.AdicionarAtividade("FUN", "Fundações", 10);
        cronograma.AdicionarAtividade("EST", "Estrutura", 20);
        cronograma.AdicionarDependencia("FUN", "EST", TipoDeDependencia.TerminoInicio, 2);

        var registro = RegistroDeCronograma.De(cronograma, versao: 1, Agora);

        Assert.Equal(1, registro.Formato);
        MesmoJson(
            """
            {
              "nome": "Casa",
              "atividades": [
                { "codigo": "FUN", "descricao": "Fundações", "duracaoEmDiasUteis": 10 },
                { "codigo": "EST", "descricao": "Estrutura", "duracaoEmDiasUteis": 20 }
              ],
              "dependencias": [
                { "predecessora": "FUN", "sucessora": "EST", "tipo": "TerminoInicio", "defasagemEmDiasUteis": 2 }
              ]
            }
            """,
            registro.Documento);
    }

    [Fact]
    public void Formato_do_orcamento_com_etapas_itens_e_BDI()
    {
        var orcamento = new Orcamento("Obra", Bdi.Informado(25m));
        orcamento.AdicionarEtapa("Vedações");
        orcamento.AdicionarItem("1", "ALV", 120.5m);
        orcamento.VincularAtividade("1.1", "ALV");
        orcamento.DefinirBdiDiferenciado("1.1", Bdi.Informado(12m));

        var registro = RegistroDeOrcamento.De(orcamento, versao: 1, Agora);

        MesmoJson(
            """
            {
              "nome": "Obra",
              "bdi": { "percentual": 25, "parcelas": null },
              "etapas": [
                {
                  "titulo": "Vedações",
                  "filhos": [
                    {
                      "tipo": "item",
                      "codigo": "ALV",
                      "quantidade": 120.5,
                      "atividade": "ALV",
                      "bdiDiferenciado": { "percentual": 12, "parcelas": null }
                    }
                  ]
                }
              ]
            }
            """,
            registro.Documento);
    }

    [Fact]
    public void Formato_da_base_de_custos()
    {
        var baseDeCustos = new BaseDeCustos("Tabela própria", new DateOnly(2026, 9, 1));
        baseDeCustos.AdicionarInsumo("BLO", "Bloco cerâmico", "un", TipoDeInsumo.Material, 1.32m);
        baseDeCustos.AdicionarInsumo("PED", "Pedreiro", "h", TipoDeInsumo.MaoDeObra, 28.74m);
        baseDeCustos.AdicionarComposicao("ALV", "Alvenaria", "m²");
        baseDeCustos.AdicionarItem("ALV", "BLO", 13.5m);
        baseDeCustos.AdicionarItem("ALV", "PED", 0.8m);

        var registro = RegistroDeBaseDeCustos.De(baseDeCustos, versao: 1, Agora);

        Assert.Equal(1, registro.Formato);
        MesmoJson(
            """
            {
              "nome": "Tabela própria",
              "dataBase": "2026-09-01",
              "insumos": [
                { "codigo": "BLO", "descricao": "Bloco cerâmico", "unidade": "un", "tipo": "Material", "preco": 1.32 },
                { "codigo": "PED", "descricao": "Pedreiro", "unidade": "h", "tipo": "MaoDeObra", "preco": 28.74 }
              ],
              "composicoes": [
                {
                  "codigo": "ALV",
                  "descricao": "Alvenaria",
                  "unidade": "m²",
                  "itens": [
                    { "codigo": "BLO", "coeficiente": 13.5 },
                    { "codigo": "PED", "coeficiente": 0.8 }
                  ]
                }
              ]
            }
            """,
            registro.Documento);
    }

    [Fact]
    public void Formato_da_linha_de_base()
    {
        var registro = RegistroDeLinhaDeBase.De(UmaLinhaDeBase(), versao: 1, Agora);

        Assert.Equal(1, registro.Formato);
        MesmoJson(
            """
            {
              "nome": "Contrato",
              "aprovadaEm": "2026-10-23",
              "datas": "Cedo",
              "inicioPlanejado": "2026-10-26",
              "terminoPlanejado": "2026-11-06",
              "meses": ["2026-10-01", "2026-11-01"],
              "total": 6250.00,
              "custoTotal": 5000,
              "linhas": [
                {
                  "tipo": "etapa",
                  "numero": "1",
                  "descricao": "Estrutura",
                  "total": 6250.00,
                  "custoTotal": 5000,
                  "valoresPlanejados": [3125, 3125],
                  "custosPlanejados": [2500, 2500]
                },
                {
                  "tipo": "item",
                  "numero": "1.1",
                  "descricao": "Concreto usinado",
                  "total": 6250.00,
                  "custoTotal": 5000,
                  "valoresPlanejados": [3125, 3125],
                  "custosPlanejados": [2500, 2500],
                  "unidade": "m³",
                  "quantidade": 10,
                  "precoUnitario": 625.00,
                  "custoUnitario": 500,
                  "atividade": "EST"
                }
              ]
            }
            """,
            registro.Documento);
    }

    [Fact]
    public void Formato_do_acompanhamento()
    {
        var acompanhamento = new Acompanhamento(UmaLinhaDeBase());
        acompanhamento.RegistrarMedicao(new DateOnly(2026, 10, 30), new LancamentoDeMedicao("1.1", 4.5m, CustoReal: 2_300m));

        var registro = RegistroDeAcompanhamento.De(acompanhamento, versao: 1, Agora);

        // A linha de base fica fora do documento: é a coluna com chave estrangeira.
        Assert.Equal((1, acompanhamento.LinhaDeBase.Id.Valor), (registro.Formato, registro.LinhaDeBaseId));
        MesmoJson(
            """
            {
              "medicoes": [
                { "mes": "2026-10-01", "itens": [{ "item": "1.1", "quantidade": 4.5, "custoReal": 2300 }] }
              ]
            }
            """,
            registro.Documento);
    }

    [Fact]
    public void Le_o_tipo_em_qualquer_posicao_do_objeto()
    {
        /*
         * O jsonb não guarda a ordem das chaves: devolve as mais curtas
         * primeiro. Hoje "tipo" é a chave mais curta de todo nó polimórfico e
         * volta na frente — por coincidência. Uma propriedade nova de nome
         * curto, como "id" ou "mes", passaria na frente dele, e a quebra só
         * apareceria em documento que passou pelo banco. Aqui, "tipo" vem no
         * fim de cada objeto.
         *
         * As etapas de primeiro nível não têm "tipo" — só podem ser etapa —;
         * dentro delas, etapa e item se distinguem por ele.
         */
        var registro = new RegistroDeOrcamento
        {
            Id = Guid.CreateVersion7(),
            Nome = "Obra",
            Versao = 1,
            Formato = 1,
            Documento =
                """
                {"bdi":{"percentual":25},"nome":"Obra","etapas":[{"filhos":[{"filhos":[{"codigo":"ALV","quantidade":1,"tipo":"item"}],"titulo":"S","tipo":"etapa"}],"titulo":"E"}]}
                """,
        };

        var lido = registro.ParaOrcamento();

        Assert.Equal("S", lido.Etapa("1.1").Titulo);
        Assert.Equal("ALV", lido.Item("1.1.1").Codigo.Valor);
    }

    /// <summary>Um item de dez dias úteis que atravessa a virada de outubro para novembro.</summary>
    private static LinhaDeBase UmaLinhaDeBase()
    {
        var baseDeCustos = new BaseDeCustos("Tabela própria", new DateOnly(2026, 9, 1));
        baseDeCustos.AdicionarInsumo("CON", "Concreto usinado", "m³", TipoDeInsumo.Material, 500m);

        var orcamento = new Orcamento("Obra", Bdi.Informado(25m));
        orcamento.AdicionarEtapa("Estrutura");
        orcamento.AdicionarItem("1", "CON", 10m);
        orcamento.VincularAtividade("1.1", "EST");

        var cronograma = new Cronograma("Obra");
        cronograma.AdicionarAtividade("EST", "Estrutura", 10);
        var obra = ProgramacaoDoCronograma.Programar(cronograma, new DateOnly(2026, 10, 26), CalendarioDeTrabalho.DeSegundaASexta([]));

        return LinhaDeBase.Estabelecer("Contrato", new DateOnly(2026, 10, 23), CalculadoraDoOrcamento.Calcular(orcamento, baseDeCustos), obra);
    }
}
