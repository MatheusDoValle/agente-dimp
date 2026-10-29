# Pesquisa e decisões técnicas

Data: 2026-10-29. Pesquisa apenas documental; nenhuma chamada de IA, instalação,
benchmark de aplicação ou teste de integração real foi executado.

## Decisões consolidadas

| ID | Decisão | Justificativa | Alternativas consideradas |
| --- | --- | --- | --- |
| R01 | Duas aplicações net10.0, camadas internas. | Cumpre arquitetura e usa SDK local 10.0.401. | Bibliotecas por camada adiadas por custo sem necessidade comprovada. |
| R02 | Upload streaming e original em área temporária privada. | Evita buffer integral e permite worker independente do HTTP. | IFormFile/model binding pode duplicar buffers; conteúdo em banco contraria ciclo de vida. |
| R03 | Fila PostgreSQL com leases e BackgroundService no Api.Agente. | Persistência já necessária; confirmação durável antes do 202. | Channel isolado perde trabalhos no crash; broker separado desnecessário inicialmente. |
| R04 | MongoDB Atlas para memória/contexto da execução, com MongoDB local isolado nos testes. | Escolha expressa do responsável em 2026-10-29; atende P09 mantendo o ciclo de vida já definido. | A escolha anterior de backend foi substituída; não criar histórico conversacional além do escopo aprovado. |
| R05 | MCP C# SDK v2, HTTP stateless, funções SK explicitamente adaptadas. | Fronteira clara e argumentos validados, sem dependência de importação dinâmica não verificada. | stdio/legado e importação ampla de ferramentas não adotados. |
| R06 | PostgreSQL/pgvector com busca exata e filtro por revisão/perfil. | Corpus inicial pequeno e baseline de recuperação reproduzível. | HNSW/IVFFlat exigem medir recall/latência antes de justificar ANN. |
| R07 | PdfPig, extração por página/palavra e revisão das tabelas. | Preserva localizadores; ordem extraída não é prova da leitura visual. | OCR/serviço de layout só por necessidade demonstrada e ADR. |
| R08 | SK + OpenAI Chat Completions; ferramenta em laço manual e saída final separada. | Usa stack exigida e controla efeitos/orçamentos. | Responses/Agent Framework somente após necessidade e ADR, não troca automática de stack. |
| R09 | Modelo inicial gpt-6-luna, reasoning_effort=none nas chamadas de ferramentas. | Capacidades e restrição documentadas; modelo externo à lógica de negócio. | Outro modelo exige prova de ferramentas/schema, custo e avaliação. |
| R10 | text-embedding-3-small com dimensão 1536 no perfil inicial. | Dimensão documentada; identidade do índice impede mistura. | large/dimensão reduzida somente com avaliação e reindexação. |
| R11 | JSON Schema estrito, depois validação semântica. | Forma válida não garante regra ou citação correta. | JSON mode isolado não satisfaz o contrato. |
| R13 | SQLite apenas para índices temporários do parser. | Índices relacionais locais evitam RAM proporcional ao arquivo e dispensam serviço adicional. | Índices caseiros em disco acrescentam lógica; tabelas permanentes com dados brutos dificultam exclusão. |
| R12 | Ledger por tentativa e política única de retry. | SDKs podem repetir chamadas sem visibilidade se não configurados. | Retry em várias camadas rejeitado por duplicação e custo opaco. |

R01–R13 são decisões de engenharia do plano; não são garantias fornecidas
automaticamente pelas bibliotecas. Versões exatas de pacotes e imagens serão fixadas
com restore/build, sem previews salvo ADR específico. A integração real do conjunto
SDK/modelo/schema precisa de teste na implementação.

## Protocolo, busca e avaliação

A documentação MCP v2 descreve transporte HTTP stateless e o protocolo 2026-07-28.
Não projetar initialize/Mcp-Session-Id de exemplos legados como requisitos desta baseline.
O contrato de negócio em rag-mcp.md é independente do transporte negociado pelo SDK.
Se a distribuição estável adotada não suportar o protocolo selecionado, resolver a
compatibilidade antes do aceite do adaptador, sem fallback silencioso.

A recuperação fixa documentId, SHA-256, layoutVersion e embeddingProfileId; o LLM não
pode ampliar esses filtros. Distância vetorial não será apresentada como probabilidade
de correção fiscal. Trechos citados precisam existir na revisão associada à execução.

O tamanho dos chunks, overlap, topK e limiar NÃO foram escolhidos por palpite.
Preparar conjunto de perguntas que cubra registros/campos, tabelas, regras cruzadas,
versão incorreta e ausência de resposta. Referências esperadas são revisadas contra
a página do PDF. Comparar perfis, registrar evidência, selecionar entre os perfis que passarem no gate objetivo abaixo e verificar que todas
as citações retornadas são recuperáveis.
Um perfil não avaliado não pode ser promovido como aceito para US3.
A seleção de modelo/dimensão acima é a identidade técnica inicial, não aprovação
da qualidade da recuperação sem esses experimentos.

### Gate objetivo de qualidade RAG (antes da seleção)

