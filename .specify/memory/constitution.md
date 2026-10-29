# Constituição do AgenteDIMP

## 1. Propósito e escopo

O AgenteDIMP tem como propósito receber arquivos DIMP, validar sua estrutura e regras
aplicáveis e produzir análises detalhadas com inconsistências, evidências e limitações.
A solução DEVE combinar validação determinística, consulta a manuais por RAG, uso
controlado de ferramentas externas, análise com LLM e respostas estruturadas e rastreáveis.
A arquitetura DEVE permitir integração futura com agentes e orquestradores por A2A.

Esta constituição rege especificações, decisões arquiteturais, planos, tarefas,
implementações e revisões. Sua adoção NÃO DEVE ser interpretada como aprovação de
layouts, regras fiscais, parâmetros ou funcionalidades ainda não especificados.

DEVE indica obrigação; NÃO DEVE indica proibição; PODE indica opção condicionada
aos requisitos, à autorização aplicável e às decisões registradas.

## 2. Princípios obrigatórios numerados

### P01. Regras sustentadas por fontes oficiais

**Regra.** Layouts, regras fiscais e critérios de conformidade DEVEM ser obtidos
de documentos oficiais identificados e versionados. Cada regra implementada DEVE possuir
referência recuperável e associação à versão aplicável. O sistema e seus responsáveis
NÃO DEVEM inventar regras nem converter suposições em requisitos aprovados.

**Justificativa.** Uma conclusão somente é verificável quando a regra e sua aplicabilidade
podem ser conferidas na fonte.

**Verificação.** A especificação e o PR DEVEM relacionar regras, documentos oficiais,
versões, critérios de aceite e testes. Regra sem fonte ou aplicabilidade definida
DEVE permanecer pendente e NÃO DEVE fundamentar declaração de conformidade.

### P02. Validação determinística antes da interpretação por IA

**Regra.** Regras estruturais e de negócio determinísticas DEVEM ser executadas por
validadores ou ferramentas verificáveis. O LLM NÃO DEVE substituir, alterar ou presumir
o resultado dessas validações. Arquivos DIMP DEVEM ser validados conforme a estrutura,
os registros e as regras definidos no layout oficial e na versão aplicável.
Validação obrigatória incompleta DEVE impedir declaração de conformidade.

**Justificativa.** Resultados reproduzíveis precisam de critérios executáveis e
independentes da variabilidade do modelo.

**Verificação.** Os testes DEVEM demonstrar detecção de arquivos válidos e inválidos,
aplicação da versão correta e bloqueio de conformidade quando uma validação obrigatória
não terminar. Os resultados determinísticos DEVEM ser preservados na execução.

### P03. Serviços com responsabilidades e dados próprios

**Regra.** A solução DEVE usar .NET 10 com ASP.NET Core, com Api.Agente e Api.RAG como
projetos principais. Cada serviço DEVE ser proprietário dos seus dados. A comunicação
funcional entre eles DEVE ocorrer por MCP. A Api.Agente NÃO DEVE consultar diretamente
tabelas ou vetores internos da Api.RAG. Endpoints, aplicação, domínio e infraestrutura
DEVEM possuir responsabilidades separadas.

**Justificativa.** Fronteiras explícitas permitem evolução dos contratos e evitam
acoplamento aos detalhes internos da base de conhecimento.

**Verificação.** O plano, os ADRs, as dependências e os testes de integração DEVEM
demonstrar essas fronteiras. Credenciais, consultas ou referências que atravessem
a propriedade de dados DEVEM ser rejeitadas na revisão.

### P04. Execução rastreável e classificação honesta dos resultados

**Regra.** Cada processamento DEVE identificar execução, usuário e contexto e seguir
o fluxo da seção 3. Os resultados DEVEM distinguir erro estrutural, inconsistência de
negócio, indício identificado pela IA, ausência de evidências e falha técnica ou
dependência indisponível. Limitações e validações não realizadas DEVEM ser explícitas.

**Justificativa.** Um indício ou uma falha de infraestrutura não equivale a um erro
comprovado no documento.

**Verificação.** Contratos e testes DEVEM cobrir as cinco categorias, execução parcial
e dependência indisponível, sem transformar incerteza ou falha técnica em conformidade.

### P05. Conhecimento recuperável, versionado e avaliado

