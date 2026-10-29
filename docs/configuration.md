# Configuracao de desenvolvimento

Estado: bootstrap da Fase 1. Nao ha validacao DIMP, processamento duravel,
persistencia de negocio, servidor MCP ou conector real de IA implementados.

## LocalDeterministic (padrao)

~~~powershell
dotnet run --project ./src/Api.RAG --launch-profile LocalDeterministic
dotnet run --project ./src/Api.Agente --launch-profile LocalDeterministic
~~~

Executar em terminais separados. URLs locais: http://127.0.0.1:5081/health/live
e http://127.0.0.1:5080/health/live. O retorno bootstrap_only confirma apenas
que o host iniciou. Os endpoints funcionais ainda nao existem.

As duas APIs registram doubles offline: resposta analitica sintetica e vetor SHA-256
reproduzivel de 1536 dimensoes. O vetor usa perfil local-fake-sha256-v1, sem qualidade
semantica, e nao pode ser misturado a embeddings reais.
Nenhum double exige chave, acessa rede ou declara conformidade DIMP.

LocalDeterministic define deadline global 120s, timeout de provedor 30s e zero
chamadas externas. Sao valores do perfil de desenvolvimento, ainda sem worker;
nao constituem SLA, teto de arquivo nem parametros de producao.
Validacao completa de budgets e configuracao pertence a T014.

## LocalReal (reservado)

O arquivo e o launch profile estao definidos, mas a inicializacao recusa este modo
na Fase 1. Nao ha conector real ainda. Os budgets estao nulos deliberadamente:
nao preencher com valores sem medir nem permitir chamada real por simples troca
de um booleano. T014/T090/T101 implementarao validacao e integracao.

## Nomes externos previstos

| Chave | Uso / proprietario |
| --- | --- |
| ConnectionStrings__AgentDb | PostgreSQL banco agente, usuario agente_app / Api.Agente |
| ConnectionStrings__KnowledgeDb | PostgreSQL banco rag, usuario rag_app / Api.RAG |
| ConnectionStrings__MongoDb | Mongo local ou Atlas com TLS / Api.Agente |
| MongoDb__DatabaseName | agente_memory no local, database por ambiente no Atlas |
| Rag__McpEndpoint | http://127.0.0.1:5081/mcp (implementacao futura) |
| OpenAI__ApiKey | Credencial externa, sem valor em arquivo versionado |
| OpenAI__ChatModel | Modelo de analise validado no teste opt-in |
| OpenAI__EmbeddingModel | Modelo da Api.RAG associado ao perfil/dimensao |
| Processing__GlobalDeadlineSeconds | Deadline global finito do perfil operacional |
| Processing__ProviderTimeoutSeconds | Timeout finito por chamada |
| Processing__MaximumProviderCalls | Limite por execucao; zero no offline |
| ExecutionContext__OperatorId | Alias estavel do operador local |
| ExecutionContext__ContextId | Contexto de desenvolvimento configurado |
| ExecutionContext__IdentityAssurance | unverified_local_configuration nesta entrega |

Em T008, copiar identificacao do host para a execucao antes de aceitar arquivos.
local-operator/local-development sao aliases do perfil inicial; configurar o operador
antes da primeira submissao funcional. Nao sao identidade autenticada.
Nao confiar identidade do LLM, do arquivo ou de headers livres do cliente.
Login, perfis e autorizacao continuam adiados; nao expor em ambiente compartilhado.

Fornecer segredos por variaveis de ambiente/processo ou mecanismo externo.
Os ambientes personalizados nao dependem do carregamento automatico de User Secrets.
infra/.env contem somente credenciais dos containers e nao e carregado pelas APIs.
Nunca colar chaves em appsettings, logs, comandos versionados ou documentacao.

O tamanho maximo de arquivo permanece indefinido; nenhum limite numerico de produto
foi adicionado. Limites efetivos de host/disco/deadline serao medidos nas fases futuras.
