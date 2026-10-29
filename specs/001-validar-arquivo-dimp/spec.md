# Feature Specification: Validar arquivo DIMP com análise rastreável

**Feature Branch**: Não criada; sem hook de Git instalado para esta etapa.

**Feature Directory**: specs/001-validar-arquivo-dimp

**Created**: 2026-10-29

**Status**: Clarify concluído — pronta para plano técnico com pendências explicitamente adiadas.

**Constitution**: ../../.specify/memory/constitution.md, versão 1.0.1.

**Input**: Criar a primeira funcionalidade do Agente DIMP com fluxo completo:
receber arquivo, validar, consultar o manual e analisar com IA. Buscar manual oficial
na internet. Conforme orientação do responsável, autorização fica fora desta primeira entrega.
Documentos DIMP são validados pelo seu layout; validação de formatos alheios não faz parte do escopo.

## Clarifications

### Session 2026-10-29

- Q: Quais validações devem entrar na primeira entrega do Agente DIMP? → A: Opção B — todas as regras estruturais e de negócio documentadas no manual V10, já na primeira entrega.
- Q: Qual tamanho máximo de arquivo DIMP precisamos aceitar nessa primeira entrega? → A: Indefinido por enquanto; os arquivos DIMP são muito grandes, mas os testes iniciais usarão arquivos menores. Nenhum tamanho máximo foi aprovado.
- Q: Quais informações do arquivo DIMP poderão ser enviadas à OpenAI para a análise por IA? → A: Opção A — resumo das validações e apenas os trechos necessários, com identificadores mascarados antes do envio.
- Q: Depois da análise, por quanto tempo devemos guardar o arquivo original e o relatório? → A: Opção A — arquivo original apenas durante o processamento; relatório com trechos mascarados por 30 dias.
- Q: Ao enviar um arquivo, você prefere receber um identificador para acompanhar a análise ou aguardar até o resultado ficar pronto? → A: Opção A — receber o identificador, acompanhar o status e consultar o relatório quando terminar.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Receber e validar um arquivo DIMP (Priority: P1)

Como analista que trabalha com arquivos DIMP, quero submeter um arquivo, receber um
identificador para acompanhar a análise e consultar as inconsistências verificáveis
do layout suportado quando o resultado estiver disponível.

**Why this priority**: A validação objetiva do arquivo é o resultado básico da solução
e não pode depender da interpretação da IA.

**Independent Test**: Submeter exemplos com resultados esperados definidos a partir da
fonte oficial, incluindo arquivo válido, inválido e versão não suportada, e conferir
as validações e limitações retornadas sem depender da análise por IA.

**Acceptance Scenarios**:

1. **Given** um arquivo recebido e aceito para análise, **When** a recepção termina,
   **Then** o analista recebe um identificador de execução para acompanhar o status,
   sem precisar aguardar a conclusão das validações ou da análise por IA.
   A versão identificada e os resultados ficam disponíveis durante o acompanhamento
   e na consulta do relatório, conforme as etapas forem concluídas.
2. **Given** um arquivo com erro conhecido de estrutura, registro ou campo,
   **When** a validação correspondente é executada, **Then** o resultado identifica
   a regra violada, a localização no arquivo quando recuperável e a referência oficial.
3. **Given** arquivo vazio, ilegível, acima de um limite operacional explicitamente
   definido ou sem identificação suficiente do layout, **When** é recebido, **Then** o
   sistema informa o motivo que impede o processamento e as etapas não realizadas,
   sem inventar uma versão. A fronteira de tamanho máximo de arquivo ainda não tem
   critério numérico aprovado e não deve ser apresentada como validada.
4. **Given** arquivo de outra versão, **When** é submetido à primeira entrega,
   **Then** recebe indicação de versão não suportada, sem aplicação silenciosa de regras V10.

5. **Given** o inventário de todas as regras estruturais e de negócio de S01,
   **When** a primeira entrega é avaliada, **Then** cada regra tem evidência de
   implementação e teste para suas condições de aplicabilidade; regra aplicável
   omitida, sem teste ou impedida por ambiguidade bloqueia o aceite da cobertura completa.