**Regra.** Documentos e chunks DEVEM manter origem, identificação, versão e localização
na fonte, quando disponível. A ingestão DEVE definir segmentação, deduplicação,
reprocessamento e atualização. A recuperação DEVE respeitar tipo de documento e versão
do manual aplicável. Referências citadas DEVEM ser recuperáveis. Evidência insuficiente
DEVE resultar em limitação declarada.

**Justificativa.** A recuperação só sustenta uma análise quando a evidência correta
pode ser reencontrada e conferida.

**Verificação.** Testes e avaliações representativas DEVEM demonstrar rastreabilidade,
filtragem por aplicabilidade e comportamento sem evidências. Modelo de embeddings,
dimensão e estratégia de busca DEVEM ser compatíveis e documentados. Tamanho dos
chunks, topK e limiares NÃO DEVEM ser fixados sem especificação e avaliação registrada.

### P06. LLM controlado por contrato e evidências

**Regra.** A integração DEVE usar OpenAI, com modelo e parâmetros em configuração externa.
A resposta do LLM DEVE obedecer a contrato JSON explícito e versionado, com mecanismo de
saída estruturada compatível com o modelo. Estrutura e regras semânticas DEVEM ser
validadas antes da aceitação. Retentativas DEVEM ser limitadas por política especificada.
O LLM NÃO DEVE fabricar evidências, resultados de ferramentas ou referências.

**Justificativa.** Uma resposta sintaticamente válida ainda pode conter conteúdo
incompatível com as regras e os resultados reais da execução.

**Verificação.** Testes DEVEM cobrir JSON inválido, violação semântica, referência
inexistente, resposta incompleta e esgotamento das retentativas. A revisão DEVE verificar
que nenhuma saída inválida é aceita como resultado concluído.

### P07. Ferramentas com autorização independente do modelo

**Regra.** Semantic Kernel DEVE integrar e executar ferramentas por Function Calling.
Cada ferramenta DEVE possuir nome, responsabilidade, contratos de entrada e saída,
validação de argumentos, autenticação, autorização, timeout, tratamento de falhas,
testes e registros de execução. Retentativas e idempotência DEVEM possuir política
quando aplicáveis, com justificativa quando não aplicáveis. A seleção de ferramenta
pelo LLM NÃO DEVE conceder autorização para sua execução.

**Justificativa.** O modelo propõe ações; as políticas do sistema determinam quais
ações o usuário e a execução podem realizar.

**Verificação.** Testes DEVEM negar chamadas não autorizadas e argumentos inválidos
antes do efeito externo e cobrir timeout, falhas e repetição de operações.
A auditoria DEVE permitir associar chamada, decisão de autorização e resultado.

### P08. Conteúdo externo tratado como dado não confiável

**Regra.** Arquivos, chunks e resultados externos DEVEM ser tratados como dados não
confiáveis. Seu conteúdo NÃO DEVE alterar instruções do sistema, regras de autorização,
contratos ou limites de execução. Dados sensíveis DEVEM ser protegidos em trânsito,
persistência, logs e envio ao provedor conforme políticas aprovadas.

**Justificativa.** Documentos podem conter instruções maliciosas ou informações que
não podem circular entre usuários, serviços ou provedores sem controle.

**Verificação.** Testes DEVEM cobrir instruções maliciosas em arquivos e chunks,
tentativas de acesso indevido e exposição de dados. A revisão DEVE verificar os limites
de confiança e os dados enviados a cada dependência.

### P09. Memória isolada e com ciclo de vida definido

**Regra.** A memória conversacional DEVE usar MongoDB ou Redis, mediante ADR anterior
à implementação. DEVE existir isolamento por usuário e conversa e, quando aplicável,
por organização. Retenção, expiração, exclusão e limite de histórico enviado ao modelo
DEVEM ser definidos. Histórico conversacional NÃO DEVE ser tratado como conhecimento
oficial nem alterar o acervo de manuais por efeito de uma conversa.

**Justificativa.** Contexto conversacional precisa de limites de acesso e duração,
sem adquirir autoridade documental.

**Verificação.** ADR e testes DEVEM demonstrar a tecnologia escolhida, isolamento,
expiração, exclusão e limite de contexto. Testes DEVEM impedir recuperação de memória
de outro usuário, conversa ou organização.

### P10. Consumo e custos registrados sem valores inventados

