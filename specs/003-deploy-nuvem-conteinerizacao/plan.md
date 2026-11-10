# Plano de Implementação: Conteinerização, Deploy em Nuvem e Vercel

**Feature:** `003-deploy-nuvem-conteinerizacao`  
**Data:** 2026-11-09  
**Status:** Aprovado  

---

## 1. Arquitetura de Implantação

```text
                               +------------------------------------------+
                               |     Vercel Serverless / Docker OCI       |
                               |                                          |
                               |  +------------------------------------+  |
  Internet / Clientes -------->|  |      Api.Agente (ASP.NET .NET 10)  |  |
                               |  |      Bind: 0.0.0.0:${PORT:-8080}   |  |
                               |  +------------------------------------+  |
                               +------------------------------------------+
                                       | TLS                    | TLS
                                       v                        v
            +------------------------------------+    +-----------------------+
            |  PostgreSQL Gerenciado + PgVector  |    |     MongoDB Atlas     |
            |     (Neon / Supabase / AWS)        |    | (Memória Conversação) |
            +------------------------------------+    +-----------------------+
```

Em ambiente Docker local/SOFIA:
```text
  Rede Docker: agentes-network
  +-------------------------+         +-------------------------------+
  |   SOFIA (Orquestrador)  | ------> |  agente-dimp (Api.Agente)     |
  |                         |         |  http://agente-dimp:8080      |
  +-------------------------+         +-------------------------------+
```

---

## 2. Decisões Arquiteturais (ADRs)

### ADR-01: Dockerfile Multi-Stage com .NET 10
- **Contexto:** Necessidade de criar imagens de produção leves, seguras e reproduzíveis.
- **Decisão:** Usar `mcr.microsoft.com/dotnet/sdk:10.0` como base de build e `mcr.microsoft.com/dotnet/aspnet:10.0` como base de execução final.
- **Justificativa:** Reduz o tamanho da imagem, remove o SDK e compiladores da imagem final e cumpre os requisitos do Vercel e Kubernetes/Docker Compose.

### ADR-02: Binding Dinâmico de Porta (`PORT`)
- **Contexto:** Vercel e plataformas serverless/PaaS injetam dinamicamente uma porta na variável de ambiente `PORT`.
- **Decisão:** Inspecionar `PORT` no `Program.cs` e invocar `builder.WebHost.UseUrls($"http://0.0.0.0:{port}")` se presente.
- **Justificativa:** Garante compatibilidade imediata sem requerer alteração de código entre desenvolvimento local e deploy em nuvem.

### ADR-03: Separação de Dados Duráveis de Armazenamento Local
- **Contexto:** Contêineres de runtime em serverless são efêmeros. O sistema de arquivos local é reiniciado a cada ciclo.
- **Decisão:** Manter todos os estados persistentes nos bancos de dados gerenciados externos com TLS (`AgentDb` e `MongoDb:DatabaseName`), mantendo no contêiner apenas buffers temporários que expiram e são limpos pelo `ArtifactRecoveryWorker`.
- **Justificativa:** Preserva a integridade e conformidade dos relatórios e do ledger de consumo independentemente de reinícios de instâncias.

---

## 3. Arquivos a Criar e Modificar

1. `Dockerfile`: Build e empacotamento da `Api.Agente`.
2. `Dockerfile.vercel`: Compatibilidade direta com o pipeline Vercel Containers.
3. `docker-compose.agentes.yaml`: Configuração para execução conjunta na rede `agentes-network` da SOFIA.
4. `vercel.json`: Arquivo de metadados para implantação sem framework.
5. `.dockerignore`: Ajuste de filtros para incluir `.vercel` e permitir o processamento dos Dockerfiles.
6. `src/Api.Agente/Program.cs` e `src/Api.RAG/Program.cs`: Suporte à variável `PORT` na inicialização e rota `/health`.
7. `src/Api.Agente/Endpoints/BootstrapEndpoints.cs` e `src/Api.RAG/Endpoints/BootstrapEndpoints.cs`: Rota adicional `/health`.
8. `docs/deployment.md`: Guia de implantação completo com variáveis de ambiente e checagens pós-deploy.
9. `.specify/memory/constitution.md`: Resolução formal de **D13**.
