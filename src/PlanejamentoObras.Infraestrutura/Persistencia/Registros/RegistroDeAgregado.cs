namespace PlanejamentoObras.Infraestrutura.Persistencia.Registros;

/// <summary>
/// A linha de uma tabela de agregados: o documento inteiro, e ao lado dele o
/// que o banco precisa enxergar sem abri-lo.
/// </summary>
/// <remarks>
/// Tipo de persistência, e não do domínio: tem setters, porque é o Entity
/// Framework que o preenche. O agregado só entra e sai daqui pelos
/// documentos (ADR-0011).
/// </remarks>
internal abstract class RegistroDeAgregado
{
    public Guid Id { get; set; }

    /// <summary>Começa em 1 e cresce a cada gravação: é o que a concorrência otimista confere.</summary>
    public int Versao { get; set; }

    /// <summary>A versão do formato do documento gravado.</summary>
    public short Formato { get; set; }

    /// <summary>O agregado inteiro, em JSON — coluna <c>jsonb</c>.</summary>
    public string Documento { get; set; } = string.Empty;

    public DateTimeOffset CriadoEm { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }

    /// <summary>
    /// Este código lê um formato; documento gravado em outro é recusado com a
    /// razão, em vez de lido pela metade.
    /// </summary>
    protected void ConferirFormato(short esperado, string agregado)
    {
        if (Formato != esperado)
        {
            throw new InvalidOperationException(
                $"{agregado} {Id} está gravado no formato {Formato}, e esta versão do sistema lê o formato {esperado}.");
        }
    }

    protected T Preencher<T>(Guid id, int versao, short formato, string documento, DateTimeOffset agora)
        where T : RegistroDeAgregado
    {
        Id = id;
        Versao = versao;
        Formato = formato;
        Documento = documento;
        CriadoEm = agora;
        AtualizadoEm = agora;

        return (T)this;
    }
}

/// <summary>Um registro de agregado que tem nome — para listar e procurar sem abrir o documento.</summary>
internal abstract class RegistroComNome : RegistroDeAgregado
{
    public string Nome { get; set; } = string.Empty;
}
