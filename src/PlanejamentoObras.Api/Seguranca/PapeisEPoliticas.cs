namespace PlanejamentoObras.Api.Seguranca;

/// <summary>
/// Os papéis de quem usa o sistema, como vêm no token — na reivindicação
/// <c>role</c>. Uma pessoa pode ter mais de um.
/// </summary>
public static class Papeis
{
    /// <summary>Monta e mantém o cronograma: atividades, durações, vínculos.</summary>
    public const string Planejador = "planejador";

    /// <summary>Mantém a base de custos e monta o orçamento.</summary>
    public const string Orcamentista = "orcamentista";

    /// <summary>Aprova a linha de base — o plano contra o qual a obra vai ser medida.</summary>
    public const string Gestor = "gestor";

    /// <summary>Mede a obra, mês a mês.</summary>
    public const string Engenheiro = "engenheiro";
}

/// <summary>
/// O que cada pedido exige de quem pede (ADR-0015). Toda política começa por
/// ser da empresa: autenticado, com uma empresa válida no token.
/// </summary>
public static class Politicas
{
    /// <summary>Ler qualquer coisa da empresa. Basta ser dela.</summary>
    public const string Consultar = "consultar";

    /// <summary>Alterar cronogramas: papel de planejador.</summary>
    public const string Planejar = "planejar";

    /// <summary>Alterar bases de custos e orçamentos: papel de orçamentista.</summary>
    public const string Orcar = "orcar";

    /// <summary>Aprovar e remover linhas de base: papel de gestor.</summary>
    public const string AprovarLinhaDeBase = "aprovar-linha-de-base";

    /// <summary>Acompanhar a obra e registrar medições: papel de engenheiro.</summary>
    public const string Medir = "medir";
}

/// <summary>Os nomes das reivindicações que a API lê do token, tal como vêm nele.</summary>
public static class Reivindicacoes
{
    /// <summary>O id da empresa de quem fez o pedido — um Guid (ADR-0013).</summary>
    public const string Empresa = "empresa";

    public const string Papel = "role";

    public const string Nome = "name";
}