6. **Given** uma execução aceita, **When** o analista encerra a conexão de envio
   e consulta posteriormente o identificador, **Then** consegue acompanhar a situação
   e a etapa; ao encerrar o processamento, consegue consultar o relatório disponível.
   Consultas antes do encerramento, após expiração e com identificador inexistente
   produzem respostas distinguíveis.

---

### User Story 2 - Receber um resultado completo ou parcial sem ambiguidade (Priority: P1)

Como analista, quero distinguir erros comprovados, indícios e falhas do processamento,
para decidir o que corrigir e o que precisa de revisão humana.

**Why this priority**: Um resultado parcial não pode ser interpretado como aprovação
do arquivo, e um indício não pode ser apresentado como erro comprovado.

**Independent Test**: Fornecer resultados conhecidos de validadores e dependências,
incluindo falhas e etapas omitidas, e verificar a composição do relatório.

**Acceptance Scenarios**:

1. **Given** processamento concluído, **When** o resultado é retornado, **Then** o
   analista vê o resumo, as inconsistências, suas categorias, severidades, evidências,
   limitações e necessidade de revisão humana.
2. **Given** validação obrigatória que falhou tecnicamente ou não foi executada,
   **When** o resultado é composto, **Then** ele informa a incompletude e nunca declara
   conformidade do arquivo.
3. **Given** indício produzido pela IA sem comprovação determinística, **When** é
   exibido, **Then** ele é identificado como indício sujeito a revisão e não como
   erro estrutural ou inconsistência de negócio comprovada.
4. **Given** erro estrutural que impede interpretar parte do arquivo, **When** o
   processamento encerra, **Then** preserva os achados já verificados e identifica
   os trechos ou verificações que não puderam ser analisados com segurança.

---

### User Story 3 - Entender os achados com apoio do manual e da IA (Priority: P2)

Como analista, quero receber explicações fundamentadas no manual correspondente ao
arquivo para compreender as inconsistências e orientar minha revisão.

**Why this priority**: A explicação contextual agrega valor ao diagnóstico
determinístico, mantendo a fonte e a distinção entre fato e inferência.

**Independent Test**: Usar um resultado de validação conhecido e uma base de conhecimento
com trechos identificados; conferir explicações, referências e comportamento quando
não há evidência suficiente ou a análise por IA está indisponível.

**Acceptance Scenarios**:

1. **Given** resultados de validação e manual da versão aplicável disponível,
   **When** a análise contextual é produzida, **Then** as conclusões fundamentadas
   no manual apontam trechos e referências que podem ser recuperados.
2. **Given** ausência de trecho suficiente, **When** o sistema analisa o achado,
   **Then** declara a limitação e não fabrica uma regra, referência ou evidência.
3. **Given** falha na consulta ao conhecimento ou na análise por IA,
   **When** o resultado é retornado, **Then** os resultados determinísticos já
   obtidos permanecem disponíveis e a parte não realizada é identificada.
4. **Given** resposta da IA incompatível com os resultados reais ou com o contrato
   de resposta, **When** é verificada, **Then** ela não é aceita como análise válida;
   após as tentativas permitidas, o resultado explicita a falha dessa etapa.
5. **Given** instruções maliciosas dentro do arquivo ou do manual recuperado,
   **When** são encontradas no conteúdo, **Then** não alteram as regras de
   processamento nem autorizam ações externas.

6. **Given** resumo e trechos com identificadores conhecidos nos exemplos de teste,
   **When** o conteúdo é preparado para a análise ou para uma retentativa,
   **Then** somente o resumo e os trechos necessários com esses identificadores
   mascarados são enviados; a localização original continua rastreável para revisão local.

---

### User Story 4 - Rastrear a execução e seu consumo (Priority: P3)

Como responsável pela análise, quero relacionar o resultado ao arquivo, à referência
documental e ao consumo informado, para revisar o processamento e seus custos.

**Why this priority**: Rastreabilidade permite conferir o diagnóstico e compreender
as chamadas adicionais sem atribuir precisão a métricas indisponíveis.

