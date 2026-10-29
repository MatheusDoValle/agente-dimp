# Skills adicionais do Agente DIMP

Instalação: 2026-10-29.
Origem: skills-speckit_2579.zip, fornecido pelo responsável e localizado em Downloads.
Escopo: somente o projeto C:/Infis/CursoIa/AgenteDIMP.
As duas skills foram copiadas sem alterações para .agents/skills/.
As dez skills speckit existentes foram preservadas.

## O que foi instalado

| Skill | Para que serve | Quando usar | Resultado esperado |
| --- | --- | --- | --- |
| github-versionamento | Organiza revisão, validações, commit, push, branches e pull requests com rastreabilidade SDD. | Quando solicitar versionamento ou publicação das alterações no GitHub. | Arquivos revisados, validações executadas quando aplicáveis, commit/push confirmado quando solicitado e evidências da operação. |
| vercel-dotnet-pgvector-atlas | Orienta preparação e deploy de APIs .NET 10 em containers na Vercel, com PostgreSQL/PgVector e MongoDB Atlas externos. | Quando solicitar preparação de infraestrutura, deploy ou diagnóstico desse ambiente específico. | Configuração adaptada ao projeto, checklist, evidências de build/testes e URL verificada quando o deploy for executado. |

São instruções Markdown e metadados YAML para orientar o Codex.
O ZIP não contém scripts executáveis, pacotes NuGet ou uma aplicação.
A instalação não executou commit, push, deploy, migrations nem instalação de CLIs.

## github-versionamento

Arquivo de entrada: [SKILL.md](../.agents/skills/github-versionamento/SKILL.md).

A skill orienta o agente a:

1. Conferir status, branch, remoto e diferenças antes de alterar o Git.
2. Preservar mudanças de outros trabalhos e selecionar caminhos explícitos para staging.
3. Conferir arquivos sensíveis, segredos, artefatos de build e regras de ignore.
4. Relacionar as mudanças à especificação, plano, tarefas, contratos e testes existentes.
5. Executar build, testes e outras validações adequadas ao estado do projeto.
6. Usar Conventional Commits, como docs(spec), feat(agente) e fix(rag).
7. Respeitar política de branch/PR e a autorização de publicação disponível.
8. Informar commit, branch, resultado do push e link do PR quando houver.

### Aplicação ao estado atual

O repositório está na branch main, ainda sem commits e com origin configurado para
MatheusDoValle/agente-dimp. Ainda não há solução .NET implementada.
Na primeira utilização, o agente precisa lidar com a ausência de histórico e adaptar
os testes aos artefatos existentes. Não pode afirmar dotnet build/test aprovados
quando não existe aplicação para compilar.

O padrão de novas branches desta sessão é codex/, salvo escolha explícita do responsável.
O exemplo feat/ contido na skill não substitui o padrão aplicável ao projeto.
A instrução da própria skill permite seguir as convenções existentes.

### Exemplo de uso no chat

~~~text
$github-versionamento Revise os artefatos SDD do Agente DIMP e prepare uma proposta de commit.
~~~

Se quiser que a operação inclua commit e push, indique isso no pedido.
Usar a skill para revisão ou instalar a skill não constitui, por si só,
um pedido de publicação remota.

## vercel-dotnet-pgvector-atlas

Arquivo de entrada: [SKILL.md](../.agents/skills/vercel-dotnet-pgvector-atlas/SKILL.md).

A skill cobre:

1. Leitura da constituição e dos artefatos SDD antes de preparar o deploy.
2. Identificação do projeto API, dependências, porta e health check.
3. Preparação de Dockerfile e dockerignore adaptados à estrutura real.
4. Configuração externa de credenciais e separação entre Preview e Production.
5. PostgreSQL com extensão vector e MongoDB Atlas fora do container.
6. Migrações controladas e verificação da conectividade.
7. Build/testes locais, preview, verificação de endpoints e promoção quando solicitada.
8. Evidências de funcionamento, logs sanitizados e estratégia de rollback.

### Relação com o plano atual do Agente DIMP

A skill é uma referência para uma arquitetura de deploy específica, não uma mudança
aprovada da arquitetura do projeto.

| Tema | Plano atual | Como tratar a skill |
| --- | --- | --- |
| Memória | MongoDB Atlas, conforme escolha do responsável e ADR-0002 atualizado. | A skill está alinhada ao backend escolhido; preservar mascaramento, expiração e exclusão explícita do contexto. |
| Hospedagem | Desenvolvimento local; Vercel ainda não foi escolhida como destino. | Avaliar o destino quando houver solicitação de deploy. |
| Processamento | BackgroundService, fila durável e temporários que sobrevivem à conexão HTTP. | Validar host do worker, persistência temporária e recuperação antes de adaptar o deploy. |
| Serviços | Api.Agente e Api.RAG, comunicação funcional por MCP. | Preparar topologia explícita para ambos; um container de exemplo não resolve a implantação dos dois. |
| Autorização | Adiada por EX-001 no ambiente inicial controlado. | Rever o marco de acesso antes de disponibilizar a usuários/clientes, conforme a exceção já registrada. |
| RAG | Busca exata inicial; ANN depende de medição. | A citação de HNSW/IVFFlat na skill não obriga ativar esses índices agora. |

