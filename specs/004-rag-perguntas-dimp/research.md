# Pesquisa e decisões

| Decisão | Evidência | Consequência |
| --- | --- | --- |
| Completar a ingestão existente antes de hospedar | `SubmitAsync` grava o trabalho como `recebida`; o único ponto encontrado para `ProcessJobAsync` é sua definição. A T091 previa um consumidor com recuperação. | Implementar e validar o consumidor com o mesmo contrato REST e a mesma fila. |
| Começar por uma única revisão oficial | O [manifesto S01](../../docs/rules/dimp-v10/manifest.json) identifica a fonte e o hash. | Indexar o Manual V10 com proveniência e evitar combinar revisões na primeira entrega. |
| Avaliar embeddings reais com uma amostra DIMP | O [ECF do Kaio](https://github.com/kaiosmacedo/Agente-ECF/blob/bf04d3722c420b3f29eaa611e654db97933aef62/docs/avaliacao/recuperacao.md) mede a recuperação por página. A [ECD da Carol](https://github.com/carolbabiasi/AgenteValidacaoECD/blob/ecf05303cc695e142b5e00204a701bb23d8d7a69/specs/001-validacao-ecd/evidence/rag-corpus-ingest.md) registra a ingestão do manual com embeddings reais. | Reutilizar a avaliação do DIMP, registrar parâmetros medidos e verificar perguntas fora do catálogo. |
| Separar a operação do agente e do acervo | O [roteiro do Kaio](https://github.com/kaiosmacedo/Agente-ECF/blob/bf04d3722c420b3f29eaa611e654db97933aef62/docs/roteiro.md) e o [guia do Roberto](https://github.com/Roberto-Rabelo/infis-agente-pareceres-tributario/blob/fe4388c4d4e27696574dfa0b37479ae7c47b90dc/specs/003-publicacao-cloud/README.md) descrevem deploy e consumo do RAG. | Hospedar os serviços separadamente e verificar o acervo e o acesso antes de promover o agente. |

O baseline de qualidade segue os critérios da [pesquisa da Feature 001](../001-validar-arquivo-dimp/research.md). O perfil fake comprova o fluxo técnico. A avaliação semântica exige vetores gerados com o modelo selecionado e uma amostra de aceite separada da calibração.
