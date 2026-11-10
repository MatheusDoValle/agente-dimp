# Tarefas de Implementação: Conteinerização, Deploy em Nuvem e Vercel

**Feature:** `003-deploy-nuvem-conteinerizacao`  
**Status:** Em Execução  

---

## Fase 1: Ajuste de Inicialização e Health Checks

- [x] **T01-1**: Adicionar leitura da variável de ambiente `PORT` e bind dinâmico em `src/Api.Agente/Program.cs` e `src/Api.RAG/Program.cs`.
- [x] **T01-2**: Adicionar endpoint `GET /health` em `src/Api.Agente/Endpoints/BootstrapEndpoints.cs` e `src/Api.RAG/Endpoints/BootstrapEndpoints.cs`.
- [x] **T01-3**: Criar testes unitários para validar os endpoints `/health` e `/health/live`.

---

## Fase 2: Configuração Docker e Multi-Stage Builds

- [x] **T02-1**: Atualizar `.dockerignore` adicionando `.vercel`, `node_modules` e mantendo exclusão de segredos e temporários.
- [x] **T02-2**: Criar `Dockerfile` multi-stage para .NET 10 na raiz do repositório para compilar e empacotar `Api.Agente`.
- [x] **T02-3**: Criar `Dockerfile.vercel` específico para o pipeline de contêineres do Vercel conforme a skill `vercel-dotnet-pgvector-atlas`.
- [x] **T02-4**: Criar `docker-compose.agentes.yaml` para orquestração com o ecossistema SOFIA na rede `agentes-network`.
- [x] **T02-5**: Criar `vercel.json` na raiz do projeto.

---

## Fase 3: Documentação de Deploy e Resolução Constitucional

- [x] **T03-1**: Atualizar `.specify/memory/constitution.md` marcando a decisão **D13** como resolvida.
- [x] **T03-2**: Criar guia completo de implantação em `docs/deployment.md` cobrindo Vercel, Neon/Supabase (PgVector), MongoDB Atlas, variáveis de ambiente e checagens pós-deploy.
- [x] **T03-3**: Atualizar `README.md` com as seções de deploy e contêineres.

---

## Fase 4: Validação, Testes e Publicação Git

- [x] **T04-1**: Executar testes da solução para garantir 100% de aprovação.
- [x] **T04-2**: Criar commit padronizado e realizar `git push origin main`.
