# Contratos do fluxo RAG

Os formatos vigentes estão em [REST da RAG](../../001-validar-arquivo-dimp/contracts/rag.openapi.json) e [MCP da RAG](../../001-validar-arquivo-dimp/contracts/rag-mcp.md).

| Interface | Comportamento de aceite |
| --- | --- |
| `POST /api/v1/documentos/ingestoes` | Multipart com `file`, `sourceUrl`, `layoutVersion`, `expectedSha256`, `extractionProfileId`, `chunkProfileId`, `embeddingProfileId`. Responde `202` para nova submissão e `200` para a mesma ingestão concluída. Mantém o mesmo ID para a combinação de revisão e perfis. |
| `GET /api/v1/documentos/ingestoes/{id}` | Expõe estado e etapa. `concluida` só após a publicação do corpus. |
| MCP `search_manual` e `get_manual_chunk` | Consultam revisão e perfil fixados pelo host. Retornam referências conferíveis e distinguem ausência de evidência, incompatibilidade e erro técnico conforme contrato. |
| `POST /demo/dimp` | Conserva campos opcionais `pergunta` e `arquivo`, Bearer e envelope atual. Pergunta aberta sem arquivo pode usar RAG. A citação fica em `analise`; indisponibilidade e falta de evidência têm indicação explícita. |
| `GET /.well-known/agent-card.json` e `POST /a2a` | Seguem a configuração e o contrato SOFIA já aplicados no agente DIMP. |

Em hospedagem, a credencial MCP é de serviço. As operações de ingestão usam permissão administrativa própria. A decisão de acesso fica registrada antes de expor os endpoints.
