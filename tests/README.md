# Testes

xUnit em Api.Agente.UnitTests, Api.RAG.UnitTests e AgenteDIMP.IntegrationTests.
A Fase 2 tem testes de contratos, contexto, budgets, mascaramento, ledger,
isolamento de bancos, finalizacao e telemetria. Os dados sao sinteticos.

Sem banco/provedor:
`dotnet test AgenteDIMP.slnx --no-build --filter "Category!=Database&Category!=LiveProvider"`

Suite completa local, com os containers do infra/compose.yaml saudaveis:
`./infra/Test-Foundation.ps1 -All -NoBuild`

Somente testes PostgreSQL reais:
`./infra/Test-Foundation.ps1 -NoBuild`

O script fornece DIMP_TEST_MODE=compose-local e connections no processo filho.
A fixture recusa host/porta/banco/papel diferentes dos locais dedicados:
127.0.0.1:55432, agente/agente_app e rag/rag_app.
Migrations sao aplicadas explicitamente, sem drop/reset/EnsureCreated.
Cada teste cria UUIDs proprios; dados sinteticos permanecem nos bancos locais.
Nao executar contra dados de clientes. Os diretorios temporarios de teste sao
criados com identidade propria e removidos no teardown.

Testcontainers.PostgreSql/MongoDb continuam disponiveis para cenarios futuros.
Nesta fase, a prova real usa os containers isolados do Compose. A limpeza de
memoria testa o contrato de confirmacao com double; o adaptador MongoDB e T098/T099.

Todo teste futuro de provedor real usa [Trait("Category", "LiveProvider")],
credencial externa e execucao explicita. Nao depende de LLM variavel para asserts.
TRX em artifacts/test-results; nao versionar logs ou credenciais.
