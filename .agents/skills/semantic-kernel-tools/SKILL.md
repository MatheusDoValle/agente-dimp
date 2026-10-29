---
name: semantic-kernel-tools
description: Projetar, implementar e testar Function Calling com Semantic Kernel em Api.Agente .NET 10, com plugins tipados, autorização, políticas de execução, auditabilidade e proteção contra ações indevidas. Usar ao criar tools para o agente DIMP.
---

# Ferramentas Semantic Kernel

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
1. Examinar a especificação e separar ações de consulta, validações determinísticas e operações com efeitos colaterais.
2. Definir um plugin por responsabilidade e função tipada por operação; documentar entradas, saídas, erros, idempotência e permissões.
3. Conferir as APIs do Semantic Kernel instaladas na solução antes de escrever código; não copiar exemplos de versões incompatíveis.
4. Validar argumentos, limites operacionais e contexto da execução no servidor; incluir contexto de organização somente quando previsto. Não confiar apenas no schema comunicado ao LLM.
5. A escolha de função pelo modelo não concede permissão. Nesta entrega, seguir EX-001 e a lista explícita de ferramentas de leitura; ações com efeitos fiscais/externos sensíveis ficam fora do escopo e exigem especificação e autorização próprias.
6. Aplicar cancelamento, timeout, retentativas limitadas e controle de chamadas; prevenir loops e repetição de efeitos colaterais.
7. Registrar chamada, duração, status e correlação com mascaramento de dados sensíveis; não registrar segredos nem documentos completos.
8. Devolver resultados estruturados; jamais fabricar sucesso quando a ferramenta falhar.
9. Testar chamada autorizada, negada, entrada inválida, duplicação, timeout e falha de ferramenta simulada.

## Critérios de entrega
- Plugins com contratos claros, testes automatizados e política de autorização explícita.
- Demonstração do ciclo LLM -> tool -> resultado -> resposta, sem omitir erros.
