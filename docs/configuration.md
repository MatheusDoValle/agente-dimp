# Configuracao de desenvolvimento

Fases 1/2 concluidas. A fundacao implementa contratos, persistencia, privacidade,
ledger e finalizacao; recebimento funcional, parser DIMP, validadores, MCP e
conectores OpenAI/MongoDB da jornada ainda dependem das fases seguintes.

## LocalDeterministic

~~~powershell
dotnet run --project ./src/Api.RAG --launch-profile LocalDeterministic
dotnet run --project ./src/Api.Agente --launch-profile LocalDeterministic
~~~

/health/live em 127.0.0.1:5081 e :5080 confirma apenas bootstrap_only.
Os doubles retornam analise sintetica e vetor SHA-256 de 1536 dimensoes,
perfil local-fake-sha256-v1. Nao ha qualidade semantica ou conformidade fiscal.
Sem chaves/banco obrigatorios para iniciar o host; repositorios exigem configuracao
ao serem resolvidos, sem conexao automatica por health check.

T014 valida no startup deadline/timeout positivos finitos, timeout dentro do deadline,
modelos tecnicos e budgets de tokens/chamadas nao negativos (positivos no modo externo),
1..3 tentativas transitórias totais e 0..1 reparo semantico. O perfil offline usa
120s/30s, zero chamadas e zero tokens. Isso nao e SLA nem limite de tamanho de arquivo.
O loop real de retentativas pertence a T110; nao foi ativado por definir estas opcoes.

## Contexto e temporarios

ExecutionContext__OperatorId e ExecutionContext__ContextId sao aliases tecnicos do
host, sem e-mail/CPF/CNPJ. O perfil local usa local-operator/local-development;
configurar o operador antes da primeira submissao funcional.
IdentityAssurance=unverified_local_configuration e obrigatorio. UUIDs/correlacao e
datas UTC sao gerados pelo sistema; o snapshot copia somente campos permitidos,
sem connection strings ou API keys. Login/autorizacao continuam adiados em EX-001.

TemporaryStorage__RootPath e opcional; padrao .local/temporary no content root
da Api.Agente, fora de wwwroot. Raiz local dedicada, diretorios/nomes por UUID,
ACL do usuario do processo no Windows e modo 0700 no Linux (host Linux ainda nao validado).
Originais, indices/sidecars, manifesto e mapa ficam nessa area privada e nao integram
backups da aplicacao, logs ou Git. Nao incluir a area em backups externos.

Nomes e outros identificadores conhecidos devem ser registrados pelo parser no
sanitizer antes de gerar descricoes/trechos. CPF/CNPJ numericos tambem sao detectados
no texto. Isso e mascaramento contextual, nao identificacao universal de pessoas
nem garantia de anonimizacao integral. Mapa reversivel somente no temporario ativo.
A finalizacao fecha novas escritas, aguarda leitores e exige exclusao confirmada.
O marcador privado _closed guarda somente UUID/data de solicitacao da limpeza;
sua manutencao sera integrada a tombstones/coletor em T113/T115.

## LocalReal e memoria

LocalReal esta definido, mas a inicializacao recusa o modo ate US3. Budgets reais
permanecem nulos; nao ativar dependencias sem perfil finito avaliado.
Memory__Enabled=false nesta fase. A porta IExecutionMemoryCleanup retorna confirmacao
apenas porque nao existe store ativo. Ativar memoria exige substituir o adaptador
por exclusao MongoDB confirmada em T098/T103; o startup recusa habilitacao prematura.
Atlas permanece o destino gerenciado. Nenhum cluster Atlas foi criado.

## Nomes externos

| Chave | Uso |
| --- | --- |
| ConnectionStrings__AgentDb | Banco agente, usuario agente_app; Api.Agente |
| ConnectionStrings__KnowledgeDb | Banco rag, usuario rag_app; Api.RAG |
| ConnectionStrings__MongoDb | Futuro contexto MongoDB local/Atlas TLS |
| MongoDb__DatabaseName | Database de memoria por ambiente |
| Rag__McpEndpoint | Futuro endpoint MCP, local :5081/mcp |
| OpenAI__ApiKey | Credencial externa, nunca em arquivo versionado |
| Processing__Model | Identidade tecnica do modelo por servico |
| Processing__GlobalDeadlineSeconds | Deadline global finito |
| Processing__ProviderTimeoutSeconds | Timeout finito por chamada |
| Processing__MaximumProviderCalls | Budget total; zero no offline |
| Processing__TokenBudget | Budget de tokens; zero no offline |
| Processing__MaximumTransientAttempts | 1..3 |
| Processing__MaximumSemanticRepairs | 0..1 |

infra/.env atende somente aos containers; nao e carregado pelas APIs.
Os ambientes personalizados nao dependem do carregamento automatico de User Secrets.
Fornecer segredos por ambiente/processo ou mecanismo externo. Nao colar valores em logs.

## Migrations e telemetria

Ferramenta EF fixada no manifesto .config/dotnet-tools.json.
AgentDbContext usa schema agente e KnowledgeDbContext schema rag, em bancos/papeis
distintos. Aplicacao nao executa migrations ao iniciar. O teste local e a ferramenta
EF fazem isso explicitamente; usar credenciais externas de cada banco.

Telemetria aceita somente categorias AgenteDIMP.Safe.*, com eventos fixos, IDs
gerados pelo host e status. O source de tracing dedicado nao coleta URL/query,
headers, corpos, prompts, nomes de arquivo ou excecoes brutas; nenhum exporter
externo foi configurado. Erros retornam Problem versionado com correlacao do host.