**Regra.** O controle de consumo DEVE persistir em PostgreSQL. Cada chamada, incluindo
retentativas e chamadas adicionais, DEVE registrar execução e correlação, provedor,
modelo, data, duração, status e consumo disponibilizado pelo provedor, incluindo tokens
de entrada e saída e embeddings quando aplicáveis. Custos estimados DEVEM indicar a
tabela de preços, sua referência e vigência ou data de consulta. Valores estimados DEVEM
ser diferenciados dos informados pelo provedor. Consumo desconhecido NÃO DEVE ser zero.

**Justificativa.** Observabilidade financeira depende da distinção entre medição,
estimativa e informação indisponível.

**Verificação.** Testes DEVEM cobrir múltiplas chamadas, retentativas, preços versionados
e ausência de métricas. A revisão DEVE conferir a origem dos valores e a possibilidade
de recalcular estimativas a partir da referência registrada.

### P11. Metadados operacionais pertencem ao sistema

**Regra.** O JSON do LLM e o envelope retornado pela API DEVEM ser contratos distintos.
Identificadores, status efetivo da execução, consumo, custos, tempos e demais metadados
operacionais DEVEM ser preenchidos e validados pelo sistema. O conteúdo analítico
do modelo NÃO DEVE sobrescrever os resultados reais de validadores ou ferramentas.

**Justificativa.** A execução possui fatos operacionais que não podem depender de
geração probabilística.

**Verificação.** Testes de contrato DEVEM demonstrar que valores operacionais propostos
pelo LLM não são usados como fonte de verdade e que o envelope reflete a execução real.

### P12. Simplicidade arquitetural com decisões explícitas

**Regra.** Projetos, bibliotecas e abstrações adicionais DEVEM ter necessidade e impacto
justificados no plano ou em ADR. Decisões arquiteturais relevantes DEVEM ser registradas
antes da implementação afetada. Versões de SDKs, escolhas de modelos e parâmetros
ajustáveis DEVEM permanecer nos planos e configurações apropriados. Uma opção técnica
PODE ser substituída mediante avaliação de impacto e observância desta constituição.

**Justificativa.** Decisões registradas permitem evolução sem criar complexidade
sem necessidade ou transformar parâmetros de execução em regras duradouras.

**Verificação.** A revisão DEVE relacionar componentes adicionais a requisitos concretos
e verificar ADRs, alternativas, impactos e compatibilidade com os contratos vigentes.

### P13. Desenvolvimento dirigido por especificações verificáveis

**Regra.** Cada funcionalidade DEVE seguir o fluxo SDD da seção 6, possuir critérios de
aceite verificáveis e rastreabilidade entre requisitos, tarefas e testes. Implementações
e PRs DEVEM demonstrar conformidade com esta constituição ou exceção aprovada e registrada.

**Justificativa.** Artefatos conectados tornam o resultado revisável e permitem detectar
divergências entre intenção, implementação e evidência.

**Verificação.** O PR DEVE apontar especificação, plano, tarefas, testes e decisões
aplicáveis e cumprir a Definition of Done. Requisitos sem evidência DEVEM permanecer
abertos, com impacto declarado.

## 3. Restrições arquiteturais

### 3.1. Api.Agente

A Api.Agente DEVE:

- Disponibilizar API REST para receber arquivos e solicitações e executar o agente.
- Orquestrar o processamento, as validações determinísticas e a análise.
- Integrar OpenAI e validar a resposta estruturada do LLM.
- Consultar a Api.RAG como cliente MCP.
- Usar Semantic Kernel para integração e execução de ferramentas por Function Calling.
- Gerenciar memória conversacional, consumo, custos e correlação das execuções.
- Expor o agente por servidor A2A conforme contrato e marco de entrega especificados.

### 3.2. Api.RAG

A Api.RAG DEVE:

- Disponibilizar API REST para administração da base de conhecimento.
- Disponibilizar endpoint POST de ingestão.
- Processar documentos e gerar chunks e embeddings com proveniência.
- Armazenar e consultar vetores em PostgreSQL com PgVector.
- Expor ferramentas de recuperação de conhecimento por servidor MCP.

A API REST administrativa da Api.RAG NÃO DEVE ser usada pela Api.Agente como atalho
à comunicação funcional por MCP. O compartilhamento de infraestrutura física PODE ser
avaliado no plano, desde que preserve propriedade, isolamento e contratos dos dados.

### 3.3. Separação e dependências

