# Agente DIMP

Duas APIs ASP.NET Core/.NET 10: Api.Agente para orquestracao e Api.RAG para conhecimento.
Memoria prevista em MongoDB Atlas; acervo e vetores em PostgreSQL/pgvector.
Repositorio: https://github.com/MatheusDoValle/agente-dimp

Fases 1 e 2 concluidas: infraestrutura, contratos, modelos, persistencia,
mascaramento por execucao, ledger e finalizacao segura. O parser DIMP, os validadores
fiscais, MCP e a analise real de IA pertencem as proximas fases.
Nao ha endpoint funcional de analise ou chamada paga ativo.

## Compilar e testar sem banco

~~~powershell
dotnet restore ./AgenteDIMP.slnx --locked-mode
dotnet tool restore
dotnet build ./AgenteDIMP.slnx --no-restore
dotnet test ./AgenteDIMP.slnx --no-build --filter 'Category!=Database&Category!=LiveProvider'
~~~

## Executar todos os testes locais

~~~powershell
if (-not (Test-Path ./infra/.env)) { ./infra/New-LocalEnvironment.ps1 }
docker compose --env-file ./infra/.env -f ./infra/compose.yaml up -d --wait
./infra/Test-Foundation.ps1 -All -NoBuild
~~~

A suite usa os bancos/papeis locais dedicados agente/rag, aplica migrations
explicitamente e nao apaga bancos/volumes. O script configura credenciais somente
no processo de teste, sem exibi-las. Testes Database exigem os containers;
LiveProvider continua separado e nenhuma chamada OpenAI real foi executada.

## Iniciar offline

Em dois terminais PowerShell na raiz:

~~~powershell
dotnet run --project ./src/Api.RAG --launch-profile LocalDeterministic
dotnet run --project ./src/Api.Agente --launch-profile LocalDeterministic
~~~

Consultar /health/live em 127.0.0.1:5081 e 127.0.0.1:5080.
O bootstrap HTTP continua iniciando sem banco/chave; os repositorios so conectam
quando usados. LocalReal permanece reservado ate a implementacao dos conectores.

Ver [configuracao](docs/configuration.md), [infraestrutura](infra/README.md),
[dependencias](docs/validation/dependencies.md),
[evidencia da Fase 2](docs/validation/phase2-foundation.md) e
[tarefas](specs/001-validar-arquivo-dimp/tasks.md).
