# Plano de implementação — RAG para perguntas DIMP

O DIMP já tem extração, segmentação, armazenamento vetorial e cliente MCP. A [tarefa T091 da Feature 001](../001-validar-arquivo-dimp/tasks.md) está marcada como concluída, mas no código inspecionado o POST chama apenas `SubmitAsync`. `ProcessJobAsync` existe sem consumidor registrado. A inicialização do RAG também aceita somente o perfil determinístico e a rota de perguntas da demo usa o catálogo local. A primeira entrega corrige o fluxo já previsto pela Feature 001.

## Contexto técnico

.NET 10, ASP.NET Core, PostgreSQL/pgvector, EF Core, PdfPig e MCP HTTP. `Api.RAG` é dona do acervo e do ledger de embeddings. `Api.Agente` consulta o RAG pelo cliente MCP, sem acesso ao banco do acervo. O perfil inicial de avaliação semântica é `text-embedding-3-small-1536`. Use o mesmo perfil na ingestão e na consulta e valide o perfil escolhido com a [avaliação existente](../../docs/validation/rag-evaluation.md).

## Fases e dependências

| Fase | Entrega | Critério para avançar |
| --- | --- | --- |
| 1. Preparação | Baseline, PDF S01, PostgreSQL local e migrações | Revisão conferida e cenário de ingestão reproduzível |
| 2. Ingestão local (US1) | Consumidor da fila, retomada, idempotência e publicação de corpus completo | Ingestão chega a `concluida` e é pesquisável via MCP em modo determinístico |
| 3. Busca semântica (US2) | Perfil real explícito, reindexação e avaliação da recuperação | Amostra de aceite aprovada com evidência por página e isolamento por perfil |
| 4. Perguntas no Swagger (US3) | Consulta ao catálogo e recuperação MCP para dúvidas abertas | Pergunta fora do catálogo cita trecho real e regressões passam |
| 5. Homologação e publicação (US4) | Serviço RAG separado, acervo persistente, controle de acesso, preview e promoção | Consulta real via preview, dados persistentes após reinício e reversão ensaiada |
| 6. Encerramento | Evidências, configuração de operação e atualização de rastreabilidade | Cada critério tem resultado, revisão, perfil e ambiente registrados |

## Decisões de implementação

- **Ingestão**: registrar revisão e trabalho de forma consistente. Usar claim atômico com lease e fencing nos checkpoints. A publicação torna consultável somente o corpus completo da revisão e do perfil, sem esconder a publicação anterior durante uma reindexação. Retomar checkpoints persistidos e manter tentativa desconhecida para reconciliação.
- **Perfis**: preservar o perfil determinístico para testes. Habilitar chamadas externas por configuração explícita, com chave e limites validados na inicialização. Falhar quando o perfil semântico estiver sem provedor, em vez de persistir vetores sintéticos com sua identificação.
- **Perguntas**: preservar respostas locais de referência. Separar “sem resposta local” da mensagem de ajuda genérica para permitir a chamada a `search_manual`. Consultar somente a revisão publicada, com limite e limiar medidos no conjunto de calibração. Usar uma resposta curta baseada nos trechos recuperados, com citações. Se houver informação insuficiente, pedir contexto ou indicar a ausência de evidência.
- **Operação**: manter o agente na Vercel e publicar a `Api.RAG` em projeto separado, no perfil `HostedQuery`, com o índice persistido no Neon/pgvector. O contêiner de consulta roda sem o consumidor da fila. A ingestão e a manutenção usam uma instância operacional separada, com credencial administrativa e cópia do PDF oficial. A conexão do contêiner usa TLS com validação de hostname e uma conta que lê o acervo e grava somente as tentativas de consulta.
- **Publicação**: usar uma opção de configuração para ativar a busca no agente, separada do Bearer A2A. Revisar a aplicação de [EX-001](../001-validar-arquivo-dimp/spec.md#ex-001--adiamento-temporário-da-autorização) antes da hospedagem compartilhada. A fase de nuvem exige autenticação entre serviços, proteção da ingestão, segredos no provedor e teste de acesso. A escolha de host e orçamento será registrada nesta fase.

## Verificação constitucional

Rastreabilidade ao manual e separação entre RAG e validação fiscal. Contratos MCP e A2A preservados. Consultas externas recebem somente conteúdo minimizado. Prazos, chamadas e custos ficam limitados e observáveis. Os testes usam o PDF oficial e exemplos sintéticos. [Constituição](../../.specify/memory/constitution.md)

## Artefatos

[Pesquisa](research.md) · [Modelo de dados](data-model.md) · [Contratos](contracts/fluxo-rag.md) · [Roteiro de validação](quickstart.md) · [Tarefas](tasks.md)