Cada serviço DEVE separar endpoints, aplicação, domínio e infraestrutura.
Essa separação NÃO DEVE exigir projetos adicionais sem justificativa.
Endpoints DEVEM delegar casos de uso à aplicação; regras de domínio NÃO DEVEM depender
de detalhes de transporte, persistência ou SDKs de provedores.

Contratos REST, MCP e A2A DEVEM ser especificados e versionados. Autenticação, autorização,
timeouts, cancelamento e comportamento diante de falhas DEVEM ser definidos antes
da implementação da integração correspondente.

### 3.4. Fluxo obrigatório de processamento

1. Receber o arquivo e identificar usuário, contexto e execução.
2. Validar tamanho, formato e condições de processamento.
3. Identificar o tipo de documento e a versão do layout.
4. Validar estrutura, registros e regras do layout DIMP aplicável.
5. Consultar por MCP os chunks do manual correspondente.
6. Executar ferramentas externas quando necessárias e autorizadas.
7. Produzir a análise com o LLM.
8. Validar a saída estruturada e suas regras semânticas.
9. Retornar o resultado com evidências, limitações e rastreabilidade.

A especificação DEVE definir as condições de interrupção, continuação parcial e retomada.
Etapas impossíveis ou não executadas DEVEM ser registradas. Tipo ou versão não
identificados NÃO DEVEM ser presumidos pelo modelo. Uma falha anterior NÃO DEVE ser
ocultada para apresentar o processamento como concluído com conformidade.

## 4. Contratos e governança dos dados

### 4.1. Documento e fontes

A especificação DEVE definir formatos e versões suportados, identificação do arquivo,
limites de processamento, fontes oficiais e critérios de aplicabilidade.
O registro da execução DEVE permitir relacionar documento, versão, validações,
ferramentas, evidências e resultado, conforme política de retenção e proteção de dados.

Layouts, regras e manuais DEVEM ter origem e versão registradas.
A localização da fonte DEVE ser preservada quando disponível; quando indisponível,
essa limitação DEVE ser explícita. Atualizações do acervo DEVEM permitir identificar
qual versão foi usada em uma análise anterior.

### 4.2. Contrato analítico e envelope da API

Os contratos DEVEM contemplar, conforme especificação:

- Identificador da execução e status do processamento, fornecidos pelo sistema.
- Resultado da análise.
- Inconsistências, sua classificação e respectivas severidades.
- Evidências e referências recuperáveis.
- Limitações e validações não realizadas.
- Necessidade de revisão humana.
- Metadados operacionais obtidos pelo sistema.

A especificação DEVE atribuir cada campo ao contrato analítico ou ao envelope da API
e definir obrigatoriedade, tipos, nulabilidade, versionamento e validações semânticas.
Este documento NÃO DEVE ser interpretado como um schema JSON já aprovado.

Critérios de severidade e revisão humana DEVEM ser explícitos e testáveis.
O resultado DEVE distinguir as cinco categorias do P04 e impedir declaração de
conformidade enquanto houver validações obrigatórias não concluídas.

### 4.3. Propriedade e ciclo de vida

A Api.RAG DEVE controlar documentos, chunks, embeddings e vetores.
A Api.Agente DEVE controlar os dados operacionais de suas execuções, sua memória
conversacional e o acompanhamento de consumo sob sua responsabilidade.

Retenção, expiração, exclusão, acesso e eventual reprocessamento DEVEM ser especificados
por categoria de dado. Relações de correlação entre serviços NÃO DEVEM permitir acesso
direto aos dados internos de outro serviço.

A configuração de embeddings DEVE impedir consulta incompatível com o índice.
Mudanças de modelo ou dimensão DEVEM ter plano documentado de atualização,
reprocessamento ou segregação dos dados afetados.

## 5. Segurança e qualidade

- Autenticação e autorização DEVEM proteger APIs, ferramentas MCP, servidor A2A,
  ingestão e administração do conhecimento conforme os contratos especificados.
- A autorização DEVE ser verificada no sistema antes de acesso a dados ou efeitos
  externos. Contexto de usuário e organização NÃO DEVE ser confiado à geração do LLM.
- Segredos DEVEM permanecer fora do código e dos artefatos versionados e DEVEM ser
  fornecidos por mecanismo externo definido no plano.
- Arquivos DEVEM passar pelos limites e controles de processamento especificados
  antes das etapas custosas ou do envio a dependências externas.
