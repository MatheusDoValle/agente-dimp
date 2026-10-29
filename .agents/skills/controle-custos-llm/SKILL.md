---
name: controle-custos-llm
description: Implementar mensuração e auditoria do consumo de tokens e custos de OpenAI e embeddings no PostgreSQL de cada serviço proprietário, com agregação correlacionada na Api.Agente, abrangendo chamadas e retentativas. Usar ao registrar métricas operacionais, estimativas e relatórios de custo.
---

# Controle de Custos LLM

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
1. Confirmar esquema, moeda, precificação por modelo, origem da tabela de preços e data de vigência; nunca inventar valores atuais.
2. Modelar uma linha por tentativa de operação com `execution_id`, `correlation_id`, serviço, provedor, modelo, operação, timestamps, duração, status e tokens de entrada/saída/embedding quando fornecidos.
3. Representar métricas desconhecidas como NULL ou estado explícito de indisponibilidade, nunca como zero.
4. Distinguir custo estimado do provedor, custo calculado e faturamento efetivo; registrar referência e versão dos preços utilizados.
5. Centralizar instrumentação em serviço/decorator/middleware testável, incluindo retentativas, chamadas de ferramentas que geram uso de LLM e embeddings.
6. Registrar a tentativa antes da chamada; se o ledger falhar, não iniciar nova chamada externa. Preservar resultados determinísticos já obtidos. Api.RAG mantém seu ledger de embeddings e fornece referências por MCP; não consultar tabelas rag diretamente nem duplicar a contabilização.
7. Expor somente agregações previstas nos contratos, correlacionadas por execução; filtros por usuário/organização seguem o marco posterior de autorização definido em EX-001. Não incluir prompts completos, credenciais ou dados fiscais nos eventos.
8. Criar migrações PostgreSQL, testes para concorrência, cálculos com decimal, preço ausente, tokens ausentes e várias tentativas.

## Critérios de entrega
- Esquema auditável e relatório por execução/modelo/período.
- Demonstração de uma chamada com medição e caso com custo indeterminado.