A documentação oficial da Vercel confirma suporte a containers via Functions e
detecção de Dockerfile raiz. Ela também esclarece que arquivos locais dessas funções
não são armazenamento durável e aponta Daemons para workers contínuos.
Isso precisa ser compatibilizado com nosso desenho, não presumido como resolvido.
Fontes: [containers na Vercel](https://vercel.com/kb/guide/does-vercel-support-docker-deployments)
e [Dockerfile raiz](https://vercel.com/changelog/deploy-a-container-from-a-plain-dockerfile),
consultadas em 2026-10-29.

### Exemplo de uso no chat

~~~text
$vercel-dotnet-pgvector-atlas Avalie a compatibilidade do plano do Agente DIMP com Vercel, usando MongoDB Atlas para memória e identificando como hospedar o worker.
~~~

Esse pedido é uma avaliação. Um deploy efetivo deve ser solicitado com o ambiente
de destino e as permissões correspondentes. Os comandos e Dockerfiles da skill
são modelos a adaptar, não configuração já aplicada a este repositório.

## Relação com o Spec Kit

Essas duas skills complementam o processo existente; não substituem o Spec Kit
nem são extensões registradas em .specify/extensions.yml.

O fluxo continua:

constituição → especificação → esclarecimentos → plano → tarefas →
análise de consistência → implementação → validação/convergência.

- speckit-tasks gerou a lista em [tasks.md](../specs/001-validar-arquivo-dimp/tasks.md); as tarefas de implementação continuam abertas.
- speckit-analyze verifica consistência dos artefatos antes da implementação.
- speckit-implement executa as tarefas e speckit-converge avalia o que falta.
- github-versionamento atende à etapa de versionamento/publicação quando solicitada.
- vercel-dotnet-pgvector-atlas atende a um trabalho posterior de infraestrutura/deploy.

Nenhum hook foi criado para executar Git ou deploy ao rodar comandos do Spec Kit.
O plano, a constituição e a especificação permaneceram inalterados nesta instalação.

## Onde estão os arquivos

~~~text
.agents/skills/
  github-versionamento/
    SKILL.md
    agents/openai.yaml
  vercel-dotnet-pgvector-atlas/
    SKILL.md
    agents/openai.yaml
docs/
  skills-speckit.md
  skills-speckit_2579.manifest.json
~~~

SKILL.md contém nome, descrição, critérios de uso e procedimento.
agents/openai.yaml define o nome e o resumo exibidos na interface.
O manifesto registra SHA-256 do ZIP e dos quatro arquivos instalados, para conferir
que a cópia corresponde ao pacote recebido.

Hash do ZIP:
0D25EA5DB0C0C230CD0E671BD64FB1C575EBE28512680FC3A095221A0F035F87.

## Disponibilidade no Codex

Os exemplos acima são enviados no chat; não são comandos PowerShell.

Skills locais são descobertas a partir do projeto aberto. Na conferência de
2026-10-29, o projeto cadastrado como Agente DIMP ainda apontava para a pasta antiga
no OneDrive. Para a descoberta automática destas skills, adicionar
C:/Infis/CursoIa/AgenteDIMP ao projeto e defini-la como pasta principal.
Depois, usar um novo turno; se a lista não atualizar, iniciar um novo chat nesse projeto.

Referências oficiais:
[skills locais no Codex](https://developers.openai.com/blog/skills-agents-sdk)
e [pastas e projetos](https://learn.chatgpt.com/docs/projects).

## Verificação realizada

- Duas skills instaladas, com quatro arquivos idênticos às entradas do ZIP.
- As dez skills preexistentes do Spec Kit foram preservadas.
- Ambos os SKILL.md passaram no quick_validate.py da skill-creator.
- Os dois arquivos YAML de interface passaram na validação de estrutura e campos.
- A validação verifica formato, nomes e placeholders; não certifica deploy,
  credenciais, permissões GitHub ou execução dos exemplos.

## Pacote complementar INFIS 7208

Em 2026-10-29 foram acrescentadas seis skills: rag-pgvector, mcp-client-server,
semantic-kernel-tools, memoria-mongodb, controle-custos-llm e testes-agentes-ia.
O projeto possui agora 18 skills locais: 10 do Spec Kit, 2 do pacote 2579 e 6 do pacote 7208.

O [guia complementar](skills-infis-complementares.md) explica as funções, as tarefas
relacionadas e as adaptações para as decisões do Agente DIMP.