**Independent Test**: Executar um cenário com múltiplas chamadas, incluindo retentativa
e consumo indisponível, e verificar correlação, origem e classificação das métricas.

**Acceptance Scenarios**:

1. **Given** uma execução com chamadas adicionais, **When** os registros são conferidos,
   **Then** cada chamada está vinculada à execução, com status, duração e consumo
   quando informado pelo provedor.
2. **Given** consumo ou preço indisponível, **When** o resultado é composto,
   **Then** a ausência é explícita e não é convertida em consumo ou custo zero.
3. **Given** custo estimado, **When** o responsável o consulta, **Then** consegue
   identificar a referência de preço usada e distingui-lo de valor informado pelo provedor.
4. **Given** duas execuções de arquivos diferentes, **When** seus resultados são conferidos,
   **Then** identificadores, evidências e resultados não são misturados.

5. **Given** uma execução encerrada por sucesso, falha, cancelamento ou timeout,
   **When** seus artefatos temporários são conferidos, **Then** o arquivo original,
   suas cópias temporárias e os dados de reversão do mascaramento foram excluídos;
   uma nova análise exige reenvio do arquivo.
6. **Given** um relatório dentro dos 30 dias após o encerramento da execução,
   **When** é consultado, **Then** seus achados e referências permanecem disponíveis
   com identificadores mascarados; após o prazo, ele está expirado e é excluído
   pela política de limpeza, sem preservar o arquivo original.

### Edge Cases

- Arquivo vazio, truncado, ilegível ou incompatível com o formato de entrada suportado.
- Estrutura que impede identificar versão, período, registro ou campo.
- Versão não suportada ou incompatibilidade de período documentada pela fonte aplicável.
- Registros ausentes, inesperados, repetidos ou fora da hierarquia prevista no manual.
- Campos com conteúdo, obrigatoriedade ou formato incompatíveis com a regra aplicável.
- Divergência entre contagens, relacionamentos ou valores que dependem de outros registros.
- Contradição, omissão ou ambiguidade na fonte oficial: registrar limitação e revisão;
  não criar uma regra para resolver silenciosamente a divergência.
- Manual recuperado de revisão ou versão diferente daquela adotada para a execução.
- Consulta sem evidências suficientes, serviço indisponível ou limite de processamento atingido.
- Resposta da IA inválida, referência inexistente ou tentativa de alterar resultado determinístico.
- Conteúdo que solicita ignorar instruções ou executar ferramentas não previstas.
- Reenvio do mesmo arquivo: tratar como nova execução identificável nesta entrega,
  sem presumir que o reenvio deve sobrescrever um resultado anterior.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE aceitar um arquivo DIMP por solicitação de análise e atribuir
  a cada execução aceita um identificador único gerado pelo próprio sistema, retornado
  após concluir a recepção e as verificações de admissibilidade, sem aguardar o
  processamento completo.
- **FR-002**: O sistema DEVE verificar legibilidade, formato e os limites operacionais
  explicitamente definidos antes de iniciar as etapas custosas; rejeições DEVEM indicar
  seu motivo. O tamanho máximo do arquivo permanece indefinido por decisão do responsável;
  essa pendência NÃO DEVE ser interpretada como suporte a arquivos de qualquer tamanho
  nem como aprovação de um limite numérico sugerido.
- **FR-003**: O sistema DEVE identificar a versão declarada e o contexto documental
  recuperável, rejeitando versões não suportadas sem presumir informações ausentes.
- **FR-004**: A primeira entrega DEVE usar o layout V10 como referência inicial, com a
  cópia documental identificada em S01. Versões anteriores não fazem parte desta entrega.
- **FR-005**: A primeira entrega DEVE implementar todas as regras estruturais e de
  negócio documentadas no manual V10 identificado em S01, por validadores determinísticos.
  O inventário DEVE ser exaustivo e abranger estrutura, registros, campos e relacionamentos,
  com identificação, aplicabilidade e referência recuperável para cada regra.