Versionar um conjunto de calibração e outro de aceite independente, com casos por
registro/campo, tabelas, regras cruzadas, revisão incorreta e ausência de evidência.
Cada pergunta respondível contém unidades de evidência obrigatórias revisadas no PDF.
No conjunto de aceite, exigir simultaneamente:
- 100% das unidades obrigatórias recuperadas dentro do topK e orçamento do perfil;
- 100% das citações/trechos com revisão e localizador conferíveis;
- zero trechos de revisão/perfil diferente;
- 100% dos casos rotulados sem evidência tratados sem resposta normativa inventada.
Registrar cobertura por categoria e latência/custo, sem inventar SLA.
Se nenhum perfil passar, US3 permanece sem aceite; não promover apenas o melhor
entre perfis reprovados. Não remover casos falhos nem alterar oráculos para aprovar
o algoritmo. Mudança no conjunto exige versão e revisão independente registradas.
Estes critérios valem para o corpus de aceite versionado, não provam qualidade em
qualquer consulta futura.

## Memória em MongoDB

A escolha de MongoDB Atlas substitui a decisão anterior por orientação do responsável.
Usar MongoDB.Driver e MongoClient compartilhado; credenciais e database são externos.
A coleção conversation_contexts guarda somente contexto mascarado por execução.

expiresAt define a expiração lógica; toda leitura filtra documentos ainda válidos.
Índice TTL de campo único em expiresAt, com expireAfterSeconds=0, faz limpeza auxiliar.
A exclusão TTL é assíncrona: ela não substitui a remoção explícita na finalização
nem garante remoção física no exato instante de expiração.
O worker exclui o contexto por executionId e confirma a operação antes do encerramento.
A instância local de testes usa o mesmo contrato de persistência, sem exigir cluster Atlas.
Essa troca não muda os 30 dias de retenção dos relatórios em PostgreSQL.

## Chamadas de IA e dados

A camada SK solicita ferramentas de leitura; a aplicação valida cada chamada antes
da execução. Depois da recuperação, gerar resposta final sem ferramentas com
contracts/llm-analysis.schema.json. Tratar recusa, conteúdo incompleto e erro como
resultados próprios. Validar IDs, referências, coerência e mascaramento.

O modelo baseline foi verificado documentalmente em OpenAI Docs.
Sua página exige reasoning_effort=none para Function Calling via Chat Completions.
Não assumir compatibilidade já testada entre a versão concreta de SK e esse modelo.
Se o conector não expuser parâmetro/capacidade necessário, adaptar a chamada oficial
por trás da infraestrutura mantendo SK para as funções, e verificar contrato;
não enfraquecer o schema silenciosamente.

Cada tentativa tem identificador próprio antes de iniciar a chamada, com métricas
nulas até retornarem. Desabilitar retries automáticos ocultos e centralizar política.
Custo é estimativa externa ao modelo com pricebook identificado; preços não foram
inventados nem fixados por este plano. Configurar store=false quando suportado:
essa opção não constitui garantia de retenção zero no provedor.
A política local de exclusão não descreve a retenção do serviço OpenAI;
verificar o contrato/configuração da conta antes de processar arquivos reais.

## Impedimentos documentais e limites deliberadamente adiados

Os conflitos SF-001 a SF-003 em rule-catalog.md foram observados em S01.
Pesquisa não resolve contradição normativa por inferência.
As regras ficam impedidas até confirmação oficial ou seleção documentada de revisão
oficial que resolva a divergência. Isso é um gate de aceite das regras, não ausência
de decisão sobre como a aplicação deve tratar a incerteza.

O limite de tamanho foi adiado pelo usuário; streaming não significa tamanho ilimitado.
O plano define instrumentação e testes menores, sem declarar capacidade não medida.
Timeouts e budgets finitos são configuração obrigatória do perfil de homologação;
a aplicação deve recusar ativar dependências se não houver orçamento válido.
Sem meta de throughput/latência aprovada, medir e registrar resultados em vez de alegar SLA.

## Fontes primárias consultadas

- [ASP.NET Core: file uploads](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0).
- [.NET: política de suporte](https://dotnet.microsoft.com/pt-br/platform/support/policy/dotnet-core).
- [Hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0).
- [PostgreSQL: SELECT e SKIP LOCKED](https://www.postgresql.org/docs/current/sql-select.html).
- [MongoDB: índices TTL](https://www.mongodb.com/docs/manual/core/index-ttl/).
- [MongoDB: MongoClient .NET](https://www.mongodb.com/docs/drivers/csharp/current/connect/mongoclient/).
- [MongoDB Atlas e driver .NET](https://www.mongodb.com/docs/drivers/csharp/current/get-started/).
- [MCP C# SDK v2: transportes](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html).
- [Semantic Kernel: MCP plugins](https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins/adding-mcp-plugins).
- [pgvector](https://github.com/pgvector/pgvector).
- [PdfPig](https://github.com/UglyToad/PdfPig).
- [OpenAI Docs: GPT-6 Luna](https://developers.openai.com/api/docs/models/gpt-6-luna).
- [OpenAI Docs: Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs).
- [OpenAI Docs: embeddings](https://developers.openai.com/api/docs/guides/embeddings).
- [Semantic Kernel: Function Calling](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/chat-completion/function-calling/?pivots=programming-language-csharp).
- [OpenAI SDK .NET](https://github.com/openai/openai-dotnet).
- [S01: manual CONFAZ fixado por hash](../../docs/referencias/dimp-v10-manual-confaz.pdf).

As URLs documentam capacidades gerais; leases, contratos, retenção e combinação de
componentes são decisões deste projeto e serão verificadas por testes próprios.