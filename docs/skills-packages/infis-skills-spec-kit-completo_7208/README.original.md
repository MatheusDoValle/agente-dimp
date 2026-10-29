# Skills para GitHub Spec Kit - INFIS

Copie `.agents/skills/` para a raiz do projeto inicializado com Spec Kit. As seis Skills são independentes e funcionam como instruções para um agente compatível, por exemplo Codex. Não são comandos nativos `/speckit.*`.

## Skills
- rag-pgvector: ingestão, embeddings e recuperação rastreável na Api.RAG
- mcp-client-server: comunicação MCP Api.Agente -> Api.RAG
- semantic-kernel-tools: ferramentas e Function Calling com autorização
- memoria-mongodb: histórico isolado no MongoDB
- controle-custos-llm: medição por tentativa no PostgreSQL
- testes-agentes-ia: testes xUnit, integração e evidências SDD

## Uso
1. `specify init AgenteFinanceiro --integration codex --script ps`
2. `cd AgenteFinanceiro`
3. Copie a pasta `.agents/skills` deste pacote para o projeto (preserve as demais Skills existentes).
4. Abra `code .` e execute `codex.cmd`.
5. Solicite: `Leia .agents/skills/rag-pgvector/SKILL.md e implemente as tarefas aprovadas da feature referentes à ingestão RAG. Respeite a Constitution e execute os testes.`
6. Substitua o nome da Skill e o objetivo conforme a etapa.

## Observações
- Ajuste nome do projeto, obrigação fiscal e requisitos nas especificações; não invente documentação oficial.
- O agente precisa dispor de acesso ao repositório e às ferramentas de compilação para executar as etapas.
- Skill não substitui `constitution`, `specify`, `plan`, `tasks` ou `implement`.
