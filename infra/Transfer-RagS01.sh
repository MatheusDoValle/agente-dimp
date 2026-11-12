#!/usr/bin/env bash
set -euo pipefail

# Run in the local PostgreSQL container with the managed database connection in PG*.
# Only the public S01 corpus and its semantic publication markers cross databases.
: "${PGHOST:?Informe o host PostgreSQL gerenciado}"
: "${PGDATABASE:?Informe o banco PostgreSQL gerenciado}"
: "${PGUSER:?Informe o usuário administrativo do banco gerenciado}"
: "${PGPASSWORD:?Informe a credencial administrativa do banco gerenciado}"
if [[ "$PGHOST" != *.neon.tech || "${PGSSLMODE:-}" != verify-full ]]; then
  echo "Use o endpoint Neon com TLS verify-full." >&2
  exit 1
fi
umask 077
workspace=$(mktemp -d)
trap 'rm -rf -- "$workspace"' EXIT
cd "$workspace"

source_db=(env PGCHANNELBINDING=disable psql -X -q -v ON_ERROR_STOP=1 -h /var/run/postgresql -U bootstrap -d rag)
revision='fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a'
profile='text-embedding-3-small-1536'
job='0d654e51-d1f8-4184-ae3e-6d5e6968762f'

"${source_db[@]}" > /dev/null <<SQL
BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
\copy (SELECT d.* FROM rag.documents d JOIN rag.document_revisions r ON r."DocumentId"=d."Id" WHERE r."RevisionSha256"='$revision') TO 'documents.csv' WITH (FORMAT csv)
\copy (SELECT "Id","DocumentId","SourceUrl","RevisionSha256","LayoutVersion","RetrievedAt","ByteSize","PageCount","SourceStatus",NULL::text FROM rag.document_revisions WHERE "RevisionSha256"='$revision') TO 'document_revisions.csv' WITH (FORMAT csv)
\copy (SELECT * FROM rag.embedding_profiles WHERE "ProfileId"='$profile') TO 'embedding_profiles.csv' WITH (FORMAT csv)
\copy (SELECT c.* FROM rag.knowledge_chunks c JOIN rag.document_revisions r ON r."Id"=c."RevisionId" WHERE r."RevisionSha256"='$revision') TO 'knowledge_chunks.csv' WITH (FORMAT csv)
\copy (SELECT v.* FROM rag.chunk_vectors v JOIN rag.knowledge_chunks c ON c."Id"=v."ChunkId" JOIN rag.document_revisions r ON r."Id"=c."RevisionId" WHERE r."RevisionSha256"='$revision' AND v."EmbeddingProfileId"='$profile') TO 'chunk_vectors.csv' WITH (FORMAT csv)
\copy (SELECT * FROM rag.ingestion_jobs WHERE "IngestionId"='$job' AND "RevisionSha256"='$revision' AND "EmbeddingProfileId"='$profile' AND "Status"='concluida') TO 'ingestion_jobs.csv' WITH (FORMAT csv)
\copy (SELECT "IngestionJobId","ChunkHash","EmbeddingProfileId",NULL::uuid,"State","UpdatedAt" FROM rag.ingestion_chunk_checkpoints WHERE "IngestionJobId"='$job' AND "EmbeddingProfileId"='$profile' AND "State"='persisted') TO 'ingestion_chunk_checkpoints.csv' WITH (FORMAT csv)
COMMIT;
SQL

{
  echo 'BEGIN;'
  for table in documents document_revisions embedding_profiles knowledge_chunks chunk_vectors ingestion_jobs ingestion_chunk_checkpoints; do
    printf 'CREATE TEMP TABLE staged_%s (LIKE rag.%s) ON COMMIT DROP;\n' "$table" "$table"
    printf "\\\\copy staged_%s FROM '%s.csv' WITH (FORMAT csv)\n" "$table" "$table"
    printf 'INSERT INTO rag.%s SELECT * FROM staged_%s ON CONFLICT DO NOTHING;\n' "$table" "$table"
  done
  cat <<SQL
DO \$$
BEGIN
  IF (SELECT count(*) FROM staged_documents) <> 1
     OR (SELECT count(*) FROM staged_document_revisions) <> 1
     OR (SELECT count(*) FROM staged_embedding_profiles) <> 1
     OR (SELECT count(*) FROM staged_ingestion_jobs) <> 1
     OR (SELECT count(*) FROM staged_knowledge_chunks) <> 492
     OR (SELECT count(*) FROM staged_chunk_vectors) <> 492
     OR (SELECT count(*) FROM staged_ingestion_chunk_checkpoints) <> 492
     OR (SELECT count(*) FROM rag.chunk_vectors v
         JOIN rag.knowledge_chunks c ON c."Id"=v."ChunkId"
         JOIN rag.document_revisions r ON r."Id"=c."RevisionId"
         JOIN rag.ingestion_chunk_checkpoints k ON k."ChunkHash"=c."ChunkHash" AND k."EmbeddingProfileId"=v."EmbeddingProfileId"
         JOIN rag.ingestion_jobs j ON j."IngestionId"=k."IngestionJobId" AND j."RevisionSha256"=r."RevisionSha256"
         WHERE r."RevisionSha256"='$revision' AND v."EmbeddingProfileId"='$profile'
           AND j."IngestionId"='$job' AND j."Status"='concluida' AND k."State"='persisted') <> 492
  THEN
    RAISE EXCEPTION 'A revisão S01 ou a publicação semântica está incompleta';
  END IF;
END;
\$$;
COMMIT;
SQL
} | psql -X -q -v ON_ERROR_STOP=1

echo 'S01: 492 trechos e vetores publicados, sem transferir ledger ou caminhos locais.'
