# Catálogo de regras DIMP: desenho, cobertura e impedimentos

Referência S01: [manual local](../../docs/referencias/dimp-v10-manual-confaz.pdf),
58 páginas, SHA-256 FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A.
Este documento define a estrutura do inventário; não é o inventário completo nem
evidência de que todas as regras foram implementadas.

## Organização do inventário

Durante a implementação, produzir catálogo versionado em docs/rules/dimp-v10/
com manifesto e entradas por registro, acompanhado de matriz página → regras.
IDs internos terão prefixo DIMP10 e não serão apresentados como códigos oficiais.

Cada entrada precisa de:
- id, catalogVersion, revisionSha256, página/seção/registro/campo e localizador.
- enunciado verificável e pequeno trecho de apoio; aplicabilidade e pré-condições.
- categoria física, estrutural, campo, condicional, relacionamento ou agregação.
- dependências e dados necessários, distinção de contexto interno/externo.
- interpretação da fonte: resolved, ambiguous ou conflicting.
- implementação: planned, implemented ou blocked; validatorId.
- testes positivos/negativos/condicionais/de fronteira e seus resultados esperados.
- regra de severidade e motivo; referência oficial de resolução de conflito, se houver.

Separar cobertura do software de avaliação em uma execução:
uma regra implementada pode ser não aplicável a um arquivo; uma regra não implementada
não pode ser rotulada não aplicável apenas para aumentar cobertura.

Os contadores coverage do relatório contam instâncias (regra + ocorrência), com
categorias exclusivas e a igualdade applicable = passed + failed + notExecuted +
blockedBySourceConflict. Não substituir a matriz de implementação por esses números.
Ver semântica e exemplo em data-model.md.

## Leitura e execução determinística

S01 pp.2–3 descreve texto Latin-1, campos por pipe, vazios preservados e CRLF.
Validar bytes antes de normalizar; não remover espaços/linhas/vazios silenciosamente.
Latin-1 decodifica qualquer byte: a decodificação sozinha não comprova a origem da codificação.

Usar etapas física/léxica, estrutura/hierarquia, campos, condições/relacionamentos
e agregações. Offset e linha identificam evidência; interpretação numérica/data
independe da cultura do servidor. Índices SQLite temporários suportam cruzamentos
sem exigir arquivo inteiro em RAM e seguem a exclusão do original.

## Registros a cobrir

As páginas referem-se à cópia S01; este rol é um mapa de navegação, não uma regra por código.

| Grupo | Registro e página inicial |
| --- | --- |
| 0 | 0000(9), 0001(13), 0002(13), 0005(15), 0006(16), 0100(17), 0105(19), 0109(21), 0200(21), 0201(24), 0209(25), 0300(26), 0600(28), 0700(30), 0990(30). |
| 1 | 1001(31), 1100(31), 1110(35), 1115(37), 1120(41), 1200(44), 1220(47), 1500(49), 1600(53), 1990(56). |
| Controle | 9001(56), 9900(57), 9990(57), 9999(58). |

Cobrir também apresentação, tabelas gerais, observações e orientações:
29 registros não equivalem a 29 regras. A matriz de fonte deve contabilizar todas
as 58 páginas, inclusive classificação justificada das páginas sem regra executável.

## Conflitos observados

| ID interno | Evidência em S01 | Decisão de tratamento |
| --- | --- | --- |
| SF-001 | COD_FIN: tabelas/orientação incluem 9 (pp.5/9–12), mas valores válidos na p.10 listam 1–6. | blocked_by_source_conflict até resolução oficial. |
| SF-002 | 1120 COD_MUN_DEST: tabela p.43 indica tamanho 8; validação p.44 exige 7 dígitos. | Não escolher tamanho silenciosamente. |
| SF-003 | 1115 IND_TP_PIX: p.41 menciona naturezas 6/8/12; validação exclui demais após tratar 6/8. | Não inferir regra para natureza 12. |

Não são erros constatados no arquivo de um cliente. São conflitos da fonte que
devem aparecer como limitação documental e revisão humana.
Resolução aceita: publicação/revisão oficial identificável ou esclarecimento oficial
registrado com origem/data/localizador. Não aceitar resposta de LLM como evidência.
Uma revisão substituta exige nova identificação da fonte e avaliação das regras afetadas;
não reescrever o hash de S01 como se o arquivo original nunca tivesse sido usado.

## Critérios de cobertura

1. Revisão página por página, incluindo tabelas e texto adjacente.
2. Inventário exaustivo validado contra a matriz de fonte.
3. Regra → validador → testes esperados com evidência reproduzível.
4. Cenários condicionais e relações entre registros também precisam de testes.
5. Totalizadores possuem regras próprias; não derivar todos de um contador genérico.
6. Zero regras aplicáveis omitidas ou impedidas no aceite de cobertura completa.
7. Relatório de cobertura nunca confunde taxa de testes com completude do catálogo.

SF-001/002/003 permanecem impedimentos explícitos de aceite. O planejamento permite
tarefas de investigação/infraestrutura, mas não declara as regras afetadas resolvidas.