- Logs e registros DEVEM permitir auditoria sem expor segredos ou dados sensíveis
  além da finalidade e da política aprovadas.
- Políticas de timeout, cancelamento, retentativa e idempotência DEVEM ter limites
  explícitos; falhas e indisponibilidade NÃO DEVEM causar ciclos ilimitados.
- Testes DEVEM cobrir validadores determinísticos, contratos, integração MCP,
  autorização de ferramentas, isolamento da memória e comportamento do LLM/RAG.
- Avaliações de RAG e LLM DEVEM usar exemplos representativos e critérios de aceite
  registrados antes da aceitação da funcionalidade. NÃO DEVEM exigir identidade
  textual entre respostas quando o critério é semântico.
- Cenários de versão incorreta, evidência ausente, prompt injection, resposta inválida
  e dependência indisponível DEVEM ter evidência de verificação.
- Metas de desempenho, disponibilidade, qualidade e custo DEVEM ser definidas no
  escopo aplicável e avaliadas; valores NÃO DEVEM ser inventados nesta constituição.

## 6. Fluxo de desenvolvimento SDD

O fluxo DEVE seguir:

constituição → especificação → esclarecimentos → plano técnico → tarefas →
implementação → validação.

1. **Constituição:** registrar princípios, restrições e pendências.
2. **Especificação:** definir escopo, fontes oficiais, requisitos, critérios de aceite
   e limites da funcionalidade, sem presumir decisões ainda não aprovadas.
3. **Esclarecimentos:** resolver ambiguidades que afetem correção, segurança,
   contratos ou aplicabilidade. A ausência de dúvidas PODE ser registrada sem
   uma rodada adicional de perguntas.
4. **Plano técnico:** definir arquitetura, integrações, contratos, parâmetros,
   estratégias de teste, avaliações e ADRs necessários.
5. **Tarefas:** decompor o plano em trabalho verificável ligado aos requisitos
   e aos testes previstos.
6. **Implementação:** executar tarefas dentro do escopo e registrar evidências.
   Descobertas que alterem requisitos ou arquitetura DEVEM atualizar os artefatos
   antes da aceitação do trabalho afetado.
7. **Validação:** verificar os critérios de aceite, a consistência entre artefatos,
   a conformidade constitucional e as lacunas remanescentes.

Os comandos do Spec Kit DEVEM apoiar essas etapas. A execução de um comando NÃO DEVE
ser aceita como prova de conclusão da etapa sem seus artefatos e evidências.
A implementação de uma decisão pendente DEVE aguardar sua resolução na etapa indicada.

## 7. Definition of Done

Uma funcionalidade somente DEVE ser considerada concluída quando:

- O escopo e os critérios de aceite estiverem especificados e esclarecidos.
- As fontes oficiais e versões aplicáveis estiverem identificadas e recuperáveis.
- Requisitos, plano, tarefas, decisões e testes estiverem rastreáveis e consistentes.
- Os validadores determinísticos aplicáveis estiverem implementados e verificados.
- Os contratos REST, MCP, A2A e JSON afetados estiverem documentados, versionados
  e verificados conforme o escopo da entrega.
- Evidências e limitações estiverem presentes nos resultados; nenhuma validação
  obrigatória incompleta puder produzir declaração de conformidade.
- As saídas do LLM passarem por validação estrutural e semântica, com falhas tratadas
  e retentativas limitadas.
- Ferramentas, autorização, isolamento, retenção e proteção de dados afetados
  tiverem evidência de verificação.
- Chamadas adicionais, retentativas, consumo desconhecido e estimativas de custo
  estiverem tratados conforme os contratos e as políticas aplicáveis.
- Testes e avaliações definidos no plano tiverem sido executados e seus resultados
  estiverem registrados, com falhas corrigidas.
- Os critérios de qualidade, desempenho e custo aplicáveis tiverem evidência de aceite.
- O PR tiver revisão de conformidade com esta constituição e exceções, se houver,
  formalmente aprovadas e rastreadas.

Itens fora do escopo DEVEM ser identificados como tal. Um item NÃO DEVE ser marcado
como não aplicável apenas para contornar uma obrigação necessária à funcionalidade.

## 8. Governança e alterações

Esta constituição DEVE prevalecer sobre planos e convenções locais conflitantes.
Conflitos DEVEM ser resolvidos por atualização dos artefatos ou pelo procedimento
de exceção, sem alterações silenciosas de requisitos.

