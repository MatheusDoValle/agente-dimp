# Revisao das recomendacoes do analyze

Data: 2026-10-29. Correcao documental autorizada pelo responsavel nesta conversa.
A constituicao nao foi alterada nem suas obrigacoes reduzidas.

| Achado | Tratamento no desenho e tarefas |
| --- | --- |
| C1 - identificacao do operador | spec/modelo definem operatorId/contextId do host e identityAssurance local nao autenticada; T008 exige captura e rejeicao de identificacao vazia; P04 revisto no plano. Sem login/autorizacao. |
| I1 - deduplicacao e cobranca | REST/quickstart delimitam resultados ja persistidos; modelo define fila RAG, lease/fencing, checkpoints por chunk e tentativa unknown sem retry automatico; T091 exige recuperacao/concorrencia. |
| A1 - aceite RAG | research.md fixa gate antes do experimento: 100% das evidencias obrigatorias no conjunto independente, referencias validas, zero mistura de revisao e casos sem evidencia tratados; T094 nao promove perfil reprovado. |
| U1 - tipos MCP | rag-mcp.schema.json especifica inputs, outputs e erros; rag-mcp.md aponta as definicoes; T082/T095 exigem validacao compartilhada. |
| A2 - cobertura | modelo e OpenAPI contam instancias unicas de regra/ocorrencia, categorias exclusivas e igualdade de agregacao; exemplo dois registros; T010/T073 exigem implementacao/testes. |

## Verificacao posterior

- JSON dos contratos parseavel e todas as referencias locais resolvidas.
- 123 IDs de tarefa preservados, dependencias existentes e sem ciclos.
- Matriz FR-001..FR-032 e SC-001..SC-013 preservada (45/45 com tarefas).
- Campos minimos P04 presentes no modelo e na tarefa de contexto.
- Semantica de resposta perdida e gate objetivo RAG presentes em desenho e tarefas.
- Esquema MCP de negocio versionado em 1.0.0; nenhum wire protocol manual.
- Sem alteracao da constituicao, checklist de qualidade ou autorizacao adiada.
- Antes/depois: sem arquivo extensions.yml, portanto sem hooks registrados.

Os cinco achados estao tratados no desenho. A implementacao/testes dessas capacidades
pertencem as tarefas futuras citadas, nao foram concluidos por editar documentos.
SF-001/SF-002/SF-003 continuam bloqueando o aceite de cobertura fiscal completa.
