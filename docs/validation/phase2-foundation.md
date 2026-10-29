# Fase 2 - Fundacao

Data: 2026-10-29. Projeto: C:/Infis/CursoIa/AgenteDIMP.
T007-T020 concluidas. Total acumulado: 20/123 tarefas; 103 pendentes.
Esta evidencia nao aprova regras fiscais nem a jornada completa.

| Tarefa | Entrega e verificacao |
| --- | --- |
| T007 | DTOs REST/analitico com required, nulabilidade, enums de string, constantes, ranges e nomes JSON; testes de round-trip, campos desconhecidos, URI e paginas. |
| T008 | TimeProvider, UUIDs, correlacao e aliases locais; snapshot com lista de campos permitidos, sem segredos. |
| T009 | Execution com estado/etapa/veredicto separados e endedAt anulavel; encerramento reservado ao finalizador. |
| T010 | Avaliacoes/achados/localizacao e contagem por regra/ocorrencia; unicidade, agregacao e IDs de ocorrencia sem conteudo do arquivo. |
| T011 | Report unico por execucao, EvidenceSnapshot com proveniencia e texto mascarado; expiresAt=endedAt+30 dias quando existe relatorio. |
| T012 | Contextos EF, factories e migrations explicitas aplicadas aos dois bancos; papeis recusam conexao ao banco do outro (SQLSTATE 42501). |
| T013 | Ledger PostgreSQL por tentativa em cada servico, antes do delegate; consumo/custo ausentes ficam null e falha pre-chamada bloqueia o provider. |
| T014 | Opcoes validadas no startup: modelos, budgets/deadlines finitos e limites de 3 tentativas/1 reparo. Sem teto numerico de arquivo. |
| T015 | Cinco testes de privacidade escritos antes do sanitizer: 5 falhas esperadas por NotImplementedException, registradas em TRX local. |
| T016 | Os cinco casos passaram apos implementar tokens por execucao, mapa privado recuperavel e guard de persistencia. |
| T017 | Copia em streaming com SHA-256, manifestos, nomes gerados e ACL privada; sem aceitar caminho de cliente. Remocao cobre SQLite/sidecars e mapa. |
| T018 | Limpeza persistida antes do encerramento; leitor aberto ou memoria nao confirmada mantem status em processamento/limpeza, sem endedAt. |
| T019 | Testes reais de payload bruto, evidencias de outra execucao, relatorio duplicado, quatro estados terminais, recuperacao de limpeza e expiracao. |
| T020 | Problem generico com UUID do host; logs e traces dedicados sem query, corpo, identificadores originais ou detalhes de excecao. |

## Resultados finais

- Restore --locked-mode aprovado.
- Build aprovado: 0 avisos e 0 erros.
- 51 testes aprovados, 0 falhos, 0 ignorados.
- Api.Agente.UnitTests: 23; Api.RAG.UnitTests: 2; IntegrationTests: 26.
- 15 dos casos de integracao exercitam PostgreSQL real; demais verificam contratos/HTTP.
- Containers do projeto saudaveis: PostgreSQL 17.11, pgvector 0.8.7 e MongoDB local.
- vector instalado apenas em rag; bancos/papeis dedicados conferidos.
- Migrations aplicadas sem reset/drop, e modelos sem alteracoes pendentes.
- Sem chamadas reais OpenAI, precos inventados ou recursos remotos provisionados.
- Checklist requirements.md: 16/16; sem alterar arquivo ou marcadores.
- Antes/depois: sem extensions.yml, portanto sem hooks registrados.

~~~powershell
dotnet tool restore
dotnet restore ./AgenteDIMP.slnx --locked-mode
dotnet build ./AgenteDIMP.slnx --no-restore
docker compose --env-file ./infra/.env -f ./infra/compose.yaml up -d --wait
./infra/Test-Foundation.ps1 -All -NoBuild
~~~

TRX completos em artifacts/test-results (nao versionados).
O caso vermelho T015 usa prefixo phase2-red-privacy; os verdes usam phase2-green-privacy
e phase2. Apenas falhas esperadas do TDD estao nesse historico; o resultado final e verde.

## Ajustes e limites

EF Core Relational/Abstractions foram fixados em 10.0.12 para resolver conflito
MSB3277 que apareceu ao expor os tipos EF nos testes. Locks atualizados; futuras
regressoes MSB3277 falham o build. Nenhum aviso foi suprimido para aprovar testes.
A ferramenta local dotnet-ef tambem usa 10.0.12.

O comentario transitorio de sincronizacao no inicio da constitution.md foi removido,
conforme sua instrucao de exclusao antes de commit. Principios e versao 1.0.1 preservados.

O encerramento exige a porta de limpeza de memoria; o adapter MongoDB real continua
em T098/T103. Os testes usam confirmacao/recusa controladas, sem alegar exclusao
real de conversation_contexts. O MongoDB local esta saudavel, mas a API ainda nao o usa.
Leases/fencing da fila duravel, parser/validadores e recebimento funcional sao fases
seguintes; os endpoints de negocio continuam ausentes e LocalReal desabilitado.
A expiracao logica/coletor de 30 dias, tombstones e manutencao dos marcadores privados
serao integrados em T107/T113/T115; esta fase verifica a data calculada e a limpeza inicial.
Linux host, RAG/MCP, capacidade real do provedor e qualidade semantica nao foram aceitos
por estes testes. SF-001/002/003 continuam bloqueando cobertura fiscal completa.
