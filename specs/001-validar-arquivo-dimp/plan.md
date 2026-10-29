# Implementation Plan: Validar arquivo DIMP com análise rastreável

**Branch**: main, sem commits; 001-validar-arquivo-dimp é o identificador da funcionalidade, não uma branch criada.
**Date**: 2026-10-29 | **Spec**: [spec.md](spec.md)
**Input**: Especificação com cinco esclarecimentos, FR-001 a FR-032 e SC-001 a SC-013.
**Status**: Desenho técnico revisado após analyze; Fase 1 concluída (bootstrap offline), com evidências em docs/validation/phase1-setup.md. Aceite funcional das jornadas ainda pendente.
**Atualização 2026-10-29**: MongoDB Atlas selecionado pelo responsável para memória do agente.

## Summary

Implementar o fluxo completo em duas aplicações ASP.NET Core/.NET 10:
Api.Agente recebe um arquivo por streaming, confirma sua aceitação com HTTP 202 e
processa uma execução durável em segundo plano. Api.RAG administra a ingestão do
manual oficial e oferece recuperação exclusivamente por MCP ao agente.

Todas as regras estruturais e de negócio de S01 entram no escopo dos validadores.
O LLM explica resultados e produz indícios; não decide a validade determinística.
A OpenAI recebe apenas resumos e trechos necessários com identificadores mascarados.
O original, os índices temporários sensíveis e o mapa de mascaramento são eliminados
antes de publicar o encerramento. O relatório mascarado expira após 30 dias.

O plano não define teto de arquivo: essa escolha foi adiada pelo responsável.
Os testes iniciais usam exemplos menores e registram os limites efetivos do host.
As ambiguidades documentais identificadas em [rule-catalog.md](rule-catalog.md)
bloqueiam o aceite das regras afetadas, sem impedir o desenho da infraestrutura.

## Technical Context

**Language/Version**: C# e net10.0; SDK 10.0.401 encontrado na máquina.
Fixar SDK e dependências estáveis em arquivos de lock na implementação, verificando
compatibilidade por restore/build; nenhuma atualização de ferramentas é realizada aqui.

**Primary Dependencies**: ASP.NET Core; EF Core/Npgsql para persistência e
transações; Microsoft.Data.Sqlite para índices temporários por execução; PostgreSQL com pgvector na Api.RAG; SDK oficial MCP C# v2, HTTP stateless;
Microsoft.SemanticKernel.Connectors.OpenAI e SDK OpenAI; MongoDB.Driver;
PdfPig para extração documental; OpenTelemetry para métricas e correlação.

**Storage**: PostgreSQL com bancos e credenciais distintos agente/rag, mesmo servidor
permitido no desenvolvimento; MongoDB Atlas para memória e contexto de execução; volume temporário
privado da Api.Agente para original e índices de validação.
Baseline de desenvolvimento PostgreSQL 17 com extensão pgvector compatível e MongoDB
local isolado para testes; MongoDB Atlas é o destino gerenciado da memória.
Fixar tags/digests após confirmar imagens e extensões no bootstrap, sem uso de latest.
Configurar ConnectionStrings__MongoDb e MongoDb__DatabaseName externamente; usar
MongoClient compartilhado por aplicação, com TLS e database separado por ambiente.

**Testing**: xUnit, testes de contrato, ASP.NET Core WebApplicationFactory,
integração com PostgreSQL/pgvector e MongoDB reais via containers; relógio controlável,
provedor fake, golden files DIMP rastreados e avaliação de recuperação.
Testes reais de OpenAI são separados dos testes padrão e só usam dados sintéticos/mascarados.

**Target Platform**: desenvolvimento local Windows, serviços e dependências locais,
operação sem exposição externa; execução em Linux/containers deve ser verificada
antes de qualquer alegação de portabilidade operacional.

**Project Type**: duas APIs/backend, sem frontend nesta funcionalidade.

**Performance Goals**: upload não mantém o arquivo inteiro em memória; leitura e
validação física sequenciais; índices temporários limitam crescimento de memória;
HTTP 202 não espera o término da análise. Medir bytes, registros, duração por etapa,
pico de memória e consumo nos exemplos menores. Não inventar SLA ou capacidade máxima.
A fronteira de tamanho foi explicitamente adiada e não será marcada como testada.

**Constraints**: original temporário; nenhuma chamada externa com identificadores
originais; expiração lógica de relatório em endedAt + 30 dias; somente ferramentas
de leitura; EX-001 permite adiar autorização na primeira entrega controlada.

**Scale/Scope**: um arquivo por submissão; worker com uma execução ativa por instância
na baseline para conter competição por recursos. Paralelismo configurável somente
após medição. Todas as regras de S01, sem prometer equivalência ao validador oficial.

