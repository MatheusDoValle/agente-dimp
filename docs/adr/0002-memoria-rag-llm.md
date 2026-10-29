# ADR-0002 — Memória transitória, RAG e integração de IA

Data: 2026-10-29.
Status: aceito para planejamento; memória corrigida para MongoDB Atlas por decisão do responsável em 2026-10-29.
Perfis de recuperação e conjunto de SDKs exigem testes.
Escopo: P05 a P10, FR-009 a FR-013, FR-018 a FR-020 e FR-026/027.

## Decisão

- MongoDB Atlas é o backend de memória escolhido pelo responsável, antes da implementação.
  Contexto por execução e, quando existentes, identidade/conversa/organização fornecidas
  pelo sistema. EX-001 não cria garantia de identidade multiusuário.
- Guardar apenas contexto mascarado, limitado pelo orçamento do modelo.
  expiresAt segue o deadline do job; índice TTL é limpeza auxiliar, com leitura filtrada
  e exclusão explícita confirmada ao finalizar. Usar MongoDB.Driver e MongoClient compartilhado.
  Não depender de MongoDB para fila durável, estado oficial ou histórico do documento.
- Api.RAG faz ingestão de PDF com PdfPig e revisão de extração/tabelas por página.
- Documentos e chunks preservam revisão/hash e localizadores. Dados de acervo são
  distintos do arquivo DIMP temporário recebido pelo agente.
- PostgreSQL/pgvector com busca exata inicial, filtro obrigatório por revisão/perfil.
  ANN só após medição que justifique complexidade e avalie perda de recuperação.
- MCP SDK C# v2, Streamable HTTP stateless. Adaptadores nativos SK explícitos para
  ferramentas de leitura, sem importar funções não previstas.
- Microsoft Semantic Kernel/OpenAI Chat Completions; gpt-6-luna com
  reasoning_effort=none nas chamadas com ferramentas, conforme fonte atual.
- Resposta final em chamada sem ferramentas com JSON Schema estrito e validação semântica.
- Embeddings iniciais text-embedding-3-small, dimensão 1536, identificados no índice.
  Alterar perfil exige reingestão/segregação; nenhum vetor incompatível é consultado.

## Alternativas e consequências

A escolha anterior de backend foi substituída pela preferência expressa por MongoDB.
Atlas é o destino gerenciado; testes usam MongoDB local isolado.
O ciclo de vida do contexto permanece curto e não cria uma jornada de histórico
conversacional não aprovada. MongoDB não substitui o PostgreSQL de consumo.
A limpeza TTL é assíncrona; exclusão explícita ao finalizar e filtro de expiresAt
são necessários para cumprir o ciclo de vida. Configuração e credenciais permanecem externas.
Não migrar para Agent Framework ou Responses apenas por mudança de tutoriais.
Responses pode ser avaliado em ADR se raciocínio com ferramentas for necessário.
Não depender de extensão de importação MCP .NET sem suporte documental/teste comprovados.

Chunk size, overlap, topK e limiar serão selecionados por experimento reproduzível,
não por defaults arbitrários deste ADR. Não existe perfil aprovado sem avaliação.

O teste real deve confirmar a combinação exata de SK, OpenAI SDK, effort e schema.
Recusa, truncamento, timeout e referência inexistente são caminhos de erro distintos.
A política única de retry registra cada tentativa e neutraliza retries ocultos.
Modelo não fornece identidade de execução nem preços/custos; essas informações são do host.

## Validação

Testes de TTL/exclusão e separação por execução; metadados de fontes; filtro de revisão;
vetor incompatível; citações inexistentes; Function Calling controlado e prompt injection.
Avaliação RAG antes de promoção do perfil e teste LiveProvider opt-in.
Nenhum teste real ou chamada ao provedor foi executado no planejamento.