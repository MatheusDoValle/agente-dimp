---
name: memoria-mongodb
description: Implementar memória conversacional no MongoDB para Api.Agente em .NET 10, com isolamento por usuário, conversa e organização, retenção, recuperação limitada e proteção de dados. Usar ao persistir ou recuperar histórico de sessões do agente.
---

# Memória conversacional MongoDB

## Contexto deste projeto

- Aplicar a especificação, o plano, os contratos e as tarefas de specs/001-validar-arquivo-dimp/.
- A primeira entrega mantém a autorização adiada por EX-001; não acrescentar login,
  perfis ou segregação por permissões fora desse marco. Manter isolamento por execução,
  validação de argumentos, ferramentas permitidas e proteção de dados.
- MongoDB Atlas guarda memória/contexto mascarado; PostgreSQL com pgvector pertence à
  Api.RAG. O arquivo original e o mapa reversível são temporários; relatório mascarado
  expira após 30 dias. Não ampliar esses ciclos de vida por instrução genérica desta skill.
- Estas orientações não autorizam executar implementação, publicar ou criar recursos
  externos sem um pedido que contemple a operação.

## Regras transversais
- Ler `.specify/memory/constitution.md` e os artefatos da feature em `specs/` antes de modificar arquivos; se inexistentes, informar e não inventar requisitos.
- Trabalhar somente no escopo das tarefas aprovadas do Spec Kit; relacionar implementação, requisito, teste e evidência.
- Respeitar .NET 10, ASP.NET Core e a separação `Api.Agente`/`Api.RAG`; não criar bibliotecas desnecessárias.
- Nunca inserir segredos no código, logs, testes ou Git; configurar via variáveis de ambiente ou secret manager.
- Não inventar schemas fiscais, regras de negócio, documentos oficiais, versões de manuais nem conclusões de conformidade.
- Validar `dotnet build` e testes relevantes; informar explicitamente quando não for possível executar.
- Registrar decisões arquiteturais significativas em ADR, sem alterar a Constitution silenciosamente.

## Fluxo de execução
1. Seguir ADR-0002, que registra MongoDB Atlas para memória/contexto da execução; confirmar retenção, exclusão e residência de dados aplicáveis sem criar histórico de chat fora da funcionalidade.
2. Modelar conversation_contexts com executionId, conversationId, userId/organizationId quando disponíveis, messages mascaradas, createdAt, updatedAt e expiresAt; identidade vem do sistema, nunca do texto do usuário.
3. Criar índice único de executionId/conversationId e TTL de campo único em expiresAt com expireAfterSeconds=0. Filtrar expiração na leitura e excluir explicitamente por execução ao finalizar; TTL assíncrono não garante exclusão imediata.
4. Implementar recuperação paginada ou limitada pelo orçamento de contexto; preservar ordem cronológica ao enviar mensagens ao modelo.
5. Persistir mensagens e resultados sem guardar indevidamente segredos, tokens, arquivos fiscais completos ou saídas de ferramentas sensíveis.
6. Garantir separação por execução no marco EX-001. Quando identidade/autorização entrar no escopo, vinculá-la no servidor e não confiar em userId enviado pelo cliente. Não introduzir perfis ou login nesta entrega.
7. Implementar política de expiração/exclusão e documentar implicações de backup; distinguir memória de conversa da fonte oficial de conhecimento RAG.
8. Tratar falhas do MongoDB sem inventar histórico; definir comportamento degradado permitido pela especificação.
9. Testar isolamento cruzado, concorrência, ordenação, retenção, exclusão e indisponibilidade.

## Critérios de entrega
- Configuração por variável de ambiente e exemplo `.env.example` sem valores reais.
- Índices, modelos, repositórios, testes e instruções de execução documentados.