### Fluxo e confiabilidade

1. Receber multipart por streaming para arquivo temporário com nome gerado pelo sistema;
   aplicar legibilidade/admissibilidade e limites efetivos do host. Não confiar no nome enviado.
2. Completar a gravação e calcular o hash. Persistir execução e trabalho pendente na mesma
   transação; só então retornar 202 com identificador e URLs relativas de consulta.
   Falha no commit elimina o temporário; varredura recupera órfãos de crash.
3. BackgroundService reserva trabalho com transação curta e lease no PostgreSQL.
   Nenhuma transação fica aberta durante parsing, MCP ou chamada à OpenAI.
4. Parser V10 lê os bytes conforme S01 e preserva posição, campos vazios e origem.
   Construir fatos e índices de referência em armazenamento temporário por execução.
   Valores monetários usam decimal; não usar ponto flutuante para validar totalizadores.
5. Executar catálogo determinístico completo e persistir somente achados/evidências
   mascarados. Marcar regras avaliadas, não aplicáveis, impedidas e não concluídas.
6. Consultar revisão exata do manual por cliente MCP; integrar funções de leitura
   explícitas ao Semantic Kernel. Modelo não escolhe revisão nem recebe ferramentas arbitrárias.
7. Preparar resumos e trechos minimizados com substituições consistentes por execução.
   Gerar explicações em grupos limitados pelo orçamento de contexto, sem truncar achados
   determinísticos. Explicitar grupos não analisados se o orçamento for atingido.
8. Validar schema e semântica da resposta; montar envelope pelo sistema.
   Registrar cada tentativa externa, inclusive erro e consumo desconhecido.
9. Finalizar e limpar original, índices, contexto transitório e mapa de mascaramento.
   Publicar estado terminal somente depois da limpeza confirmada.
10. Disponibilizar relatório/achados paginados por 30 dias. A leitura verifica expiresAt;
    coletor remove conteúdo expirado. Tombstone mínimo distingue 410 de 404.

Leases, fencing token e checkpoints evitam publicação por worker que perdeu posse.
A entrega interna é pelo menos uma vez; não há promessa de chamada externa exatamente
uma vez. Estado ambíguo de consumo permanece desconhecido e não dispara repetição ilimitada.
Após crash, se o original ainda existir e a execução for ativa, retomar checkpoint seguro;
se faltar, encerrar como falha sem inventar resultado. Limpeza é idempotente.

A ingestão RAG usa fila/checkpoints próprios por ingestionJobId, reserva com lease
e fencing, deduplicação por revisão/perfis e publicação atômica do corpus completo.
Deduplicação evita nova chamada para resultados já persistidos; não garante cobrança
externa exatamente uma vez após crash. Tentativa com resposta perdida fica unknown,
sem retry automático, conforme data-model.md e contrato REST.

### Orçamentos e configuração

Parâmetros externos de modelo, timeout, token budget, quantidade máxima de chamadas,
tentativas e armazenamento devem ser validados no startup e registrados com a execução.
Os valores de operação são definidos no perfil de homologação com exemplos medidos;
não são limites de produto aprovados implicitamente.

Baseline de resiliência: até 3 tentativas totais por chamada transitória, com backoff
e Retry-After; até 1 chamada adicional de reparo de conteúdo inválido, sujeita ao mesmo
orçamento global. Recusa do modelo não é erro transitório. Desabilitar retries ocultos
nas camadas abaixo para que toda tentativa seja observável.
Cada timeout e deadline global é obrigatório no perfil operacional e finito;
se não configurado, o worker não inicia chamadas externas.
Não definir chunk size, topK ou limiar por palpite: perfil avaliado é pré-condição do RAG.
O gate de research.md exige cobertura integral das evidências obrigatórias no conjunto
de aceite, referências válidas, zero mistura de revisão e tratamento correto de ausência;
se nenhum perfil passar, manter US3 sem aceite.

### Rastreabilidade e validação

| Requisitos | Componentes/evidência prevista |
| --- | --- |
| FR-001/002/003/004/025/030/031/032 | Upload, fila durável, parser, status e contratos REST; SC-001/007/013. |
| FR-005/006/007/017/018/023 | Catálogo, validadores, totalizadores e cobertura por regra; SC-002/003/009. |
| FR-008/014/015/016/022 | Composição de relatório e estados parciais; SC-005/008. |
| FR-009/010/012/026 | Ingestão versionada, filtros MCP, evidências e prompt injection; SC-004. |
| FR-011/013/027 | Minimização, Function Calling controlado, schema estrito e validação semântica; SC-004/010. |
| FR-019/020 | Ledger por tentativa, métricas nulas e pricebook versionado; SC-006. |
| FR-021/024 | Ferramentas sem efeitos externos e exceção EX-001; testes de catálogo de funções. |
| FR-028/029 | Finalização, exclusão, expiração, tombstones e relógio falso; SC-011/012. |

