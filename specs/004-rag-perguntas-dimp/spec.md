# Especificação 004 — Perguntas sobre a DIMP com RAG

**Estado**: implementada e validada em produção.

**Dependências**: [validação DIMP](../001-validar-arquivo-dimp/spec.md), [SOFIA/A2A](../002-integracao-sofia-a2a/spec.md) e [deploy](../003-deploy-nuvem-conteinerizacao/spec.md).

## Cenários e critérios de aceite

### US1 — Indexar o Manual V10 (P1)

Como operador, quero acompanhar a ingestão da revisão oficial até ela ficar disponível para consulta, para poder distinguir recebimento de indexação concluída.

- Enviar o PDF oficial com hash e perfis válidos gera um identificador consultável. A ingestão passa de recebida a concluída após a publicação dos trechos e vetores.
- Reenviar a mesma revisão e os mesmos perfis reutiliza o resultado, inclusive quando duas submissões chegam juntas. Uma interrupção durante a ingestão não libera um corpus parcial.
- Se o arquivo, hash ou perfil for inválido, a falha fica identificável. Uma tentativa de provedor com resultado desconhecido fica registrada para reconciliação.

### US2 — Recuperar trechos por significado (P1)

Como analista, quero buscar o manual usando linguagem natural e conferir os trechos em suas páginas de origem.

- Para perguntas de aceite, a busca recupera as evidências obrigatórias da revisão selecionada dentro do limite de resultados.
- Em uma busca sem evidência suficiente, o sistema indica a ausência. Uma revisão ou um perfil de vetores incompatível fica fora dos resultados.
- Cada trecho exibido identifica a fonte oficial, a revisão e o local no manual.

### US3 — Perguntar no Swagger sem anexar arquivo (P1)

Como usuário da demo, quero perguntar sobre a DIMP sem anexar um arquivo e receber uma resposta com referências verificáveis.

- A pergunta “Como relaciono um comprovante de pagamento ao resumo diário?” é respondida com evidência recuperada do Manual V10. O teste deve demonstrar que a busca foi chamada.
- “Qual registro tem a data da venda?” e consultas explícitas como “campo 3 do registro 1110” continuam funcionando.
- Uma pergunta sem respaldo recebe indicação de falta de evidência. Se a busca estiver indisponível, a resposta informa a limitação. A análise de DIMP com arquivo e o fluxo A2A continuam disponíveis.

### US4 — Validar a versão hospedada (P2)

Como operador, quero validar a indexação e a resposta citada em um ambiente de teste antes de atualizar o Swagger público.

- A versão de teste encontra trechos do corpus publicado após reinício e exige a credencial adequada nas operações entre serviços e na administração do acervo.
- O teste publicado demonstra consulta aberta com citação, consulta direta ao catálogo, upload sintético e compatibilidade A2A. O operador consegue restaurar a versão anterior do agente se a promoção falhar.

## Requisitos funcionais

| ID | Requisito | História |
| --- | --- | --- |
| FR-001 | Registrar e verificar a origem, versão, hash e perfis da ingestão do manual oficial. | US1 |
| FR-002 | Processar a ingestão com retomada controlada, deduplicação e publicação apenas de corpus completo. | US1 |
| FR-003 | Isolar buscas por revisão e perfil e registrar as tentativas de uso do provedor. | US2 |
| FR-004 | Recuperar evidências com localizador, distinguir ausência de evidência de falha técnica e medir a qualidade em perguntas de aceite. | US2 |
| FR-005 | Atender perguntas abertas sem arquivo com evidências, mantendo as consultas explícitas e os fluxos de arquivo e A2A. | US3 |
| FR-006 | Limitar a administração e a consulta entre serviços às credenciais e permissões previstas para hospedagem. | US4 |
| FR-007 | Validar o deploy em ambiente de teste com possibilidade de reversão e registrar as evidências da promoção. | US4 |

## Entidades e casos de borda

Revisão do manual, trabalho de ingestão, trecho, perfil de vetores e evidência citada. Os casos de borda incluem falha após envio ao provedor, reenvio concorrente, ausência de resultado, corpus de outra revisão, indisponibilidade da busca e pergunta com dados que precisam ser minimizados antes do envio externo.

## Critérios de sucesso

- **SC-001**: o mesmo PDF e perfis resultam em um trabalho lógico e um corpus completo, com zero trechos duplicados no cenário de reenvio concorrente.
- **SC-002**: todas as evidências obrigatórias da amostra de aceite aparecem dentro do limite aprovado, todas as citações são rastreáveis, todos os casos sem evidência são reconhecidos e há zero mistura de revisões ou perfis.
- **SC-003**: a pergunta aberta sem resposta no catálogo recebe uma resposta apoiada em evidência recuperada. Os cenários de catálogo, arquivo sintético e A2A passam nos testes de regressão.
- **SC-004**: a versão hospedada passa pelos testes de acesso, de reinício e de consulta citada antes da promoção. O procedimento de reversão é verificável.

## Escopo

O acervo inicial contém a revisão oficial S01 do Manual DIMP V10. As perguntas abertas são testadas no Swagger sem arquivo. A validação fiscal determinística permanece responsável por achados e o A2A continua como contrato de integração. Novos leiautes, memória conversacional e alterações no orquestrador central ficam fora desta entrega.
