#!/bin/bash
set -euo pipefail
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  --set=agent_password="$AGENT_DB_PASSWORD" --set=rag_password="$RAG_DB_PASSWORD" <<'SQL'
CREATE ROLE agente_app LOGIN PASSWORD :'agent_password';
CREATE ROLE rag_app LOGIN PASSWORD :'rag_password';
CREATE DATABASE agente OWNER agente_app;
CREATE DATABASE rag OWNER rag_app;
REVOKE ALL ON DATABASE agente FROM PUBLIC;
REVOKE ALL ON DATABASE rag FROM PUBLIC;
GRANT CONNECT, TEMPORARY ON DATABASE agente TO agente_app;
GRANT CONNECT, TEMPORARY ON DATABASE rag TO rag_app;
SQL
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname agente <<'SQL'
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO agente_app;
SQL
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname rag <<'SQL'
CREATE EXTENSION IF NOT EXISTS vector;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO rag_app;
SQL
