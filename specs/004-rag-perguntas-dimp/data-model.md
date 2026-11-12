# Modelo de dados

Reutilizar as entidades do [modelo da Feature 001](../001-validar-arquivo-dimp/data-model.md) e as migrações do `KnowledgeDbContext`.

| Entidade | Identidade e invariantes |
| --- | --- |
| Revisão de documento | Hash SHA-256 do PDF, leiaute, fonte oficial, páginas e referência ao armazenamento. |
| Trabalho de ingestão | Uma combinação de revisão, extração, segmentação e embedding. Status observável de recebida a concluída ou falhou. |
| Item de fila e checkpoint | Claim exclusivo com lease e fencing. Checkpoint persistido por trabalho, hash do trecho e perfil. Tentativa desconhecida exige reconciliação. |
| Trecho e vetor | Texto com páginas, seção, código de registro/campo quando disponível e hash. Vetor associado ao perfil com modelo, dimensão e métrica compatíveis. |
| Publicação do corpus | Vincula revisão e perfis a uma ingestão completa. Consultas e resolução de referências excluem trechos de ingestão incompleta. |
| Evidência em resposta | Referência ao trecho, revisão, página ou seção e fonte. Somente dados necessários à resposta e à rastreabilidade. |

Persistir uma mudança de estado que torne a publicação observável de forma atômica. Evitar novas tabelas quando o modelo existente representar o estado com as mesmas garantias.
