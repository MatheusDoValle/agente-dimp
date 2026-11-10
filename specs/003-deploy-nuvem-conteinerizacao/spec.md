# Especificação da Funcionalidade: Conteinerização, Deploy em Nuvem e Vercel

**Feature:** `003-deploy-nuvem-conteinerizacao`  
**Data:** 2026-11-09  
**Status:** Aprovado  
**Padrão:** Docker Multi-Stage .NET 10 | Vercel Containers | SOFIA Network Docker Compose | Bancos Gerenciados (PgVector e MongoDB Atlas)  

---

## 1. Contexto e Objetivo

O **Agente Fiscal DIMP INFIS** foi validado localmente e integrado ao ecossistema SOFIA via protocolo A2A v0.3.x e CORS. Para permitir a operação em ambientes compartilhados e de produção sem acoplamento a máquinas locais, o serviço precisa ser conteinerizado e preparado para implantação na nuvem (Vercel, Docker Compose da rede de agentes da INFIS e contêineres OCI padronizados).

Esta especificação define os requisitos de conteinerização para .NET 10, suporte à variável de ambiente `PORT` exigida por plataformas de nuvem/serverless, integração segura com instâncias gerenciadas externas de **PostgreSQL com PgVector** e **MongoDB Atlas**, além de orquestração na rede Docker `agentes-network` da SOFIA.

---

## 2. Requisitos Funcionais (RF)

- **RF-DEP-01 (Dockerfile Raiz e Multi-Stage):** A raiz do projeto DEVE conter um `Dockerfile` otimizado para .NET 10 (`mcr.microsoft.com/dotnet/sdk:10.0` para build e `mcr.microsoft.com/dotnet/aspnet:10.0` para runtime), realizando restauração locked/compatível e build Release de `src/Api.Agente/Api.Agente.csproj`.
- **RF-DEP-02 (Dockerfile Vercel):** DEVE ser disponibilizado o arquivo `Dockerfile.vercel` conforme as diretrizes da plataforma Vercel e da skill `vercel-dotnet-pgvector-atlas`.
- **RF-DEP-03 (Alocação Dinâmica de Porta - Variável `PORT`):** As APIs (`Api.Agente` e `Api.RAG`) DEVEM inspecionar a variável de ambiente `PORT` na inicialização e, quando definida, realizar o bind em `http://0.0.0.0:{PORT}`. Caso não esteja definida, devem manter as URLs configuradas (fallback para `http://0.0.0.0:8080` ou `appsettings`).
- **RF-DEP-04 (Endpoints de Health Check):** As APIs DEVEM disponibilizar tanto `GET /health/live` quanto `GET /health` retornando status HTTP `200 OK` e metadados operacionais não sensíveis para verificação de liveness/readiness por balanceadores de carga e orquestradores de contêineres.
- **RF-DEP-05 (Compose da Rede de Agentes SOFIA):** DEVE ser disponibilizado o arquivo `docker-compose.agentes.yaml` para permitir a execução do `agente-dimp` na rede externa `agentes-network` compartilhada com a SOFIA, expondo a porta de host configurável e conectando-se ao DNS interno `http://agente-dimp:8080`.
- **RF-DEP-06 (Configuração Vercel):** DEVE ser disponibilizado o arquivo `vercel.json` configurando o projeto para o runtime de contêiner sem conflito com frameworks de frontend.
- **RF-DEP-07 (Suporte a Bancos Externos Gerenciados):** A documentação e configuração DEVEM orientar a conexão TLS obrigatória (`SSL Mode=Require`) para PostgreSQL com PgVector gerenciado (ex.: Supabase, Neon) e connection string autenticada para MongoDB Atlas, sem embutir credenciais no código.

---

## 3. Requisitos Não Funcionais (RNF)

- **RNF-01 (Segurança de Imagem e Segredos):** O `.dockerignore` DEVE garantir que nenhum arquivo `.env`, credencial, segredo, banco SQLite temporário ou artefato local de build seja incluído na imagem Docker.
- **RNF-02 (Tamanho Reduzido da Imagem):** Utilizar imagem base `aspnet:10.0` mínima (Alpine ou Debian chiseled/slim), sem ferramentas de compilação ou depuração desnecessárias na imagem final de runtime.
- **RNF-03 (Imutabilidade de Contêiner):** Nenhum dado de negócio durável deve ser gravado no sistema de arquivos do contêiner; temporários são efêmeros e dados duráveis residem nos bancos externos (PostgreSQL/PgVector e MongoDB Atlas).

---

## 4. Critérios de Aceite (Given / When / Then)

1. **Cenário 1: Bind dinâmico de porta via variável `PORT`**
   - **Given** que a variável de ambiente `PORT=8099` está definida.
   - **When** a `Api.Agente` inicializa.
   - **Then** a aplicação escuta requisições na porta 8099 em `0.0.0.0`.

2. **Cenário 2: Consulta ao Health Check**
   - **Given** o contêiner da `Api.Agente` em execução.
   - **When** um balanceador de carga ou cliente executa `GET /health`.
   - **Then** a resposta deve ser `200 OK` com `status: "alive"`.

3. **Cenário 3: Validação de Build do Dockerfile**
   - **Given** os arquivos de projeto no repositório.
   - **When** executado o build do Dockerfile em ambiente de contêiner.
   - **Then** a compilação e publicação do binário .NET 10 ocorrem sem falhas.

4. **Cenário 4: Prevenção de Vazamento no .dockerignore**
   - **Given** o arquivo `.dockerignore`.
   - **When** inspecionado contra o checklist de segurança.
   - **Then** deve excluir `.git`, `.env*`, `.vercel`, `secrets/`, temporários e arquivos `.dimp`.
