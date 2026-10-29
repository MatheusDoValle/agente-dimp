# Fronteira prevista para A2A

A constituição prevê servidor A2A na Api.Agente e integração futura com orquestradores.
A especificação exclui integração com agentes terceiros desta primeira funcionalidade.
Portanto, este plano registra o adaptador e seu marco posterior; não anuncia endpoint
A2A ativo, Agent Card ou compatibilidade de protocolo ainda não implementados.

O adaptador deverá chamar os mesmos casos de uso de submissão, status e relatório.
Não duplicará validadores, fila nem banco. A identidade de tarefa externa será
correlacionada à executionId gerada pelo sistema.
Artefatos serão explícitos e validados; não haverá download arbitrário de URL de terceiros.

Antes da ativação:
- Registrar ADR com versão/SDK oficiais atuais e matriz de interoperabilidade.
- Revisar EX-001 e os controles de acesso antes do uso compartilhado.
- Definir Agent Card, operações de tarefa/artefato, limites, cancelamento e contratos.
- Verificar isolamento, rastreabilidade e a mesma política de retenção/mascaramento.
- Testar adaptação para os estados de execução sem reinterpretar conformidade.

Esse marco não é uma pendência que autorize omitir a arquitetura futura;
também não amplia o escopo aprovado da primeira entrega.