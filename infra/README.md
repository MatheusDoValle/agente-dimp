# Infraestrutura local

Somente PostgreSQL 17/pgvector e MongoDB 8.0 isolados para desenvolvimento.
MongoDB Atlas continua sendo o destino gerenciado da memoria; nenhum cluster Atlas
ou recurso remoto e criado por estes scripts. O PostgreSQL e exclusivo deste projeto.

## Preparar

No PowerShell, na raiz C:/Infis/CursoIa/AgenteDIMP:

~~~powershell
./infra/New-LocalEnvironment.ps1
docker compose --env-file ./infra/.env -f ./infra/compose.yaml config --quiet
docker compose --env-file ./infra/.env -f ./infra/compose.yaml up -d --wait
docker compose --env-file ./infra/.env -f ./infra/compose.yaml ps
~~~

O gerador nao sobrescreve .env existente e nao exibe senhas.
Nao compartilhar saida de docker compose config sem --quiet: ela contem segredos.
As portas publicadas aceitam somente loopback: PostgreSQL 127.0.0.1:55432 e
MongoDB 127.0.0.1:57017. Os perfis offline das APIs nao precisam desses containers.

## Propriedade

- banco agente: owner/login agente_app, senha AGENT_DB_PASSWORD;
- banco rag: owner/login rag_app, senha RAG_DB_PASSWORD, extensao vector;
- CONNECT revogado de PUBLIC nos dois bancos; cada papel recebe somente seu banco;
- MongoDB agente_memory: usuario agente_memory com readWrite apenas nesse database;
- bootstrap/local_admin apenas inicializam o ambiente; nao usar nas APIs.

A inicializacao roda somente em volume novo. Nao aplica migrations nem cria tabelas
de negocio. Alterar .env depois nao altera as senhas ja gravadas no volume.
Nao remover volumes para corrigir isso sem antes avaliar os dados existentes.
`docker compose --env-file ./infra/.env -f ./infra/compose.yaml stop` para os servicos
sem apagar dados. Nao executar down -v automaticamente.

## Imagens e reproducibilidade

Digests de manifest multi-arquitetura consultados no Docker Registry oficial em
2026-10-29 e fixados em compose.yaml:

- pgvector/pgvector:pg17 @ sha256:ac08538c6f8b9904c33c8224c5e5706dbe760aca29db1d096972b4052c22a75d
- mongo:8.0 @ sha256:b9924c330a8bbc5800fe9c69f04a84e221eca7b28fd049a0354147d9908fa142

O digest e a identidade imutavel; a tag legivel nao autoriza atualizacao automatica.
Confirmar versao do servidor e da extensao no primeiro bootstrap executado.
Fontes: [pgvector Docker](https://github.com/pgvector/pgvector#docker) e
[MongoDB Official Image](https://hub.docker.com/_/mongo).

## Atlas

Nas fases de integracao, fornecer ConnectionStrings__MongoDb (TLS) e
MongoDb__DatabaseName por configuracao externa. Usar credenciais de aplicacao e
database separado por ambiente. Nao abrir firewall, criar cluster nem liberar IP
como efeito da inicializacao local. Autorizacao de usuarios do produto continua
adiada em EX-001; credenciais de banco nao implementam essa autorizacao.

## Testes da fundacao (Fase 2)

Com containers saudaveis: ./infra/Test-Foundation.ps1 -All -NoBuild. O script configura credenciais somente no processo filho e aplica migrations explicitamente aos bancos locais dedicados. Confirmados PostgreSQL 17.11/pgvector 0.8.7 e bloqueio cruzado dos papeis. Nao executa drop/reset nem conecta a Atlas; ver docs/validation/phase2-foundation.md.
