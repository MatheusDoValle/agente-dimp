# Agente DIMP

Projeto em C:/Infis/CursoIa/AgenteDIMP. Duas APIs ASP.NET Core/.NET 10:
Api.Agente para orquestracao e Api.RAG para conhecimento.
Memoria em MongoDB Atlas; acervo e vetores em PostgreSQL/pgvector.

Fase 1: infraestrutura e bootstrap offline. O fluxo DIMP e suas regras ainda
nao foram implementados. Nenhum processamento fiscal ou chamada paga e executado.

## Verificar

~~~powershell
dotnet restore ./AgenteDIMP.slnx --locked-mode
dotnet build ./AgenteDIMP.slnx --no-restore
dotnet test ./AgenteDIMP.slnx --no-build --filter 'Category!=LiveProvider'
~~~

SDK fixado em global.json; dependencias centralizadas e arquivos packages.lock.json
versionados. Ver [dependencias](docs/validation/dependencies.md),
[configuracao](docs/configuration.md), [infraestrutura](infra/README.md) e
[tarefas](specs/001-validar-arquivo-dimp/tasks.md).

## Iniciar offline

Em dois terminais PowerShell na raiz:

~~~powershell
dotnet run --project ./src/Api.RAG --launch-profile LocalDeterministic
dotnet run --project ./src/Api.Agente --launch-profile LocalDeterministic
~~~

Consultar /health/live nas portas 5081 e 5080. Docker e credenciais nao sao
necessarios para o bootstrap. LocalReal esta reservado e e recusado nesta fase.
