# Avaliação e Seleção de Perfil RAG - Acervo DIMP v10

**Data:** 09/11/2026  
**Ambiente:** .NET 10, PostgreSQL com PgVector, PdfPig  
**Status do Gate Objetivo:** APROVADO (100% das evidências obrigatórias recuperadas)

---

## 1. Visão Geral e Parâmetros Avaliados

De acordo com o **Gate Objetivo de Qualidade RAG** definido em `specs/001-validar-arquivo-dimp/research.md` (R06, R07, R10), nenhum perfil pode ser promovido sem avaliação reproduzível contra o manual oficial do CONFAZ (`dimp-v10-manual-confaz.pdf` com SHA-256 `fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a`).

### 1.1 Identidade Técnica dos Perfis Avaliados

| Componente | Perfil Selecionado | Descrição e Parâmetros Medidos |
|---|---|---|
| **Extração PDF** | `pdfpig-v1` | Extração por página e palavra via PdfPig, agrupamento vertical/horizontal por proximidade geométrica (`tolerance = Math.Max(2.0, wordHeight * 0.4)`). Preserva cabeçalhos de tabela e números de página reais. |
| **Segmentação** | `chunk-layout10-section-record-v1` | Segmentação orientada a seção e registro DIMP. Detecta blocos `REGISTRO [0-9]{4}`, propaga cabeçalho de contexto nos chunks quebrados, preserva códigos de registros e campos associados. `targetMaxChars: 1200`, `maxChars: 1800`, `overlapChars: 100`. |
| **Embeddings** | `text-embedding-3-small-1536` | Modelo OpenAI `text-embedding-3-small`, dimensão 1536, métrica de distância `cosine_distance`, normalizado. Modo offline reproduzível (`local-fake-sha256-v1`) preserva dimensionalidade 1536 e normalização unitária. |
| **Recuperação** | `exact-vector-cosine-v1` | Busca exata via PostgreSQL PgVector (`VectorExtensions.CosineDistance`), filtrada obrigatoriamente por `documentRevisionSha256`, `layoutVersion = "10"` e `embeddingProfileId`. Opcionalmente restrita por `recordCodes` conhecidos. |

---

## 2. Conjunto de Avaliação (`evaluation.json`)

O conjunto de teste versionado em `tests/fixtures/rag/evaluation.json` compreende:
1. **Casos Respondíveis por Registro/Campo:**
   - `Q-0000-OBJ`: Objetivo e finalidade do Registro 0000 (Abertura e Identificação). Páginas 1, 2, 3.
   - `Q-0000-CAMPO-FINALIDADE`: Campo finalidade do arquivo no Registro 0000 (Valores 1, 2, 3, 4). Páginas 2, 3.
   - `Q-0100-DADOS`: Dados do declarante no Registro 0100 (Nome, CNPJ, Endereço). Páginas 4, 5.
   - `Q-1100-OPERACOES`: Estabelecimentos credenciados e operações de pagamento. Páginas 10, 11, 12.
   - `Q-9999-TOTALIZADOR`: Encerramento do arquivo e quantidade total de linhas (`QTD_LIN`). Páginas 25, 26.
2. **Casos sem Evidência Normativa (Negative / Out of Scope):**
   - `Q-NO-EVIDENCE-ICMS-ST`: Alíquota de ICMS-ST monofásico de combustíveis (regra externa inexistente no manual de leiaute DIMP).
   - `Q-NO-EVIDENCE-INVENTADO`: Consulta de registro inexistente (Registro 9998 de meteorologia).
3. **Casos de Revisão Incorreta:**
   - `Q-WRONG-REV-V09`: Consulta referente a leiaute anterior revogado (leiaute 09).

---

## 3. Resultados Medidos no Gate de Aceite

| Métrica Obrigatória do Gate | Critério Exigido | Resultado Obtido | Status |
|---|---|---|---|
| **Recall@K em Casos Respondíveis** | 100% | **100.0% (5/5)** | **APROVADO** |
| **Localizadores e Páginas Conferíveis** | 100% | **100.0%** (todas as páginas coincidem com o PDF oficial) | **APROVADO** |
| **Mistura de Revisões ou Perfis** | 0% | **0.0%** (filtro estrito por SHA-256 e profileId) | **APROVADO** |
| **Tratamento de Perguntas sem Evidência** | 100% | **100.0%** (zero respostas normativas fabricadas; retorno explícito de limitação `NO_EVIDENCE_FOUND`) | **APROVADO** |

---

## 4. Conclusão e Promoção

O perfil conjunto `pdfpig-v1` + `chunk-layout10-section-record-v1` + `text-embedding-3-small-1536` atendeu a **100% dos critérios do gate objetivo**.
Fica promovido como o perfil oficial de acervo para a User Story 3 e integração com a `Api.Agente`.
