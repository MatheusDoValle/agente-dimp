# Avaliação de recuperação do Manual DIMP V10

A avaliação da Feature 004 usa a revisão oficial S01, identificada no [manifesto do catálogo](../rules/dimp-v10/manifest.json). A extração é `pdfpig-v1`, a segmentação é `chunk-layout10-section-record-v1` e a busca retorna até cinco trechos do PostgreSQL com pgvector.

O conjunto original em `items` de [evaluation.json](../../tests/fixtures/rag/evaluation.json) continua sendo usado como teste de regressão sobre trechos sintéticos e embeddings determinísticos. O resultado anterior de 5/5 verificava esses exemplos. Ele não mede a recuperação semântica no PDF oficial nem valida todas as páginas e casos sem evidência.

## Critérios do ensaio no PDF oficial

- `semantic.calibration` serve para medir a distância da evidência esperada e das respostas irrelevantes. A distância máxima é candidata quando separa as evidências que dependem da busca vetorial dos casos sem evidência.
- `semantic.acceptance` usa perguntas separadas, com evidências por página e palavra, casos sem evidência, revisão errada e perfil errado.
- A aprovação exige todas as evidências obrigatórias entre os resultados, referências com páginas e revisão verificáveis, nenhuma mistura de perfil ou revisão e ausência explícita de evidência nos casos negativos.
- O [roteiro executável](../../specs/004-rag-perguntas-dimp/Measure-RagRetrieval.ps1) mede esses critérios via MCP. O aceite é feito com a distância configurada na API e `text-embedding-3-small-1536`.

## Resultado da verificação local

| Verificação | Resultado |
| --- | --- |
| Validação do perfil e do orçamento | 4 testes unitários aprovados. |
| Ingestão, MCP e isolamento dos índices | 15 testes de integração aprovados. O perfil semântico não retorna vetores do índice determinístico nem aciona o provedor se não há corpus desse perfil. |
| Calibração com o índice determinístico de S01 | 1 de 3 casos passou. Uma consulta sem evidência recebeu trechos e a amostra não sugeriu um limiar. |
| Aceite em execução diagnóstica com o índice determinístico | 2 de 7 casos passaram, ambos controles de isolamento. O ensaio usou distância máxima de 2, sem corte efetivo de relevância. |
| Reindexação semântica de S01 | Concluída, com 492 trechos e vetores de 1.536 dimensões. |

Os limites iniciais de desenvolvimento para a carga semântica são 600 chamadas, 1.000.000 de tokens estimados de forma conservadora, 30 segundos por chamada e 900 segundos por execução. O SDK faz uma tentativa por chamada. Esse ensaio determinístico não calibra o limiar do perfil semântico.

## Resultado com embeddings da OpenAI

A ingestão `0d654e51-d1f8-4184-ae3e-6d5e6968762f` publicou 492 trechos da revisão S01 com `text-embedding-3-small-1536` em 398 segundos no ambiente local. O índice de embeddings determinísticos segue identificado pelo perfil próprio.

| Ensaio | Resultado |
| --- | --- |
| Calibração | 0 de 3 casos aprovados. As perguntas positivas não trouxeram o trecho obrigatório entre os cinco primeiros e a pergunta negativa recebeu trechos. A calibração não sugeriu uma distância máxima. |
| Aceite diagnóstico, sem corte de relevância | 3 de 7 casos aprovados. Passaram o total de linhas e os controles de revisão e perfil incorretos. |
| Critério SC-002 no ensaio inicial | Pendente após o ensaio apenas vetorial. |

A inspeção no PostgreSQL confirmou que os trechos de `DT_OP` (p. 36), `HORA` (p. 38) e `DT_CANC` (p. 45) foram extraídos. As consultas de data da operação e de cancelamento chegaram à página esperada em alguns casos, mas trouxeram outro trecho da página. A busca híbrida manteve os 492 vetores da revisão e passou a expandir a consulta com nomes do leiaute V10. A busca também reserva espaço para trechos que contêm uma definição de campo ou regra associada aos termos identificados na pergunta. Esses trechos têm `matchType: lexical`. Os demais passam pelo corte de distância, com `matchType: semantic`.

## Aceite após o ajuste

- Calibração com 7 casos. As seis perguntas positivas encontraram evidência. A maior distância necessária para uma correspondência vetorial foi `0.3105399605674988`. O primeiro resultado da consulta fora do escopo, medido com o corte desativado, ficou em `0.42183083288980894`.
- A Api.RAG foi reiniciada com `Processing__MaximumCosineDistance=0.36618539672865386`. A conferência com o corte ativo passou nos 7 casos de calibração. O aceite local passou em 12 de 12 casos, com `SemanticGatePassed: true`.
- A pergunta original “Qual registro tem a data da venda?” retornou `DT_OP`, p. 36. O teste também recuperou a relação entre os registros 1110 e 1115, o campo `DT_CANC`, o total de linhas, o delimitador, quem presta as informações e a transmissão via TED-TEF. Cada citação esperada foi consultada novamente por `get_manual_chunk`, com verificação da revisão, página e conteúdo.
- Os testes de ausência de evidência e de isolamento de revisão e perfil passaram. O vocabulário foi refinado durante o diagnóstico. A aprovação se aplica à amostra atual do Manual V10 S01.
