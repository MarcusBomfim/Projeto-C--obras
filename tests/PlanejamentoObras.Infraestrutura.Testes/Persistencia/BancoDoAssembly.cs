using PlanejamentoObras.Infraestrutura.Testes.Persistencia;

// Um banco para o assembly de testes inteiro: sobe uma vez, antes do
// primeiro teste. Fica fora de BancoDeTestes porque os testes da API usam a
// mesma classe, mas dentro de um fixture deles.
[assembly: AssemblyFixture(typeof(BancoDeTestes))]