- **FR-006**: O inventário DEVE identificar regras cobertas, não implementadas,
  não aplicáveis e impedidas por ambiguidade documental. A entrega NÃO DEVE ser aceita
  como cobertura completa enquanto houver regra aplicável não implementada ou sem teste,
  ou ambiguidade que impeça verificar a regra. Uma validação obrigatória não concluída
  DEVE impedir conclusão de conformidade da execução.
- **FR-007**: Cada inconsistência comprovada DEVE indicar a regra, uma descrição,
  categoria, severidade e evidência; localização no arquivo DEVE ser informada quando
  recuperável, com justificativa explícita quando não puder ser determinada.
- **FR-008**: O sistema DEVE separar erro estrutural, inconsistência de negócio,
  indício identificado pela IA, ausência de evidências e falha técnica/dependência
  indisponível. Uma falha técnica NÃO DEVE ser atribuída ao documento como erro fiscal.
- **FR-009**: O sistema DEVE consultar somente o conhecimento identificado como aplicável
  ao layout e à revisão documental adotados e preservar essa identificação no resultado.
- **FR-010**: Trechos recuperados DEVEM permitir consultar documento, versão e localização,
  quando disponível; a ausência de localização ou de evidência suficiente DEVE ser explícita.
- **FR-011**: A análise por IA DEVE explicar os achados com base no resumo dos resultados
  reais, nos trechos estritamente necessários do arquivo com identificadores mascarados
  e nas evidências do manual recuperadas, sem substituir validadores nem fabricar referências.
- **FR-012**: Arquivos e conhecimento recuperado DEVEM ser tratados como dados.
  Instruções contidas neles NÃO DEVEM alterar o comportamento ou os limites da execução.
- **FR-013**: O resultado analítico DEVE passar por validação de estrutura, coerência com
  os achados e existência das referências antes de ser aceito. Tentativas de correção
  DEVEM ser limitadas por política documentada.
- **FR-014**: O resultado retornado DEVE conter identificador, situação do processamento,
  resumo, achados categorizados e suas severidades, evidências, limitações, validações
  não realizadas e indicação de necessidade de revisão humana.
- **FR-015**: Identificadores, tempos, consumo, custos e situação efetiva do processamento
  DEVEM ser fornecidos pelo sistema a partir da execução real, sem aceitar valores
  inventados pela IA.
- **FR-016**: O sistema DEVE preservar os resultados determinísticos disponíveis quando
  a consulta ao manual ou a análise por IA falhar e informar a incompletude do fluxo.
- **FR-017**: O sistema NÃO DEVE declarar conformidade se qualquer validação obrigatória
  aplicável não tiver sido concluída; a conclusão DEVE delimitar a cobertura efetiva.
- **FR-018**: Cada conclusão ou apontamento normativo DEVE ter referência oficial
  recuperável. Inferências sem comprovação DEVEM ser identificadas como indícios,
  com necessidade de revisão humana.
- **FR-019**: O sistema DEVE registrar as chamadas realizadas por execução, incluindo
  retentativas, com provedor, modelo, data, duração, status e consumo disponibilizado,
  inclusive geração de representações do conhecimento quando aplicável.
- **FR-020**: Consumo desconhecido DEVE permanecer identificado como desconhecido.
  Estimativas de custo DEVEM indicar a referência de preços e ser distinguidas dos
  valores efetivamente informados pelo provedor.
- **FR-021**: A primeira entrega NÃO DEVE corrigir, assinar ou transmitir o arquivo ao
  Fisco. Também NÃO DEVE executar ações externas que alterem dados; eventual ferramenta
  adicional com esse efeito exige especificação própria e definição de autorização.
- **FR-022**: O resultado DEVE indicar revisão humana quando houver indício da IA,
  insuficiência de evidências, contradição documental ou validação obrigatória pendente.
- **FR-023**: Os critérios de severidade DEVEM distinguir achados que impedem concluir a
  validação daqueles que apenas exigem atenção; a classificação NÃO DEVE transformar
  indício em erro comprovado nem falha técnica em inconsistência do arquivo.