Cada PR DEVE incluir uma verificação dos princípios aplicáveis, os vínculos às
evidências e as exceções aprovadas. Uma exceção DEVE registrar justificativa, impacto,
responsável, aprovação pelo responsável pelo projeto e prazo de revisão quando
temporária. A ausência de aprovação DEVE impedir a aceitação do desvio.

Alterações DEVEM ocorrer por proposta revisável, com motivação, avaliação de impacto
nas especificações e implementações existentes, responsável e aprovação registrada.
Os artefatos afetados DEVEM ser atualizados ou possuir plano de adequação rastreável.

A versão DEVE seguir MAJOR.MINOR.PATCH:

- **MAJOR:** remoção ou redefinição incompatível de princípios ou governança.
- **MINOR:** adição de princípios ou ampliação material de obrigações compatíveis.
- **PATCH:** esclarecimentos e correções sem alteração de obrigação.

A adoção inicial DEVE usar 1.0.0. A data de ratificação DEVE preservar a adoção original;
a data da última alteração DEVE refletir cada modificação, ambas em formato ISO.

Este documento define obrigações de desenvolvimento. NÃO DEVE ser usado para declarar
conclusão de funcionalidades, validação de documentos ou aprovação de decisões pendentes.

**Versão**: 1.0.1 | **Ratificada em**: 2026-10-29 | **Última alteração**: 2026-10-29

## 9. Decisões pendentes

As decisões abaixo NÃO estão aprovadas por esta constituição.
Cada uma DEVE ser resolvida no artefato e na etapa indicados antes do trabalho dependente.

| ID | Decisão a resolver | Etapa e registro exigidos |
| --- | --- | --- |
| D01 | Tipos de arquivo, formatos, versões de layout e documentos oficiais DIMP suportados na primeira entrega. | Especificação e esclarecimentos, com fontes oficiais e aplicabilidade. |
| D02 | Regras estruturais e de negócio, layouts oficiais aplicáveis e critérios de conformidade. | Especificação e esclarecimentos; rastreabilidade entre regra, fonte e aceite. |
| D03 | Limites de arquivos, identificação de versão e condições de interrupção, execução parcial e retomada. | Requisitos na especificação; mecanismos e limites operacionais no plano. |
| D04 | Campos, status, severidades, evidências e critérios de revisão humana nos contratos analítico e da API. | Especificação e esclarecimentos; schemas e validações no plano. |
| D05 | Corpus de manuais, proveniência, localização, versionamento, atualização e autorização da ingestão. | Especificação; fluxo de ingestão e ciclo de vida no plano. |
| D06 | Segmentação, deduplicação, embeddings, dimensão, busca, topK e limiares do RAG. | Plano técnico, com avaliação representativa antes do aceite. |
| D07 | Modelo OpenAI, parâmetros, limites de contexto, saída estruturada e política de resposta inválida. | Plano técnico e configuração externa, antes da implementação da integração. |
| D08 | Catálogo de ferramentas, contratos, autorizações, efeitos externos e políticas de execução. | Especificação e esclarecimentos; contratos e testes no plano. |
| D09 | MongoDB ou Redis para memória e os limites de retenção, exclusão e histórico. | ADR e plano técnico, antes da implementação da memória. |
| D10 | Organização dos dados de consumo em PostgreSQL, precificação, ausência de métricas e falhas de registro. | Plano técnico e ADR quando relevante; contratos de dados e verificações. |
| D11 | Contratos MCP e A2A, descoberta, autenticação, autorização e marco de entrega A2A. | Escopo na especificação; contratos, decisões e testes no plano. |
| D12 | Identidade dos usuários, organizações aplicáveis, dados sensíveis e política de envio ao provedor. | Especificação e esclarecimentos; controles, retenção e exclusão no plano. |
| D13 | Hospedagem, componentes adicionais, SDKs, topologia e mecanismo de fornecimento de segredos. | Plano técnico e ADRs relevantes, sem alterar as restrições constitucionais. |
| D14 | Conjuntos de avaliação, metas de qualidade, desempenho e custo e critérios de aceite operacional. | Requisitos na especificação; procedimento e evidências no plano e na validação. |
| D15 | Responsáveis nominais por aprovações, exceções e revisão de decisões arquiteturais. | Governança do projeto antes da primeira aprovação ou exceção aplicável. |