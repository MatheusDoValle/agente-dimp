# ADR-0001 — Dois serviços e processamento durável

Data: 2026-10-29.
Status: aceito no desenho técnico; depende de validação na implementação.
Escopo: FR-001 a FR-008, FR-016/025/028/030 a FR-032.

## Contexto

A constituição exige Api.Agente e Api.RAG em .NET 10/ASP.NET Core.
O usuário escolheu acompanhar análise por identificador e adiar o limite de arquivo.
Trabalho aceito precisa sobreviver ao encerramento da conexão e a reinício do processo.

## Decisão

- Manter dois projetos principais com Endpoints/Application/Domain/Infrastructure
  como módulos internos, sem bibliotecas vazias por camada.
- Api.Agente recebe arquivo por streaming e usa volume temporário privado.
- Persistir execução e job no mesmo banco agente antes de retornar 202.
- BackgroundService no Api.Agente, fila PostgreSQL com reserva curta, leases,
  heartbeat, fencing e checkpoints. Uma execução ativa por instância inicialmente.
- Usar SQLite exclusivamente como índice temporário por execução para cruzamentos
  e agregações que não cabem com segurança em memória. O pacote adicional substitui
  implementação própria de ordenação/índices em disco e não cria serviço de negócio.
  Excluir arquivo e sidecars com o original.
- Bancos/credenciais agente e rag separados, mesmo host local permitido.
- Api.Agente nunca consulta diretamente dados rag.
- Não criar frontend, broker dedicado ou worker hospedado separado neste primeiro marco.

## Alternativas consideradas

Channel em memória isolado foi rejeitado como fonte única porque perde trabalhos no crash.
Broker externo adicionaria instalação/operação sem necessidade de distribuição atual.
Tabelas permanentes com dados brutos dificultariam cumprir a exclusão do original;
índices temporários locais têm ciclo de vida explícito.
Carregar todas as linhas/relacionamentos em RAM não é aceitável para a evolução a arquivos grandes.

## Consequências e validação

Fila interna tem semântica pelo menos uma vez; etapas precisam de idempotência.
Um token de lease antigo não pode publicar relatório ou limpar o arquivo de um novo dono.
Upload/commit não formam transação distribuída: recuperação precisa limpar órfãos e
identificar jobs cujo arquivo desapareceu.
O teto do produto continua indefinido; host, disco e timeouts possuem limites efetivos
que devem aparecer no perfil de homologação e nos testes.

Aceite por testes de desconexão, crash antes/depois do 202, lease expirado, duas
execuções, perda de temporário, idempotência e exclusão. Não foi executado nesta fase.
Fontes e evidência documental em research.md; detalhes de entidades em data-model.md,
ambos em specs/001-validar-arquivo-dimp/.