- **FR-024**: A entrega inicial NÃO DEVE incluir gestão de permissões, perfis ou
  autorização por usuário/organização, conforme EX-001. A correlação por execução e a
  proteção de segredos e conteúdo sensível continuam obrigatórias.
- **FR-025**: O processamento NÃO DEVE misturar achados, evidências ou dados de execuções
  distintas. Uma nova submissão DEVE receber identidade de execução própria.
- **FR-026**: A referência documental usada DEVE ser identificável mesmo após atualização
  da base de conhecimento; o sistema NÃO DEVE substituir silenciosamente o manual
  associado a uma análise já produzida.

- **FR-027**: Antes de qualquer chamada à OpenAI, inclusive retentativas, o sistema DEVE
  restringir o conteúdo derivado do arquivo ao resumo das validações e aos trechos
  necessários à explicação, com identificadores mascarados. CPF, CNPJ, nomes e outros
  campos que identifiquem pessoas ou estabelecimentos, quando presentes nesse conteúdo,
  NÃO DEVEM ser enviados com seus valores originais. As substituições DEVEM ser
  consistentes dentro da execução e manter o vínculo com a localização original para
  a revisão local. O arquivo original completo e o mapa que permita reverter o
  mascaramento NÃO DEVEM ser enviados ao provedor. Mascaramento NÃO DEVE ser
  apresentado como garantia de anonimização integral do conteúdo.
- **FR-028**: O arquivo original DEVE permanecer disponível somente enquanto a execução
  aceita aguarda processamento ou está em andamento e ser excluído quando ela terminar,
  inclusive por falha, cancelamento ou timeout.
  Cópias temporárias e dados que permitam reverter o mascaramento DEVEM seguir o mesmo
  ciclo de vida. O arquivo original NÃO DEVE compor histórico, logs ou backups permanentes.
  Nova análise após o término DEVE exigir reenvio do arquivo. O plano DEVE definir
  recuperação e limpeza de temporários após interrupção inesperada do processo,
  sem converter essa recuperação em autorização para retenção histórica.
- **FR-029**: O relatório e seus metadados de rastreabilidade DEVEM ficar disponíveis
  para consulta por 30 dias corridos a partir do encerramento da execução. Os trechos
  persistidos DEVEM manter identificadores mascarados, incluindo evidências e mensagens
  derivadas dos validadores; o relatório NÃO DEVE conter cópia do arquivo original nem
  dados que permitam reverter o mascaramento. Ao completar o prazo, o relatório DEVE
  expirar e ser excluído segundo o mecanismo de limpeza definido no plano. Os registros
  de consumo e as fontes oficiais seguem políticas próprias e NÃO DEVEM armazenar o
  arquivo original como forma de contornar sua exclusão.
- **FR-030**: O processamento de uma execução aceita DEVE continuar independentemente
  da conexão que enviou o arquivo. Fechar essa conexão após a confirmação de aceitação
  NÃO DEVE cancelar a análise. A aceitação NÃO DEVE ser confundida com validação do
  conteúdo do arquivo ou conclusão da análise.
- **FR-031**: O solicitante DEVE poder consultar pelo identificador a situação da
  execução, a etapa atual e a data da última atualização. As situações de negócio são
  recebida, em processamento, concluída, concluída parcialmente, falhou e cancelada,
  quando houver cancelamento. Conclusão parcial DEVE informar etapas ou validações
  não realizadas; conclusão do processamento NÃO DEVE significar conformidade do arquivo.
- **FR-032**: O relatório DEVE ser consultável pelo identificador após o encerramento
  e durante o prazo de retenção. Antes de estar disponível, a consulta DEVE informar
  que o processamento ainda está pendente; identificador inexistente, relatório
  indisponível por falha e relatório expirado DEVEM ter respostas distinguíveis.
  Quando houver achados determinísticos e falha nas etapas seguintes, o relatório
  parcial DEVE permanecer consultável conforme FR-016 e FR-029. O plano DEVE definir
  a retenção mínima dos metadados necessária para distinguir expiração de inexistência.

### Key Entities *(include if feature involves data)*

- **Arquivo submetido**: conteúdo recebido e seus metadados, relacionado a uma execução;
  versão e contexto documental podem permanecer desconhecidos se a leitura falhar.
