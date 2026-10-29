# Testes

Os dois projetos UnitTests e a suite IntegrationTests usam xUnit.
O bootstrap roda sem Docker, banco ou chave OpenAI. Nao valida regras DIMP, MCP,
persistencia nem qualidade de RAG. Testcontainers.PostgreSql e Testcontainers.MongoDb
estao referenciados para os cenarios reais das fases seguintes.

Comando padrao (exclui provedor pago):
`dotnet test AgenteDIMP.slnx --no-build --filter "Category!=LiveProvider"`

Todo teste futuro de provedor real deve usar [Trait("Category", "LiveProvider")],
exigir credencial externa e execucao explicita com filtro Category=LiveProvider.
Nao criar teste real ativo por padrao nem declarar chamadas reais executadas nesta fase.
