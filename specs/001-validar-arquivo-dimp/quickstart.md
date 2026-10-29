# Roteiro de validação da primeira entrega

Este é o roteiro a executar após a implementação. A Fase 1 cria solução, compose e testes de bootstrap.
Os comandos de ingestão/análise abaixo dependem das fases posteriores; não usar a
saúde do bootstrap como evidência de funcionalidade. Ver README.md na raiz e
[registro da Fase 1](../../docs/validation/phase1-setup.md).

## Pré-requisitos

- PowerShell, Git, SDK .NET 10 e Docker Desktop em funcionamento.
- SDK local observado: 10.0.401; disponibilidade do daemon Docker ainda não verificada.
- PostgreSQL/pgvector e MongoDB local isolado, com imagens fixadas no futuro infra/compose.yaml.
- Para o ambiente Atlas, configurar ConnectionStrings__MongoDb e MongoDb__DatabaseName
  externamente; os testes locais não dependem de uma conta Atlas.
- Manual S01 preservado e com hash verificado.
- Fixtures sintéticas/mascaradas com resultados esperados revisados por regra.
- Perfil de homologação com deadlines e budgets finitos; perfil RAG medido antes do aceite.
- Modo de testes offline usa doubles de OpenAI, sem chave e sem chamadas pagas.
  Modo real é opt-in, exige credencial externa e dados já minimizados/mascarados.

## Preparar e verificar a solução implementada

~~~powershell
Set-Location 'C:/Infis/CursoIa/AgenteDIMP'
if (-not (Test-Path -LiteralPath './AgenteDIMP.slnx')) {
    throw 'A solucao ainda nao foi implementada; executar este roteiro apos a fase de implementacao.'
}
if (-not (Test-Path -LiteralPath './infra/.env')) { ./infra/New-LocalEnvironment.ps1 }
docker compose --env-file ./infra/.env -f ./infra/compose.yaml up -d --wait
dotnet restore ./AgenteDIMP.slnx
dotnet build ./AgenteDIMP.slnx --no-restore
dotnet test ./AgenteDIMP.slnx --no-build --filter 'Category!=LiveProvider'
~~~

A composição inicial contém somente dependências locais, incluindo MongoDB para testes,
e semeará bancos distintos;
a criação do schema será por migrações explícitas de cada serviço.
Não executar migrations automaticamente em produção.
Em dois terminais PowerShell, iniciar os futuros projetos:

~~~powershell
dotnet run --project ./src/Api.RAG --no-build --launch-profile LocalDeterministic -- --urls http://127.0.0.1:5081
~~~

~~~powershell
dotnet run --project ./src/Api.Agente --no-build --launch-profile LocalDeterministic -- --urls http://127.0.0.1:5080
~~~

O perfil LocalDeterministic implementado deve fornecer provedores fake de análise
e embeddings e parâmetros finitos de teste. Não confundir evidência desses doubles
com validação de qualidade/capacidade do modelo real.

## Preparar o acervo

A implementação deve fornecer perfis de extração, segmentação e embedding identificados.
O perfil usado abaixo é o que tiver sido selecionado no experimento de recuperação;
não inventar um ID para contornar a avaliação.
sourceUrl é metadado; o servidor recebe o PDF, não busca a URL enviada.

~~~powershell
$manual = './docs/referencias/dimp-v10-manual-confaz.pdf'
$hash = (Get-FileHash -LiteralPath $manual -Algorithm SHA256).Hash
if ($hash -ne 'FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A') {
    throw 'Manual diferente da revisao S01.'
}
$extractionProfileId = Read-Host 'ID do perfil de extracao implementado'
$chunkProfileId = Read-Host 'ID do perfil de segmentacao avaliado'
$embeddingProfileId = Read-Host 'ID do perfil de embeddings avaliado'
$ingestion = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:5081/api/v1/documentos/ingestoes' -Form @{
    file = Get-Item -LiteralPath $manual
    sourceUrl = 'https://www.confaz.fazenda.gov.br/legislacao/arquivo-manuais/a16___anexo_iv_dimp_v10.pdf'
    layoutVersion = '10'
    expectedSha256 = $hash
    extractionProfileId = $extractionProfileId
    chunkProfileId = $chunkProfileId
    embeddingProfileId = $embeddingProfileId
}
Invoke-RestMethod -Uri ('http://127.0.0.1:5081' + $ingestion.statusUrl)
~~~