- **Execução de análise**: identificador, etapas realizadas, situação, etapa atual,
  data da última atualização, datas de início e encerramento, limitações e vínculos
  com arquivo, achados, referências e chamadas.
- **Regra de validação**: identificação, critério verificável, aplicabilidade ao layout,
  fonte e situação de cobertura.
- **Achado**: categoria, severidade, descrição, origem determinística ou inferencial,
  localização recuperável, regra e evidências associadas.
- **Referência documental**: origem oficial, identificação, versão, revisão/cópia,
  localização e data de obtenção.
- **Trecho de conhecimento**: conteúdo recuperado e vínculo à referência e à sua localização.
- **Resultado da análise**: resumo, achados, limitações, verificações não realizadas,
  cobertura, necessidade de revisão humana e data de expiração; evidências persistidas
  usam trechos com identificadores mascarados e ficam disponíveis por 30 dias após
  o encerramento da execução.
- **Registro de consumo**: chamada e sua correlação, provedor, modelo, métricas disponíveis,
  duração, situação e referência de eventual estimativa de custo.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 100% dos exemplos de aceite do layout suportado, o analista recebe
  identificação de execução e resultado compatível com as validações esperadas
  documentadas no inventário de regras. Evidência: US1 e FR-001 a FR-007.
- **SC-002**: Há zero declarações de conformidade em cenários de aceite com validação
  obrigatória não concluída, regra obrigatória não coberta ou versão não suportada.
  Evidência: US1/US2 e FR-003, FR-006 e FR-017.
- **SC-003**: Em 100% dos achados comprovados avaliados, o revisor consegue identificar
  a regra, a categoria, a evidência e a referência aplicável; localização indisponível
  é explicitada. Evidência: US1/US2 e FR-007, FR-008 e FR-018.
- **SC-004**: Há zero referências inexistentes aceitas nos cenários de ausência de
  evidência, resposta inválida e instrução maliciosa; todos os indícios permanecem
  identificados como inferências. Evidência: US3 e FR-010 a FR-013 e FR-018.
- **SC-005**: Em todos os cenários de indisponibilidade de conhecimento ou IA, o
  analista recebe os resultados determinísticos já obtidos e a limitação da etapa
  indisponível. Evidência: US2/US3 e FR-008, FR-014 e FR-016.
- **SC-006**: Em 100% dos cenários de retentativa ou métricas ausentes, o responsável
  consegue correlacionar as chamadas; consumo desconhecido nunca aparece como zero
  e custo estimado é distinguível de custo informado. Evidência: US4 e FR-019/FR-020.
- **SC-007**: Em todos os testes com execuções distintas, nenhum achado ou referência
  de um arquivo é atribuído ao outro. Evidência: US4 e FR-025.
- **SC-008**: Para cada exemplo de aceite, um revisor consegue determinar, usando
  somente o resultado e suas referências, o que foi verificado, o que falhou, o que
  não foi realizado e o que exige revisão. Evidência: US2/US3 e FR-014 a FR-018 e FR-022.

- **SC-009**: 100% das regras estruturais e de negócio documentadas em S01 constam
  do inventário com referência e condições de aplicabilidade. Todas as regras aplicáveis
  possuem implementação determinística e testes com resultados esperados; há zero regras
  aplicáveis omitidas, sem teste ou bloqueadas por ambiguidade no aceite da primeira entrega.
  Evidência: US1 e FR-005/FR-006.
- **SC-010**: Em 100% dos exemplos de teste com identificadores conhecidos, incluindo
  retentativas, o conteúdo enviado à OpenAI não contém os identificadores originais
  nem o arquivo completo; contém somente o resumo e os trechos necessários com
  mascaramento, mantendo rastreabilidade local dos achados.
  Evidência: US3 e FR-011/FR-027.
- **SC-011**: Em 100% dos testes de término por sucesso, falha, cancelamento e timeout,
  nenhum arquivo original ou dado de reversão permanece retido após o encerramento;
  testes de interrupção inesperada verificam a recuperação e limpeza previstas no plano.
  Evidência: US4 e FR-028.
