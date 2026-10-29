# Dependencias da Fase 1

Data: 2026-10-29. SDK fixado: 10.0.401. Target: net10.0.
Dois projetos de aplicacao e tres projetos de teste em AgenteDIMP.slnx.
Versoes diretas centralizadas em Directory.Packages.props; cinco packages.lock.json
registram o grafo resolvido completo. NuGet.Config usa somente nuget.org.

| Pacote | Versao fixada |
| --- | --- |
| Microsoft.EntityFrameworkCore | 10.0.12 |
| Microsoft.EntityFrameworkCore.Design | 10.0.12 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 |
| Npgsql | 10.0.3 |
| Pgvector | 0.3.2 |
| Pgvector.EntityFrameworkCore | 0.3.0 |
| Microsoft.Data.Sqlite | 10.0.12 |
| MongoDB.Driver | 3.13.0 |
| ModelContextProtocol | 2.3.0 |
| ModelContextProtocol.AspNetCore | 2.3.0 |
| Microsoft.SemanticKernel.Connectors.OpenAI | 1.81.0 |
| Microsoft.SemanticKernel.Core | 1.81.0 |
| OpenAI | 2.14.0 |
| PdfPig | 0.1.16 |
| OpenTelemetry.Extensions.Hosting | 1.20.1 |
| OpenTelemetry.Instrumentation.AspNetCore | 1.20.1 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 |
| Microsoft.NET.Test.Sdk | 18.11.0 |
| xunit | 2.9.3 |
| xunit.runner.visualstudio | 4.0.1 |
| Testcontainers.PostgreSql | 4.16.0 |
| Testcontainers.MongoDb | 4.16.0 |

## Escolha e evidencia

Consultados os indices oficiais NuGet v3 em 2026-10-29:
https://api.nuget.org/v3-flatcontainer/{package-id-em-minusculas}/index.json.
Pacotes Microsoft da plataforma fixados em 10.0.12 para acompanhar o runtime
Microsoft.NETCore.App/Microsoft.AspNetCore.App 10.0.12 instalado; o feed ja disponibiliza
10.0.14, mas esta fase nao atualiza SDK/runtime da maquina.

- dotnet restore AgenteDIMP.slnx --use-lock-file: aprovado nos cinco projetos.
- dotnet build AgenteDIMP.slnx --no-restore: aprovado, zero avisos/erros.
- dotnet restore AgenteDIMP.slnx --locked-mode: aprovado, sem alterar o grafo.
- dotnet test --no-build: oito testes offline aprovados, inclusive dois hosts ASP.NET.

Nenhuma incompatibilidade de restore/compilacao observada.
Esses resultados nao comprovam interacao real entre SK/modelo/protocolo MCP,
operacao EF/pgvector em servidor ou extracao de tabelas do PDF; os gates das fases
seguintes, especialmente T012/T082/T094/T105, permanecem obrigatorios.
Pacotes destinados a essas fases ja estao referenciados, sem ativar seus conectores.

Fontes: [NuGet CPM](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management),
[restore e locked mode](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-restore),
[SK OpenAI](https://www.nuget.org/packages/Microsoft.SemanticKernel.Connectors.OpenAI/1.81.0),
[MCP ASP.NET](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/2.3.0),
[provider Npgsql](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.3),
[pgvector EF](https://www.nuget.org/packages/Pgvector.EntityFrameworkCore/0.3.0).
