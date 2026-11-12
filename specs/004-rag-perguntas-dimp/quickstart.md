# Roteiro de validação

## Resultado da Fase 1 — 2026-11-11

| Verificação | Resultado observado |
| --- | --- |
| Manual S01 | PDF local e PDF baixado do CONFAZ idênticos, com 826.393 bytes. SHA-256 `FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A`. Leiaute V10 e 58 páginas no manifesto. |
| Banco local | PostgreSQL 17 saudável em `127.0.0.1:55432`, extensão pgvector `0.8.7` e duas migrações da RAG registradas, incluindo `20261109212506_KnowledgeAcervo`. |
| Api.RAG | Publicação Release compilada com .NET 10. A API respondeu `200` em `/health/live` no perfil `LocalDeterministic`. |
| POST de ingestão | `202 Accepted` com a ingestão `a5982a3d-12e2-42d1-858d-380c343b4c32`. A repetição do POST devolveu `202` com o mesmo ID. |
| GET de status | `200 OK`, estado `recebida` e etapa `recebimento` na primeira consulta e na consulta após o reenvio. |
| MCP | `tools/list` mostrou `search_manual`. A consulta pela data da operação na revisão S01, com `local-fake-sha256-v1`, retornou zero trechos e `NO_EVIDENCE_FOUND`. |

A carga de teste usou apenas o manual público. O banco e a ingestão recebida ficaram disponíveis no ambiente local. Na Fase 2, o consumidor da fila deve processar esse trabalho até `concluida` e liberar os trechos da revisão para consulta.

## Resultado da Fase 2 — Ingestão local

| Verificação | Resultado observado |
| --- | --- |
| Ingestão do Manual S01 | O trabalho `a5982a3d-12e2-42d1-858d-380c343b4c32` passou por extração, segmentação e embeddings até `concluida` na etapa de publicação. Foram gravados 492 checkpoints de trechos. |
| Retomada | Uma execução foi interrompida após 120 checkpoints persistidos. Após expirar o lease daquela instância de teste, a instância seguinte continuou o mesmo trabalho. |
| Reenvio | O mesmo PDF e perfis retornaram `200 OK` com o mesmo ID após a conclusão. |
| MCP | `search_manual` retornou cinco trechos com páginas e hash de S01. `get_manual_chunk` confirmou o trecho com `DT_OP` na p. 36 por ID. |
| Testes de integração | 15 aprovados. O teste de recuperação verifica POST concorrente, invisibilidade do corpus parcial e retomada após expiração do lease sem refazer o checkpoint persistido. |

O índice usa `local-fake-sha256-v1`. A p. 36 foi confirmada por ID. A busca de teste por data da operação retornou outras páginas entre os primeiros resultados. A qualidade do ranking e dos filtros de registro será medida na Fase 3 com o perfil semântico.

## Local

