# Skills complementares INFIS para o Agente DIMP

Data: 2026-10-29.
Pacote: infis-skills-spec-kit-completo_7208.zip, localizado em Downloads.
Destino: .agents/skills/ do projeto C:/Infis/CursoIa/AgenteDIMP.
Resultado: 6 novas skills; total de 18 skills locais, preservando as 12 anteriores.

## O que cada skill faz

| Skill | Responsabilidade | Artefatos e verificações |
| --- | --- | --- |
| [rag-pgvector](../.agents/skills/rag-pgvector/SKILL.md) | Ingestão de manuais, segmentação, embeddings e recuperação na Api.RAG. | Proveniência, revisão/hash, pgvector, deduplicação e avaliação da recuperação. |
| [mcp-client-server](../.agents/skills/mcp-client-server/SKILL.md) | Comunicação MCP entre Api.Agente cliente e Api.RAG servidor. | Contratos das ferramentas, transporte compatível, erros tipados, cancelamento e teste entre serviços. |
| [semantic-kernel-tools](../.agents/skills/semantic-kernel-tools/SKILL.md) | Integração e execução controlada de ferramentas por Semantic Kernel e Function Calling. | Funções tipadas, argumentos validados, lista de ferramentas, limites, falhas e rastreabilidade. |
| [memoria-mongodb](../.agents/skills/memoria-mongodb/SKILL.md) | Memória/contexto mascarado no MongoDB Atlas. | Coleção conversation_contexts, separação por execução, contexto limitado, expiração e exclusão explícita. |
| [controle-custos-llm](../.agents/skills/controle-custos-llm/SKILL.md) | Medição de tokens e custos por tentativa de chamada. | Ledger PostgreSQL de cada serviço, correlação MCP, métricas desconhecidas e preços versionados. |
| [testes-agentes-ia](../.agents/skills/testes-agentes-ia/SKILL.md) | Testes e evidências para as funcionalidades do agente. | xUnit, doubles, integração REST/MCP, PostgreSQL/pgvector, MongoDB e contratos de resposta. |

São instruções para orientar o agente, com SKILL.md e metadados agents/openai.yaml.
Não são novos comandos nativos do Specify CLI, bibliotecas instaladas na aplicação
ou serviços já em execução.

## Como se conectam às tarefas existentes

Referência: [tasks.md](../specs/001-validar-arquivo-dimp/tasks.md).

| Skill | Tarefas relacionadas |
| --- | --- |
| rag-pgvector | T081, T085–T095: contratos, acervo, extração, segmentação, embeddings, avaliação e recuperação. |
| mcp-client-server | T082, T095–T097 e T111: contratos MCP, servidor, cliente, adaptador e consumo correlacionado. |
| semantic-kernel-tools | T097 e T100–T105: funções controladas, contexto, conector, validação e integração. |
| memoria-mongodb | T098–T099, T103 e T115: persistência, testes, integração e limpeza de contexto. |
| controle-custos-llm | T013, T090, T106 e T108–T114/T116: tentativa, embeddings, custos, correlação e retenção operacional. |
| testes-agentes-ia | Suítes de teste das quatro jornadas e validações T118–T123, conforme os requisitos. |

Essa relação orienta a seleção da skill; não altera dependências, prioridades,
checkboxes ou aprovação de tarefas. O arquivo tasks.md permanece com todas as tarefas abertas.

## Exemplos de uso no chat

Os exemplos abaixo são pedidos a enviar no chat quando a etapa correspondente for
solicitada; não são comandos PowerShell nem uma execução realizada por esta instalação.

~~~text
$rag-pgvector Revise as tarefas de ingestão RAG e explique os pré-requisitos antes de implementar.

$mcp-client-server Revise o contrato MCP e sua compatibilidade com o plano do Agente DIMP.

$semantic-kernel-tools Revise a lista de ferramentas de leitura prevista para o agente.

$memoria-mongodb Confira as tarefas T098 e T099 contra o ADR-0002 e a política de contexto.

$controle-custos-llm Confira o rastreamento de tentativas e a separação dos ledgers dos serviços.

$testes-agentes-ia Revise a cobertura de testes prevista no tasks.md e identifique lacunas.
~~~

Quando houver pedido de implementação, a skill deve atuar somente nas tarefas e
permissões abrangidas por esse pedido. A geração desta documentação não executa
implementação, chamadas pagas, deploy ou publicação Git.

## Ajustes do pacote para este projeto

As instruções do ZIP eram genéricas. Os seis SKILL.md receberam contexto do Agente DIMP
e os seguintes ajustes para preservar as decisões já registradas:

| Tema | Aplicação no projeto |
| --- | --- |
| Fonte e regras DIMP | Validar layout, registros, campos e regras oficiais; a referência a XSD foi removida da skill de testes. |
| Autorização | Respeitar a exceção temporária EX-001; não criar login, perfis ou permissões nesta entrega. |
| Ferramentas | Manter lista explícita de funções de leitura, validação de argumentos e ausência de efeitos externos não previstos. |
| Memória | MongoDB Atlas conforme ADR-0002; nomes de campos e índices alinhados ao modelo, leitura com expiresAt e exclusão explícita ao finalizar. |
| RAG | Busca exata inicial e perfil de recuperação avaliado; não adotar ANN ou topK/limiar arbitrários por causa de exemplos. |
| Tamanho de arquivo | Nenhum teto de produto foi aprovado; registrar e validar limites operacionais efetivos. |
| Propriedade dos dados | Api.RAG guarda seu ledger de embeddings; Api.Agente agrega referências via MCP, sem ler tabelas rag nem duplicar custo. |
| Falha de medição | Registrar tentativa antes de chamar o provedor; falha do ledger impede nova chamada externa. |

O contexto mascarado do MongoDB conserva o ciclo de vida da execução.
Os 30 dias continuam sendo o prazo do relatório mascarado em PostgreSQL.
A remoção TTL é auxiliar e não substitui a exclusão explícita prevista no plano.

Os metadados agents/openai.yaml foram preservados exatamente como no ZIP.
As duas skills importadas anteriormente também permaneceram intactas.
Os ajustes não mudam constituição, especificação, plano, contratos ou tarefas.

## Origem e rastreabilidade

SHA-256 do ZIP:
F4138C5049E608506CC739ABBC047EB1AE825C2946FA2C2E838CBFBD2395B442.

- [Manifesto da instalação](infis-skills-spec-kit-completo_7208.manifest.json):
  hashes originais/instalados e descrição dos ajustes em cada skill.
- [README original do pacote](skills-packages/infis-skills-spec-kit-completo_7208/README.original.md):
  cópia preservada para consulta, sem substituir o README do projeto.
- [Guia das skills anteriores](skills-speckit.md).

O exemplo AgenteFinanceiro no README original não foi executado.
Não foi necessário reinicializar o Spec Kit nem sobrescrever skills preexistentes.
O ZIP original em Downloads foi preservado.

## Validação e disponibilidade

As seis skills passaram no quick_validate.py da skill-creator.
A verificação final confere metadados YAML, nomes, links, hashes e preservação das
skills anteriores. Esses checks não equivalem a build, testes ou implantação da aplicação.

As skills ficam disponíveis a partir do próximo turno em um chat cujo projeto use
C:/Infis/CursoIa/AgenteDIMP como pasta principal.
A instalação local não muda automaticamente o cadastro de pasta do projeto no Codex.
O próximo passo do fluxo SDD continua sendo speckit-analyze.
