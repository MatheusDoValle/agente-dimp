# ADR-0003 — Retenção, mascaramento e fronteiras dos contratos

Data: 2026-10-29.
Status: aceito no desenho, conforme esclarecimentos do responsável.
Escopo: FR-014/015/017/022/024/027 a FR-032.

## Decisão

O conteúdo derivado do DIMP enviado à OpenAI limita-se ao resumo das validações e aos
trechos necessários com identificadores mascarados. O mapa reversível nunca sai da
Api.Agente. A inspeção vale para mensagens de ferramentas, retentativas, logs e persistência.
Mascarar por campo e por dados conhecidos da execução; descrições interpoladas também
passam pelo filtro. Um achado não pode vazar valor original só por não ser uma linha do arquivo.

O original e os índices/mapa vivem apenas enquanto a execução aceita aguarda/realiza
processamento. A etapa limpeza pertence ao processamento. Somente após sua confirmação
o sistema grava endedAt e publica o estado terminal.
Falha de limpeza fica operacionalmente visível e é recuperada, sem anunciar exclusão falsa.

Relatórios, evidências e mensagens armazenadas são mascarados e expiram em endedAt+30 dias.
A leitura bloqueia no instante de expiração, mesmo antes do coletor físico.
Coletor inicial roda a cada minuto; parâmetro operacional ajustável e testado.
Tombstones mínimos por 90 dias após expiresAt permitem 410; depois podem retornar 404.
Ledger de consumo por 90 dias e logs sanitizados por 7 dias são políticas operacionais
deste plano, sem arquivo ou conteúdo que reconstrua o relatório.
Acervo oficial e preços versionados seguem seu próprio ciclo de vida.

REST possui contrato operacional versionado e separado do schema do modelo.
Estado de execução, conformidade, categoria e severidade têm eixos distintos.
Achados completos são paginados e não descartados para caber no contexto do LLM.
Resultado de IA é revalidado e não pode reduzir revisão humana obrigatória.

## Exceção existente e marco futuro

EX-001 adia autorização por decisão expressa do responsável.
Não há implementação de perfis/permissões nesta entrega controlada.
Não enfraquecer demais requisitos de proteção por causa desse adiamento.
Servidor A2A será adaptador futuro nos mesmos casos de uso, com contrato e revisão
dos controles antes de uso compartilhado, conforme a especificação.

## Alternativas rejeitadas

Guardar original por 30 dias foi opção não escolhida pelo usuário.
Deixar expiração depender apenas do coletor permitiria leitura após o prazo.
Usar IDs/tokens/custos inventados pelo modelo eliminaria a confiabilidade do envelope.
Truncar achados determinísticos para caber no prompt esconderia inconsistências.

## Verificação

Testar fim por sucesso/falha/cancelamento/timeout, crash durante limpeza, dia 30 com
relógio controlado, tombstone, mascaramento em todas as saídas e ausência de payload
bruto nos backups/logs de aplicação. A política local não garante retenção zero do provedor;
verificar configurações/contrato da conta antes de usar dados reais.