1. Na raiz do projeto, siga a [preparação da Feature 001](../001-validar-arquivo-dimp/quickstart.md#preparar-e-verificar-a-solução-implementada) para subir o PostgreSQL/pgvector. Aplique as migrações de `KnowledgeDbContext` com a conexão de desenvolvimento definida em `ConnectionStrings__KnowledgeDb`.
2. Rode `Api.RAG` no perfil `LocalDeterministic` e envie o PDF pelo [roteiro de ingestão](../001-validar-arquivo-dimp/quickstart.md#preparar-o-acervo). Use `pdfpig-v1`, `chunk-layout10-section-record-v1` e `local-fake-sha256-v1`.
3. Consulte `statusUrl` até `concluida`. Reenvie o mesmo PDF e confirme o mesmo ID. Confira `search_manual` com a revisão S01 e valide a referência ao trecho.
4. Após habilitar o perfil semântico, reindexe a mesma revisão com `text-embedding-3-small-1536`. Registre os resultados das perguntas de aceite e confira que a busca não mistura perfis.
5. No Swagger local, use `POST /demo/dimp` e confira a matriz abaixo. Verifique que a consulta aberta realmente acionou o MCP.

| Caso | Evidência esperada |
| --- | --- |
| Qual registro tem a data da venda? | Catálogo, registro 1110, campo 03, p. 36. |
| Como relaciono um comprovante de pagamento ao resumo diário? | Recuperação MCP, registros 1110 e 1115 com trechos e páginas conferidos. |
| Pergunta sem respaldo no acervo | Indicação de ausência de evidência, sem citação inventada. |
| RAG indisponível | Limitação explícita na pergunta aberta; consulta direta do catálogo disponível. |
| DIMP sintética e chamada A2A | Envelope e comportamento de validação já esperados. |

## Preparar a avaliação da Fase 3

1. Guarde a chave em `OpenAI:ApiKey` no User Secrets do projeto `src/Api.RAG/Api.RAG.csproj`. A API também aceita `OPENAI_API_KEY` no ambiente.
2. Com o PostgreSQL local disponível e `ConnectionStrings__KnowledgeDb` configurada, execute no PowerShell da raiz do repositório.

```powershell
$env:DevelopmentProfile__ExternalCallsEnabled = 'true'
dotnet run --project ./src/Api.RAG --launch-profile LocalReal
```

3. Envie S01 pelo mesmo roteiro de ingestão local. Use `pdfpig-v1`, `chunk-layout10-section-record-v1` e `text-embedding-3-small-1536`. Consulte `statusUrl` até `concluida`. O perfil semântico gera uma ingestão distinta do perfil de teste.
4. Execute a calibração. O script lê as páginas esperadas do conjunto de avaliação e sugere uma distância máxima somente quando há separação na amostra.

```powershell
./specs/004-rag-perguntas-dimp/Measure-RagRetrieval.ps1 -Split calibration
```

5. Reinicie a API no terminal de execução com o valor validado localmente para S01. Para repetir a calibração, inicie a API sem essa variável.

```powershell
$env:Processing__MaximumCosineDistance = '0.36618539672865386'
dotnet run --project ./src/Api.RAG --launch-profile LocalReal
```

6. Em outro terminal, execute o aceite com a mesma distância.

```powershell
./specs/004-rag-perguntas-dimp/Measure-RagRetrieval.ps1 -Split acceptance -MaximumCosineDistance 0.36618539672865386
```

Registre o resultado em [rag-evaluation.md](../../docs/validation/rag-evaluation.md). O perfil só passa pela Fase 3 quando o script reporta `SemanticGatePassed: true` com os vetores semânticos.

## Fase 4 — Perguntar no Swagger local

1. No Rider, inicie `LocalReal` do projeto `Api.RAG`. A API fica em `http://127.0.0.1:5081`. Use o PostgreSQL local com S01 já indexado, a chave OpenAI no User Secrets da Api.RAG e as variáveis de execução da Fase 3. Configure `ConnectionStrings__KnowledgeDb`, `DevelopmentProfile__ExternalCallsEnabled=true` e `Processing__MaximumCosineDistance=0.36618539672865386`.
2. Em paralelo, inicie `DimpDemoLocal` do projeto `Api.Agente` com o Bearer da demo configurado em `SOFIA_A2A_TOKEN`. Esse perfil ativa `ClassroomDemo__ManualSearchEnabled` e usa `http://127.0.0.1:5081` como destino do MCP, com prazo de 15 segundos. Para outra porta, configure `Services__RagApi__BaseUrl`.
3. Abra [Swagger local](http://127.0.0.1:5098/swagger/index.html), clique em **Authorize** e informe o Bearer da demo. Em **POST /demo/dimp**, clique em **Try it out**, escreva “Como relaciono um comprovante de pagamento ao resumo diário?” em `pergunta`, deixe `arquivo` em branco e clique em **Execute**.
4. Confira em `analise` as páginas, os IDs dos trechos e o SHA-256 da revisão. Cada ID pode ser conferido com `get_manual_chunk` no MCP. Uma pergunta fora do acervo retorna `NO_EVIDENCE_FOUND`. Com a Api.RAG desligada, a pergunta aberta retorna `status=parcial` com `MCP_UNAVAILABLE` e o catálogo continua disponível.

O perfil de consulta é `text-embedding-3-small-1536` por padrão. Só use `Services__RagApi__EmbeddingProfileId` se houver vetores publicados para esse perfil no mesmo acervo. A resposta aberta usa até três citações extraídas da revisão S01. A demo limita a pergunta sem arquivo a 1.200 caracteres com a busca ativa e mascara CPFs, CNPJs e e-mails antes da classificação e do MCP. Escreva dúvidas gerais sem dados pessoais.

### Resultado do ensaio ponta a ponta

| Verificação | Resultado observado |
| --- | --- |
| Swagger e Bearer | OpenAPI expôs `pergunta` e `arquivo` opcionais. O POST sem Bearer retornou `401`. |
| Comprovante e resumo diário | `POST /demo/dimp` retornou `concluido` com `DT_OP` na p. 36 e a regra de soma entre os registros 1115 e 1110 nas pp. 39–40. Os três IDs de citação foram resolvidos pela Api.RAG em execução. |
| Ausência de evidência e indisponibilidade | A previsão do tempo retornou `NO_EVIDENCE_FOUND`. Ao desligar a Api.RAG, a pergunta aberta retornou `parcial` e o catálogo ainda explicou `DT_OP` para as duas formulações de referência. |
| Arquivo sintético e SOFIA | O upload gerou achados. O Agent Card apontou para o A2A do ambiente de teste e `message/send` retornou o artefato `analise-sofia-v1`. |

A matriz de regressão passou nos 36 testes selecionados de demo, A2A, MCP, busca e ingestão. Duas expectativas antigas da suíte A2A foram alinhadas ao contrato vigente. O teste do Agent Card passou a verificar `text/plain` e o teste de método desconhecido usa um método ausente da lista de operações.

## Fase 5 — Pré-produção e produção

O projeto `agente-dimp-rag` na Vercel hospeda a consulta MCP. O Neon Free mantém o índice fora dos contêineres. O PDF oficial S01 permanece no repositório e pode ser usado pelo processo de ingestão administrativo. O [script de transferência](../../infra/Transfer-RagS01.sh) copia os 492 trechos e vetores já avaliados, com os marcadores de publicação da revisão. A transferência transacional exclui caminhos de armazenamento locais e o ledger do ambiente de desenvolvimento.

### Configuração do serviço

- `Api.RAG` usa `ConnectionStrings__KnowledgeDb` com TLS `VerifyFull` e o usuário `dimp_rag_runtime`. Esse usuário lê o acervo e insere e atualiza o ledger. A credencial proprietária do banco fica com o operador.
- `Api.RAG` usa `OpenAI__ApiKey` para o mesmo perfil `text-embedding-3-small-1536` e `RagAccess__QueryToken` para autenticar `POST /mcp`. O `Dockerfile` da Api.RAG inclui a raiz pública Root YR do ISRG para a cadeia TLS observada no Neon.
- `Api.Agente` usa `ClassroomDemo__ManualSearchEnabled=true`, `Services__RagApi__BaseUrl` e `Services__RagApi__QueryToken`. O token da SOFIA autentica a chamada do usuário ao agente.
- A ingestão e o expurgo do ledger ficam no processo administrativo `LocalReal` em loopback. Configure `RagAccess__AdminToken` e o acesso administrativo ao Neon para operar esse processo. O worker de retenção expurga registros com mais de 90 dias enquanto a instância administrativa estiver em execução.

### Verificação do serviço hospedado

| Verificação | Resultado observado |
| --- | --- |
| Neon | Migrações aplicadas e pgvector 0.8.6 ativo. O índice confirmou 492 vetores da revisão S01 e os checkpoints publicados. |
| Permissões | MCP sem Bearer ou com Bearer administrativo retornou `401`. O Bearer de consulta retornou `403` em `get_ingestion_usage`. O POST de ingestão no contêiner público retornou `404`. A rota local administrativa aceitou apenas o Bearer administrativo. |
| Persistência | Dois processos novos no perfil `HostedQuery` recuperaram a mesma revisão e perfil do Neon após reinício, com validação TLS. |
| Busca no Neon | A [Api.RAG](https://agente-dimp-rag.vercel.app/health/live) recuperou `DT_OP` na p. 36 e a regra de soma na p. 40 em 2.913 ms no ensaio observado. O ID foi resolvido em uma chamada separada. |
| Aceite da Api.RAG hospedada | 12 de 12 casos aprovados, incluindo a rejeição de perfil incompatível antes da busca. |
| Falha controlada | Em loopback, o agente em perfil de produção recebeu rejeição do MCP hospedado com uma credencial de teste inválida. Retornou `parcial` e o catálogo continuou disponível. |
| Reversão | A implantação anterior do agente é `dpl_jqgtNo4j6dmwvBQg4uW8GnNz2iKJ`. Para reverter depois de uma promoção, use `vercel rollback dpl_jqgtNo4j6dmwvBQg4uW8GnNz2iKJ` no projeto do agente e confira o domínio público. |

O deploy `dpl_B4xwGnSu7bom1B364o6zN97kegDG` da Api.RAG está ativo em `agente-dimp-rag.vercel.app`. A primeira validação do agente usou o deploy isolado `dpl_7Qy7DwReQEPSFRfrcNUPfmC2XFKS` com `--prod --skip-domain`. O Swagger autenticado no Chrome recuperou as citações, retornou `NO_EVIDENCE_FOUND` para a previsão do tempo e concluiu `message/send` com um DIMP sintético.

### Resultado em produção — 2026-11-12

| Verificação | Resultado observado |
| --- | --- |
| Promoção | O deploy `dpl_719t8FYJ3dyvfBLYCrxLG1qpRHt9`, que aplica o limite hospedado de 4 MiB, foi promovido. A Vercel confirmou que o domínio público aponta para esse ID. |
| Swagger | [Swagger de produção](https://agente-dimp-demo.vercel.app/swagger/index.html). `POST /demo/dimp` expôs o arquivo como opcional e retornou `401` sem Bearer. |
| Consulta citada | “Como relaciono um comprovante de pagamento ao resumo diário?” retornou `concluido`, `DT_OP` na p. 36 e a regra de soma nas pp. 39–40 em 4.186 ms. Os três IDs foram conferidos no MCP de produção. |
| Catálogo e ausência de evidência | A pergunta sobre a data da venda identificou `1110` e `DT_OP` na p. 36. A previsão do tempo retornou `NO_EVIDENCE_FOUND`. |
| Upload | O DIMP sintético retornou achados. O corpo multipart acima de 4 MiB retornou `413`. |
| SOFIA | O [Agent Card](https://agente-dimp-demo.vercel.app/.well-known/agent-card.json) autenticado apontou para `/a2a`. O `message/send` retornou `completed` e o artefato `analise-sofia-v1` com achados. |
| Regressão | 37 testes locais selecionados aprovados. Incluem o limite de upload no perfil de produção. |

### Procedimento de promoção

1. Aplique as migrações no banco do RAG. Indexe S01, confira o estado e teste a recuperação após reiniciar o serviço.
2. Verifique que acesso à ingestão e ao MCP sem a credencial adequada é rejeitado. Configure os segredos do agente e da RAG no ambiente de hospedagem.
3. Publique o agente como versão de teste, confira a matriz e registre ID do deploy, revisão, perfil, páginas citadas, latência e resultado. A saúde do serviço é só uma verificação de disponibilidade.
4. Registre o ID do deploy anterior. Promova a versão aprovada, repita os testes no domínio público e, se a promoção falhar, restaure o deploy anterior ou desative a busca por configuração. Mantenha o acervo indexado para diagnóstico.
