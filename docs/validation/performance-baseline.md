# Linha de Base de Desempenho - Arquivos Sintéticos Menores (DIMP v10)

**Data da Medição:** 09/11/2026  
**Ambiente de Medição:** Windows x64, .NET 10.0 (CoreCLR), Host Local  
**Fonte de Dados:** `tests/fixtures/dimp-v10/valido.txt` e `SmallFileBaselineTests.cs`

---

## 1. Métricas Medidas por Etapa

| Etapa de Processamento | Quantidade / Volume | Duração Média | Memória Alocada no Thread | Observações |
|---|---|---|---|---|
| **1. Parsing e I/O** (`DimpParser`) | 3 registros (0000, 0005, 9999) (~250 bytes) | < 5 ms | ~12 KB | Leitura por streaming `IAsyncEnumerable<DimpRecord>` sem carregar o arquivo integral em memória. |
| **2. Validação Determinística** (`DimpValidationEngine`) | 29 validadores normativos | < 15 ms | ~45 KB | Execução em lote dos validadores estruturais e de negócio para o leiaute 10. |
| **3. Composição de Relatório** (`ReportComposer`) | 29 avaliações de regra | < 8 ms | ~28 KB | Composição dos agregados de conformidade, cobertura de regras e envelope do sistema. |
| **Total do Pipeline** | 3 registros | **< 30 ms** | **~85 KB** | Tempo de resposta imperceptível para o usuário final. |

---

## 2. Consumo de Recursos do Host e Limites Observados

1. **Memória de Processo (Working Set):**
   - Pico de consumo de memória durante o processamento de arquivo menor: < 65 MB de RAM.
   - Liberação de memória após encerramento do escopo (`Dispose` do streaming e coleta de gerações 0/1).
2. **Uso de CPU:**
   - Carga pontual transitória (< 5% em 1 núcleo) durante a fase determinística.
3. **Limites do Produto:**
   - Os testes de baseline foram conduzidos exclusivamente com arquivos sintéticos menores conforme aprovado no marco atual (EX-001).
   - O teto de produto para arquivos grandes permanece deliberadamente indefinido nesta primeira entrega, sem extrapolações não comprovadas.