## Constitution Check

Revisão anterior à pesquisa e após o desenho: as mesmas obrigações foram verificadas.
PASS indica desenho compatível, não evidência de implementação já concluída.

| Princípios | Antes | Após | Evidência do desenho |
| --- | --- | --- | --- |
| P01/P02 | PASS | PASS com impedimentos de fonte identificados | S01 fixada por hash; catálogo completo e conflitos sem interpretação inventada. |
| P03 | PASS | PASS | Duas APIs, net10.0, camadas internas e MCP como fronteira funcional. |
| P04 | Lacuna identificada no analyze | PASS no desenho revisado | operatorId/contextId configurados pelo host, identificação local não autenticada explícita, execução/correlação geradas pelo sistema e categorias separadas; ver spec.md e data-model.md. |
| P05 | PASS | PASS | Revisões imutáveis, proveniência e perfil de recuperação avaliado. |
| P06 | PASS | PASS | OpenAI configurável, schema estrito e validação semântica. |
| P07/P08 | EX-001 | EX-001 | Sem autorização nesta fase; ferramentas de leitura, validação e mascaramento mantidos. |
| P09 | PASS com EX-001 | PASS com EX-001 | MongoDB Atlas escolhido pelo responsável e registrado em ADR; contexto por execução, sem criar chat público nem garantias multiusuário. |
| P10/P11 | PASS | PASS | PostgreSQL para consumo e envelope operacional exclusivo do sistema. |
| P12 | PASS | PASS | Dois projetos principais; MongoDB é o backend permitido, não um terceiro serviço de negócio. |
| P13 | PASS | PASS | Spec, decisões, contratos e estratégia de aceite ligados aos requisitos. |

A2A: o desenho reserva um adaptador de servidor na Api.Agente.
Ativação e integração com terceiros pertencem ao marco posterior já excluído da primeira
funcionalidade; ver [contracts/a2a.md](contracts/a2a.md). Nenhum terceiro serviço substitui
Api.Agente nem é criada uma jornada não aprovada.

## Project Structure

### Documentation (this feature)

~~~text
specs/001-validar-arquivo-dimp/
  spec.md
  plan.md
  research.md
  data-model.md
  rule-catalog.md
  quickstart.md
  contracts/
    agente.openapi.json
    rag.openapi.json
    llm-analysis.schema.json
    rag-mcp.md
    a2a.md
  checklists/requirements.md
docs/adr/
  0001-servicos-processamento-duravel.md
  0002-memoria-rag-llm.md
  0003-retencao-mascaramento-contratos.md
~~~

tasks.md foi gerado; a implementação segue as fases e dependências registradas.

### Source Code (repository root)

Estrutura de referência; a Fase 1 materializa o bootstrap, sem implementar as jornadas.

~~~text
AgenteDIMP.slnx
global.json
Directory.Packages.props
src/
  Api.Agente/
    Endpoints/
    Application/
    Domain/
    Infrastructure/
    Workers/
  Api.RAG/
    Endpoints/
    Application/
    Domain/
    Infrastructure/
tests/
  Api.Agente.UnitTests/
  Api.RAG.UnitTests/
  AgenteDIMP.IntegrationTests/
  fixtures/dimp-v10/
infra/
  compose.yaml
~~~

**Structure Decision**: organização por responsabilidades dentro de dois projetos
principais, sem bibliotecas extras apenas para reproduzir camadas.
Testes unitários separados por serviço e uma suíte de integração verificam fronteiras.
Infra/compose é entregue pela Fase 1; sua evidência de execução consta em docs/validation/phase1-setup.md.

## Complexity Tracking

| Exceção/impedimento | Motivo e tratamento | Limite |
| --- | --- | --- |
| EX-001, autorização adiada | Decisão expressa do responsável em spec.md; não implementar perfis/permissões agora. | Revisar antes de uso compartilhado/produção. |
| Tamanho de arquivo adiado | Decisão expressa; nenhuma escolha numérica de produto foi feita. | Registrar capacidades medidas e limites do host; não alegar suporte ilimitado. |
| Conflitos de S01 | Regras conflitantes não podem ser inventadas para satisfazer cobertura. | Bloqueiam aceite da cobertura afetada até evidência oficial. |

Decisões de arquitetura: [ADR-0001](../../docs/adr/0001-servicos-processamento-duravel.md),
[ADR-0002](../../docs/adr/0002-memoria-rag-llm.md) e
[ADR-0003](../../docs/adr/0003-retencao-mascaramento-contratos.md).