Esperado: 202 para ingestão nova; 200 para mesma revisão/perfis já concluídos;
sem segunda chamada para resultados já persistidos. Submissões concorrentes reutilizam
o job. Crash com resposta do provedor não persistida mantém consumo desconhecido e
não dispara retry automático; não há garantia de cobrança externa exatamente uma vez. Api.Agente deve consultar trechos por
MCP, não pelo REST administrativo nem por SQL no banco rag.

## Submeter e acompanhar um arquivo sintético menor

~~~powershell
$baseUri = 'http://127.0.0.1:5080'
$fixture = Get-Item -LiteralPath './tests/fixtures/dimp-v10/valido.txt'
$accepted = Invoke-RestMethod -Method Post -Uri ($baseUri + '/api/v1/analises') -Form @{
    file = $fixture
}
$accepted.executionId
$status = Invoke-RestMethod -Uri ($baseUri + $accepted.statusUrl)
$status
~~~

Esperado: identificador retornado após upload/aceitação, antes de concluir a análise.
Consultas de status mostram etapa e atualização. Fechar a conexão após aceitação
não cancela o worker. Quando reportAvailable for true:

~~~powershell
$report = Invoke-RestMethod -Uri ($baseUri + $accepted.reportUrl)
$report
$findings = Invoke-RestMethod -Uri ($baseUri + $report.findingsUrl)
$findings
~~~

Consultar nextCursor até terminar os achados; total não é reduzido pelo tamanho da página.
Resultado concluído não significa documento sem inconsistências.
Não usar dados reais neste roteiro inicial para provar o mascaramento.

## Cenários obrigatórios de aceite

| Cenário | Resultado esperado |
| --- | --- |
| Arquivo V10 de referência | Regras aplicáveis verificadas, relatório e origem documental rastreáveis. |
| Erro estrutural/regra de campo | Achado determinístico com localização e fonte, sem inferência fiscal inventada. |
| Versão não suportada | Limitação clara; não aplicar V10 silenciosamente. |
| Falha RAG/IA | Preservar resultados determinísticos, conclusão parcial e revisão humana. |
| Resposta inválida/recusa | Schema/semântica reprovados, tentativas limitadas, nunca aceitar como sucesso. |
| Injeção no arquivo/trecho | Conteúdo não altera ferramentas, fonte, regras ou autorização. |
| Identificadores conhecidos | Nenhum valor original em payload externo, logs, relatório ou contexto persistido. |
| Duas execuções concorrentes | Nenhuma mistura de dados; lease/fencing impede publicação duplicada. |
| Crash antes/depois do 202 | Trabalho aceito não se perde; órfãos e temporários são recuperados/limpos. |
| Encerramento e limpeza | Originais/índices/mapa e contexto MongoDB excluídos antes do estado terminal; TTL não substitui exclusão explícita. |
| Dia 30 com relógio falso | 410 imediato na leitura e exclusão física pelo coletor; 404 para ID desconhecido. |
| Retry/consumo desconhecido | Cada tentativa registrada, nulos preservados e estimativa identificada. |
| SF-001/002/003 sem resolução | Não declarar cobertura completa nem inventar resultado da regra. |

Relógio controlável e injeção de falhas evitam esperar 30 dias ou simular limpeza
apenas por texto. Cobertura de regra requer exemplos positivos, negativos e condicionais,
não apenas teste que replica a implementação.
A análise de consumo soma Api.Agente e chamadas relacionadas da Api.RAG sem duplicar
tentativas. Observar que estado terminal pode ser adiado por falha real de limpeza.

## Validações reais posteriores

O teste LiveProvider deve conferir a combinação exata SK/SDK/modelo/effort/schema,
mascaramento no transporte e contabilização de cada tentativa. Ele não roda por
padrão nem foi executado durante o planejamento.

A avaliação RAG deve registrar relevância, referência correta, ausência de evidência,
perfil e latência medida; comparar perfis antes de fixar chunk/topK/limiar.
Os testes menores devem publicar tamanho/quantidade de registros, tempo por etapa,
memória e custos medidos; não extrapolar para arquivos grandes não avaliados.

SF-001/002/003 precisam de resolução oficial antes do aceite de cobertura completa.
A execução segue tasks.md; a Fase 1 não encerra os gates de aceite das jornadas.