import { describe, expect, test } from 'vitest';
import type { Esquemas } from '../api/cliente';
import { cronogramaDoReadme, FeriadosDoReadme, programacaoDoReadme } from '../testes/exemplosDoCronograma';
import { montarGantt, numeroDoDia, type Escala } from './geometriaDoGantt';

// Números redondos para as contas de cabeça: cada dia vale 10.
const Tamanho: Escala = { larguraDoDia: 10, alturaDaLinha: 20, esquerda: 100, topo: 40, direita: 0 };

const calendario = { feriados: FeriadosDoReadme, sabadoUtil: false };

/** A distância de 05/10/2026, o primeiro dia da obra do README, até a data. */
const diasDesde = (data: string) => numeroDoDia(data) - numeroDoDia('2026-10-05');

function gantt(hoje?: string) {
  return montarGantt(programacaoDoReadme, cronogramaDoReadme.dependencias, calendario, Tamanho, hoje);
}

function linha(codigo: string) {
  const encontrada = gantt().linhas.find((candidata) => candidata.codigo === codigo);

  if (!encontrada) {
    throw new Error(`Sem a atividade ${codigo}.`);
  }

  return encontrada;
}

describe('a geometria do Gantt, com a obra do README', () => {
  test('um dia por coluna, de 05/10/2026 a 07/01/2027, e os meses do tamanho dos seus dias', () => {
    const { dias, meses, largura, altura } = gantt();

    expect(dias).toHaveLength(95);
    expect(meses.map((mes) => [mes.rotulo, mes.x, mes.largura])).toEqual([
      ['out./2026', 100, 270],
      ['nov./2026', 370, 300],
      ['dez./2026', 670, 310],
      ['jan./2027', 980, 70],
    ]);
    expect([largura, altura]).toEqual([100 + 950, 40 + 8 * 20]);
  });

  test('fim de semana e feriado não são úteis; sábado útil, só se o calendário disser', () => {
    const util = (data: string, dias = gantt().dias) => dias.find((dia) => dia.data === data)?.util;

    expect(util('2026-10-09')).toBe(true); // sexta
    expect(util('2026-10-10')).toBe(false); // sábado
    expect(util('2026-10-12')).toBe(false); // segunda, Nossa Senhora Aparecida
    expect(
      util(
        '2026-10-10',
        montarGantt(programacaoDoReadme, [], { feriados: [], sabadoUtil: true }, Tamanho).dias,
      ),
    ).toBe(true);
  });

  test('a barra vai do começo do primeiro dia ao fim do último, com os dias parados dentro', () => {
    // MOB, de segunda 05/10 a sexta 09/10; FUN, de 13/10 a 26/10, por cima do feriado.
    expect(linha('MOB').barra).toEqual({ x: 100, largura: 50 });
    expect(linha('FUN').barra).toEqual({ x: 100 + diasDesde('2026-10-13') * 10, largura: 140 });
    expect(linha('MOB').folga).toBeNull();
  });

  test('a folga total vai do fim cedo ao fim tarde, e o marco fica no meio do seu dia', () => {
    // ELE termina cedo em 11/12 e tarde em 16/12: cinco dias de calendário.
    expect(linha('ELE').folga).toEqual({ x: 100 + (diasDesde('2026-12-11') + 1) * 10, largura: 50 });
    expect(linha('ELE').critica).toBe(false);

    expect(linha('ENT').barra).toBeNull();
    expect(linha('ENT').marco).toEqual({ x: 100 + diasDesde('2027-01-07') * 10 + 5 });
  });

  test('uma seta por vínculo, em destaque as do caminho crítico', () => {
    const { ligacoes } = gantt();
    const caminhoDe = (de: string, para: string) =>
      ligacoes.find((ligacao) => ligacao.predecessora === de && ligacao.sucessora === para);

    expect(ligacoes).toHaveLength(8);
    expect(
      ligacoes
        .filter((ligacao) => !ligacao.critica)
        .map((ligacao) => `${ligacao.predecessora}→${ligacao.sucessora}`),
    ).toEqual(['EST→ELE', 'ELE→REB']);
    // Do fim de MOB, desce até FUN e chega no começo dela.
    expect(caminhoDe('MOB', 'FUN')?.caminho).toBe(`M150,50 H156 V70 H${100 + diasDesde('2026-10-13') * 10}`);
  });

  test('cada tipo de vínculo amarra os lados certos', () => {
    const atividade = (
      codigo: string,
      inicio: string,
      termino: string,
    ): Esquemas['AtividadeProgramadaResposta'] => ({
      codigo,
      descricao: codigo,
      duracaoEmDiasUteis: 1,
      inicioCedo: inicio,
      terminoCedo: termino,
      inicioTarde: inicio,
      terminoTarde: termino,
      folgaTotal: 0,
      folgaLivre: 0,
      ehCritica: true,
      ehMarco: false,
    });
    const programacao: Esquemas['ProgramacaoResposta'] = {
      dataDeInicio: '2026-10-05',
      dataDeTermino: '2026-10-09',
      duracaoEmDiasUteis: 5,
      atividades: [atividade('A', '2026-10-06', '2026-10-07'), atividade('B', '2026-10-06', '2026-10-08')],
      dependenciasCriticas: [],
    };
    const tipos = ['InicioInicio', 'TerminoTermino', 'InicioTermino'] as const;
    const ligacoes = montarGantt(
      programacao,
      tipos.map((tipo) => ({ predecessora: 'A', sucessora: 'B', tipo, defasagemEmDiasUteis: 0 })),
      { feriados: [], sabadoUtil: false },
      Tamanho,
    ).ligacoes;

    // A ocupa de 110 a 130; B, de 110 a 140.
    expect(ligacoes.map((ligacao) => ligacao.caminho)).toEqual([
      'M110,50 H104 V70 H110',
      'M130,50 H136 V70 H140',
      'M110,50 H104 V70 H140',
    ]);
  });

  test('a linha de hoje só aparece se hoje cai dentro da obra', () => {
    expect(gantt('2026-10-14').hoje).toBe(100 + diasDesde('2026-10-14') * 10);
    expect(gantt('2027-02-01').hoje).toBeNull();
    expect(gantt().hoje).toBeNull();
  });
});