- **SC-012**: Em todos os testes de fronteira temporal, o relatório pode ser consultado
  antes de completar 30 dias e deixa de estar disponível ao completar o prazo, com
  exclusão verificada conforme a política de limpeza. Nenhum relatório persistido
  contém identificadores originais dos trechos ou dados de reversão do mascaramento.
  Evidência: US4 e FR-029.
- **SC-013**: Em todos os testes de submissão aceita, o identificador fica disponível
  antes da conclusão da análise; encerrar a conexão original não cancela a execução.
  O solicitante consegue acompanhar o status e consultar o relatório quando disponível,
  distinguindo espera, falha, expiração e identificador inexistente.
  Evidência: US1 e FR-001/FR-030/FR-031/FR-032.

## Assumptions

### Escopo confirmado e escolhas iniciais

- **Confirmado pelo responsável em 2026-10-29**: a primeira funcionalidade cobre
  recebimento, validação determinística, consulta ao manual e análise por IA.
- **Confirmado pelo responsável em 2026-10-29**: autorização não será tratada agora;
  o tratamento constitucional dessa decisão está em EX-001.
- **Referência inicial adotada após pesquisa solicitada**: V10 do manual do CONFAZ,
  cópia S01. Não se presume suporte a versões anteriores, regras específicas de uma
  UF que não constem da referência nem cobertura de todas as revisões rotuladas V10.
- A unidade de processamento é um arquivo por solicitação. Reenvio gera outra execução.
  Lotes e comparação entre arquivos de períodos distintos ficam fora desta funcionalidade.
- O recebimento e o processamento são separados: o solicitante recebe o identificador
  após a aceitação do arquivo, acompanha a situação e consulta o relatório posteriormente.
  O processamento aceito não depende de manter a conexão original aberta.
- A entrega produz diagnóstico e explicação. Correção automática, assinatura,
  transmissão fiscal, painel visual, integração com agentes terceiros e conversação
  continuada sobre o arquivo não são jornadas desta primeira funcionalidade.
- A arquitetura estabelecida pela constituição continua válida; esta especificação
  não substitui os contratos e as decisões técnicas que serão definidos no plano.
- A primeira entrega não promete equivalência ao validador oficial nem aprovação
  fiscal. A conclusão se limita às regras efetivamente verificadas e às evidências disponíveis.

### Dependências e decisões a resolver nas próximas etapas

- O inventário de regras com referências e exemplos esperados DEVE ser detalhado
  e revisado antes da implementação dos validadores, abrangendo todas as regras
  estruturais e de negócio de S01. Nenhum subconjunto de regras será tratado como
  escopo final desta primeira entrega. Contradições documentais DEVEM ser explicitadas
  e resolvidas com evidência oficial antes do aceite da cobertura afetada, sem escolher
  interpretação silenciosa.
- Os exemplos de aceite DEVEM incluir arquivo válido, erro estrutural, erro de
  campo/regra, versão não suportada, interrupção, dependência indisponível,
  ausência de evidência e conteúdo malicioso.
- O tamanho máximo de arquivo permanece indefinido por decisão do responsável em
  2026-10-29. A validação inicial usará arquivos menores; essa orientação não define
  um tamanho numérico nem uma garantia de capacidade para arquivos grandes.
  O plano DEVE registrar os tamanhos dos exemplos usados e os limites efetivamente
  avaliados. Um teto obrigatório de produto fica adiado até revisão com esses exemplos;
  testes da fronteira de tamanho permanecem pendentes e não há suporte ilimitado implícito.
- Limites de duração, volume de achados e chamadas ainda DEVEM ser estabelecidos
  no esclarecimento/plano com justificativa e exemplos; não há números presumidos.
- As situações de processamento e a consulta posterior seguem FR-030 a FR-032.
  Severidades, representação dos estados nos contratos e conteúdo mínimo das referências
  DEVEM ser formalizados no plano, preservando FR-008, FR-014, FR-017, FR-022 e FR-023.
