using PlanejamentoObras.Dominio.Comum;
using PlanejamentoObras.Dominio.LinhasDeBase;

namespace PlanejamentoObras.Dominio.Medicoes;

/// <summary>A obra passou a ser acompanhada contra uma linha de base.</summary>
public sealed record AcompanhamentoIniciado(AcompanhamentoId Acompanhamento, LinhaDeBaseId LinhaDeBase) : EventoDeDominio
{
    public override Identificador Agregado => Acompanhamento;
}

/// <summary>A medição de um mês foi registrada.</summary>
public sealed record MedicaoRegistrada(AcompanhamentoId Acompanhamento, int Numero, DateOnly Mes) : EventoDeDominio
{
    public override Identificador Agregado => Acompanhamento;
}

/// <summary>A última medição foi desfeita, para ser corrigida.</summary>
public sealed record MedicaoDesfeita(AcompanhamentoId Acompanhamento, int Numero, DateOnly Mes) : EventoDeDominio
{
    public override Identificador Agregado => Acompanhamento;
}
