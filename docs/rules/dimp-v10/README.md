# Inventario S01 / DIMP V10

Catalogo `0.1.0`, criado em 2026-11-04. Contem **620 unidades de regra**,
**199 campos**, **29 registros** e matriz das **58 paginas** de S01.
Uma unidade pode conter mais de uma condicao. A contagem nao representa predicados
atomicos independentes, cobertura fiscal nem validadores implementados.

- `catalog.schema.json`: schema Draft 2020-12, identidade, localizador, condicoes,
  severidade, estado da interpretacao, validatorId e vinculos planejados de testes.
- `manifest.json`: revisao S01 e hash exato do catalogo; fixe ambos antes de usa-lo.
- `catalog.json`: tabelas por campo, estrutura por registro, observacoes e orientacoes.
- `source-units.json`: texto com coordenadas/localizadores e celulas das tabelas,
  preservados para revisao. Conteudo de manuais oficiais, sem arquivos de clientes.
- `source-matrix.csv`: pagina -> regras/registros/unidades, incluindo contexto justificado.
- `source-conflicts/`: os tres conflitos originais e sete novos impedimentos/ambiguidades.
- `research-sources.json`: documentos oficiais comparados, sem troca automatica de revisao.

## Metodo e limites

Leitura das 58 paginas com extracao tabular por limites geometricos e texto posicionado
(pdfplumber/pypdf locais). As paginas24 e 43 foram renderizadas para conferir as tabelas
e divergencias. A tabela de hierarquia das paginas6-9 tambem representa o diagrama da
p.2. Tabelas gerais e legendas apoiam as regras dos campos; apresentacao/cabecalhos
nao sao predicados de validacao isolados.

Os localizadores `pNN/table-NN/row-NN` e `pNN/text-NNN/yNNN` usam numero de pagina
fisica baseado em 1 e coordenadas do PDF. Textos que atravessam paginas permanecem em
unidades separadas, com o mesmo registro; consultar tambem a unidade adjacente. Nao
executar fragmentos fora do contexto. Nomes/numeros/tamanhos divergentes foram mantidos.
Consulta a tabela IBGE/MOC ou a fatos nao presentes no arquivo requer fonte/contexto;
ausencia desses dados nao autoriza aprovar ou declarar nao aplicavel.

Nao ha validador fiscal implementado neste inventario. `planned` e `blocked` descrevem
o software. `tests` contem IDs de casos a desenvolver: `fixture:null` e
`expectedResult:null` significam **oraculo ainda nao estabelecido**, nunca teste aprovado.
Ha cenarios independentes iniciais em `tests/fixtures/dimp-v10/expected/manifest.json`;
eles nao cobrem todos os enunciados. T029 e o aceite de cobertura permanecem abertos.

O registro carregavel rejeita identidade/hash diferentes, campos desconhecidos,
localizador invalido, IDs duplicados, dependencias ausentes/ciclicas e declaracoes de
implementacao sem testes minimos. A cobertura do software e separada das instancias de
uma execucao. Nenhuma regra planejada vira nao aplicavel. O registro nao executa os
enunciados, nao avalia codigo descrito pelo JSON e nao e registrado no host funcional.

## Proveniencia e reproducao

Conferir SHA-256 do PDF e do catalogo em PowerShell:

~~~powershell
Get-FileHash ./docs/referencias/dimp-v10-manual-confaz.pdf -Algorithm SHA256
Get-FileHash ./docs/rules/dimp-v10/catalog.json -Algorithm SHA256
~~~

A revisao foi conferida com o PDF baixado novamente da URL original e permaneceu
identica. a 19/a 06 tem conteudo/hash diferentes. Nenhuma foi adotada como errata de S01.
Regenerar extracao nao substitui revisao de enunciados, aplicabilidade ou oraculos.