- O envio à OpenAI está restrito ao resumo das validações e aos trechos necessários
  com identificadores mascarados, conforme FR-011/FR-027. O plano DEVE detalhar os
  campos identificadores, a minimização e os testes de mascaramento antes de usar
  arquivos reais. O arquivo original será excluído ao término do processamento e
  o relatório com trechos mascarados ficará disponível por 30 dias, conforme FR-028/FR-029.
  O plano DEVE definir expiração, exclusão, recuperação após interrupções e demais
  controles de proteção. A retenção de consumo, logs e fontes oficiais DEVE ser
  definida separadamente, respeitando a exclusão do arquivo original.
- A disponibilidade de conhecimento com proveniência é pré-condição de US3.
  Administração completa de documentos não faz parte desta jornada; a preparação
  inicial e a atualização do acervo DEVEM ser contempladas no plano.
- Modelos, parâmetros de recuperação, contratos de integração, armazenamento e
  operação continuam sujeitos às decisões pendentes da constituição. Não são
  escolhidos implicitamente por esta especificação.

### Identificação mínima no desenvolvimento (P04)

Cada execução registra operatorId e contextId provenientes da configuração do host,
além de executionId e correlationId gerados pelo sistema. operatorId identifica o
operador local configurado; identityAssurance=unverified_local_configuration explicita
que não houve autenticação. Nenhum desses campos vem do LLM ou do arquivo.
Isso atende à rastreabilidade sem implementar login, autorização ou garantia
multiusuário. Não persistir nome de conta Windows, e-mail ou segredo por conveniência.
A configuração deve identificar o operador/contexto antes de aceitar uma execução.

### EX-001 — Adiamento temporário da autorização

- **Solicitação e aprovação**: o responsável pelo projeto, nesta conversa em 2026-10-29:
  "por enquanto nao vamos tratar autorizacao".
- **Responsável pela revisão**: responsável pelo projeto Agente DIMP.
- **Justificativa**: concentrar a primeira entrega no fluxo completo de análise.
- **Desvio delimitado**: funcionalidades de autorização, perfis e segregação por
  permissões não serão implementadas nesta primeira entrega. Não se declara que
  isolamento entre usuários ou organizações já esteja disponível.
- **Impacto constitucional**: P07/P08/P09 e seção 5, apenas quanto a controles de
  autorização e garantias de separação baseadas em identidade/permissões.
  A correlação e a separação entre execuções, a validação de argumentos e a proteção
  de segredos e conteúdo permanecem obrigatórias.
- **Limite de aplicação**: primeira entrega em ambiente de desenvolvimento controlado.
  Este adiamento não autoriza efeitos externos com alteração de dados.
- **Prazo de revisão**: antes de disponibilização compartilhada a usuários ou clientes
  ou de implantação em produção. Não há aprovação automática para esses usos.
- **Critério de encerramento**: registrar decisão revisada e verificar os requisitos
  de acesso aplicáveis antes do marco de disponibilização.

### Fontes e identificação da referência inicial

- **S01 — Manual oficial DIMP V10, CONFAZ**:
  https://www.confaz.fazenda.gov.br/legislacao/arquivo-manuais/a16___anexo_iv_dimp_v10.pdf
  — consultado e baixado em 2026-10-29; 58 páginas.
  Cópia local: ../../docs/referencias/dimp-v10-manual-confaz.pdf.
  SHA-256: FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A.
  Localizadores iniciais: pp. 1–3, apresentação e estrutura; pp. 6–9, versão,
  blocos e abertura. As regras serão identificadas individualmente no inventário.
- **S02 — Receita Estadual do RS, página do TED-TEF**:
  https://atendimento.receita.rs.gov.br/validador-ted-tef — consultada em 2026-10-29.
  A página indica o endereço de S01 como manual DIMP V10.
- A pesquisa encontrou outros PDFs V10 no CONFAZ com conteúdo e datas diferentes.
  A versão V10, sozinha, não identifica a revisão documental. S01 é a cópia de
  referência desta especificação; sua escolha não afirma que seja a última revisão
  existente nem decide a aplicabilidade jurídica a todos os períodos e UFs.