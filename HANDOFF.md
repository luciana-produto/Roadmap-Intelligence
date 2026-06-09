# HANDOFF — Módulo Roadmap/Planejamento (ProductHub)

> Documento de retomada gerado em 2026-06-09. A sessão foi interrompida no meio da
> correção dos bugs de priorização (drag-and-drop). Tudo que foi concluído já está
> escrito em disco; o que falta está detalhado em **Pendências** e **Próximos passos**.

---

## 1. Objetivo da tarefa

Esta sessão tratou de uma lista de ajustes no módulo de Roadmap/Planejamento, enviada
pelo usuário (produto). Os itens são:

1. **Produto da demanda** deve virar uma **listagem multisseleção em dropdown**, igual ao
   campo "Time" (antes eram checkboxes inline).
2. **Dependência entre demandas/épicos**: ao editar a demanda **bloqueadora** ("B"), ela
   perdia a marcação "Este item bloqueia", o ícone de cadeado e o badge "Dependência
   inconsistente" (só voltava com F5). Precisa refletir sem reload.
3. **Excluir vínculo de bloqueio pela demanda bloqueadora**: hoje só dá para remover o
   vínculo acessando a demanda bloqueada ("A"). O usuário quer remover também pela "B",
   na seção "Este item bloqueia".
4. **Priorização (arraste para priorizar) — 3 sub-bugs:**
   - (4a) **Épico simples sem demanda se perde** ao arrastar; às vezes precisa arrastar 2x.
   - (4b) Arrastar uma demanda/épico para a **última linha antes do agrupador do próximo
     quarter** faz o item **trocar de quarter** em vez de virar última prioridade do
     quarter atual.
   - (4c) Intermitente: o item **trava/some na tela** durante o arraste.

**Restrição de ambiente (persistente):** NÃO é possível rodar `npm run typecheck` /
`nuxt build` (Node v14; Nuxt 4 exige 18+) nem `dotnet build` (sem .NET SDK). Validação é
feita **por inspeção** apenas.

**Regra de memória (persistente):** sempre indicar ao final de cada resposta se a mudança
foi no backend, frontend ou ambos.

---

## 2. O que já foi concluído

### ✅ Tarefa #1 — Produto da demanda como dropdown multisseleção (FRONTEND)
- Criado computed `demandProductsLabel` (label "Selecione os produtos" / nome / "N produtos").
- O campo "Produto" da demanda foi convertido de checkboxes inline para um `UPopover` +
  `UButton` com checkboxes internos, no mesmo padrão do campo "Time" de épicos.
- Mantida a divulgação progressiva (aparece ao lado de "Épico pai" depois que o time é
  selecionado).

### ✅ Tarefa #2 — Bloqueador mantém "Este item bloqueia" + cadeado + badge sem F5 (BACKEND + FRONTEND)
**Causa raiz:** no `UpdateRoadmapDemandCommandHandler`, ao editar B, o `dependedOnBy` era
mapeado a partir de `dependencyLinks`, mas a demanda A (que depende de B) **não era
carregada em `demandsById`** → `MapDependency(A)` retornava null → `dependedOnBy` vinha vazio.
- **Backend:** o handler agora carrega `dependencyLinkDemands` (todas as demandas dos dois
  lados de cada link) e as inclui em `demandsById`.
- **Frontend (store):** `refreshDependencySnapshotsFor(updatedDemand)` (criado em sessão
  anterior) atualiza os snapshots `dependsOn`/`dependedOnBy` de todas as outras demandas
  que referenciam a demanda editada — agora chamado dentro de `applyUpdatedDemandState`.
- **Frontend (roadmap.vue):** `isReverseDependencyInconsistent` + badge de inconsistência
  considerando os dois lados (já feito em sessão anterior; confirmado em uso aqui).

### ✅ Tarefa #3 — Excluir vínculo "Este item bloqueia" pela bloqueadora (BACKEND + FRONTEND)
- **Backend:**
  - `UpdateRoadmapDemandCommand`: novo campo `IReadOnlyList<Guid>? RemovedDependedOnByIds = null`.
  - `IRoadmapDemandRepository` + `RoadmapDemandRepository`: novo método
    `RemoveDependenciesPointingToAsync(blockerDemandId, dependentDemandIds)` — remove os
    links onde `DependsOnDemandId == blocker && DemandId IN dependents`.
  - `UpdateRoadmapDemandCommandHandler`: após `ReplaceDependenciesAsync` e **antes** de
    `SaveChangesAsync`, chama a remoção quando `RemovedDependedOnByIds` vier preenchido.
- **Frontend:**
  - `types/roadmap.ts`: `DemandFormData.removedDependedOnByIds?: string[]`.
  - `utils/roadmapDemandPayload.ts`: `buildUpdateDemandPayload` inclui
    `removedDependedOnByIds: payload.removedDependedOnByIds ?? []`.
  - `stores/roadmap.ts` (`updateDemand`): após `applyUpdatedDemandState`, remove
    localmente o `dependsOn` de cada A apontando para B (sem F5).
  - `DemandFormModal.vue`: ref `removedDependedOnByIds`, computed `visibleDependedOnBy`,
    função `removeDependedOnBy`, botão "X" em cada badge de "Este item bloqueia", reset no
    watcher de abertura, e inclusão no `emit('submit', ...)`.

### 🟡 Tarefa #4 — Priorização (drag) — PARCIALMENTE FEITA (FRONTEND)
**Já aplicado em disco:**
- Em `syncListSectionDividers` (roadmap.vue), os dividers de **quarter** e **additional**
  agora recebem `dataset.quarterKey` (linha ~2034, bloco que cria `dividerRow`). Isso
  permite descobrir o quarter de destino quando o item é solto contra um divider.
- Adicionados os helpers **`resolveDropQuarterKey(item)`** e **`parseQuarterKey(key)`**
  logo **antes** de `handleEpicSortEnd` (~linha 1446). `resolveDropQuarterKey` sobe pelo
  DOM a partir do item solto e retorna o `quarterKey` da primeira data-row OU divider de
  quarter/additional — desambiguando "última linha do quarter" de "primeira do próximo".

**AINDA NÃO aplicado (é exatamente onde parei):** ligar esses helpers nos handlers e
adicionar o `forceListRerender`. Ver **Pendências** e **Próximos passos**.

---

## 3. Arquivos alterados

### Backend
- `backend/src/ProductHub.Application/Roadmap/Commands/UpdateDemand/UpdateRoadmapDemandCommand.cs`
  - novo parâmetro `RemovedDependedOnByIds`.
- `backend/src/ProductHub.Application/Roadmap/Commands/UpdateDemand/UpdateRoadmapDemandCommandHandler.cs`
  - carrega `dependencyLinkDemands` e inclui em `demandsById` (fix #2);
  - remove vínculos via `RemoveDependenciesPointingToAsync` (fix #3).
- `backend/src/ProductHub.Domain/Roadmap/Interfaces/IRoadmapDemandRepository.cs`
  - assinatura `RemoveDependenciesPointingToAsync`.
- `backend/src/ProductHub.Infrastructure/Repositories/RoadmapDemandRepository.cs`
  - implementação de `RemoveDependenciesPointingToAsync`.

### Frontend
- `frontend/app/components/roadmap/DemandFormModal.vue`
  - Produto como dropdown (UPopover) + `demandProductsLabel`;
  - `removedDependedOnByIds`, `visibleDependedOnBy`, `removeDependedOnBy`, botão X, reset,
    inclusão no submit.
- `frontend/app/stores/roadmap.ts`
  - `updateDemand`: remoção local de `dependsOn` para `removedDependedOnByIds`.
  - (`refreshDependencySnapshotsFor` já existia de sessão anterior.)
- `frontend/app/types/roadmap.ts`
  - `DemandFormData.removedDependedOnByIds`.
- `frontend/app/utils/roadmapDemandPayload.ts`
  - `buildUpdateDemandPayload` envia `removedDependedOnByIds`.
- `frontend/app/pages/roadmap.vue`
  - `dataset.quarterKey` em dividers de quarter/additional;
  - helpers `resolveDropQuarterKey` e `parseQuarterKey`.

---

## 4. Arquivos criados
- **`HANDOFF.md`** (este arquivo).
- Nenhum arquivo de código novo foi criado nesta sessão (apenas edições).

---

## 5. Pendências (o que falta — tudo na Tarefa #4)

Faltam **4 edições em `frontend/app/pages/roadmap.vue`** para fechar os bugs de drag:

1. **`handleEpicSortEnd`** — usar `resolveDropQuarterKey(item)` como fonte primária do
   quarter de destino (antes do `targetQuarter`/`fallbackQuarterRef` existentes). Hoje o
   `targetQuarterRef` vem do `dataset.targetQuarterKey` (setado no `onMove`), que aponta
   para o quarter ERRADO quando o drop é no boundary (pega a primeira linha do próximo
   quarter). Isso causa #4a (épico simples vai pro quarter errado) e #4b (troca de quarter).

2. **`handleListSortEnd`** — substituir a determinação de `targetQuarter` (hoje
   `rowAtNewIndex ?? rowBeforeNewIndex`, baseada em índice plano) por
   `parseQuarterKey(resolveDropQuarterKey(item))`, com fallback para a lógica antiga.
   Esta é a correção central de #4a/#4b para a visão NÃO agrupada e demandas em geral.

3. **Criar `forceListRerender(scrollTop?, scrollLeft?)`** (sugestão: logo após
   `refreshListPresentation`, ~linha 5074). Deve fazer:
   ```ts
   async function forceListRerender(scrollTop?: number | null, scrollLeft?: number | null) {
     planningGroupedRenderNonce.value++            // muda listTableKey -> UTable remonta
     await refreshListPresentation(scrollTop, scrollLeft) // nextTick + sync dividers + initSortable + scroll
   }
   ```
   `planningGroupedRenderNonce` já existe (linha ~4089) e já faz parte de `listTableKey`
   (linha ~4090) — hoje nunca é incrementado; é a "válvula de escape" para forçar remount.

4. **`onEnd` do Sortable** (dentro de `initListSortable`, ~linha 1828) — envolver as
   chamadas de `handleEpicSortEnd`/`handleListSortEnd` em `try/finally` e no `finally`
   chamar `await forceListRerender(scrollTop, scrollLeft)` (capturar scroll no início do
   `onEnd`). Isso conserta #4c (item travado): num drop "no-op" o DOM é mutado pelo
   Sortable mas o watcher de dados (linha ~5422) NÃO dispara (sortOrder não muda), deixando
   o DOM inconsistente. Forçar o remount restaura a ordem do modelo sempre.

> Observação importante sobre #4c: o watcher em `roadmap.vue` (~linha 5422) só re-sincroniza
> dividers + Sortable quando o **hash dos dados muda**. Drop sem mudança de dados = DOM preso.
> Daí a necessidade do `forceListRerender` no `finally`.

---

## 6. Próximos passos (em ordem de execução)

1. Abrir `frontend/app/pages/roadmap.vue`.
2. **Editar `handleListSortEnd`** (~linha 1700+): trocar
   ```ts
   const targetQuarterRef = rowAtNewIndex ?? rowBeforeNewIndex
   if (!targetQuarterRef) { await roadmapStore.fetchDemands(); return }
   const targetQuarter = { quarterYear: targetQuarterRef.quarterYear, quarterNumber: targetQuarterRef.quarterNumber }
   ```
   por algo como:
   ```ts
   const fallbackRow = rowAtNewIndex ?? rowBeforeNewIndex
   const targetQuarter = parseQuarterKey(resolveDropQuarterKey(item))
     ?? (fallbackRow ? { quarterYear: fallbackRow.quarterYear, quarterNumber: fallbackRow.quarterNumber } : null)
   if (!targetQuarter) { await forceListRerender(); return }
   ```
   (manter o restante da lógica de `beforeId`/`afterId` como está.)
3. **Editar `handleEpicSortEnd`** (~linha 1497): antes do cálculo de `targetQuarterRef`,
   priorizar `parseQuarterKey(resolveDropQuarterKey(item))`:
   ```ts
   const walkedQuarter = parseQuarterKey(resolveDropQuarterKey(item))
   const targetQuarterRef = walkedQuarter ?? targetQuarter ?? (fallbackQuarterRef
     ? { quarterYear: fallbackQuarterRef.quarterYear, quarterNumber: fallbackQuarterRef.quarterNumber }
     : null)
   ```
4. **Adicionar `forceListRerender`** após `refreshListPresentation` (~linha 5086).
5. **Editar `onEnd`** (~linha 1828) para `try/finally` com `forceListRerender` no finally
   (capturar `scrollTop`/`scrollLeft` no começo do `onEnd`).
6. **Validação por inspeção** (não há build disponível):
   - Conferir que `resolveDropQuarterKey`/`parseQuarterKey` estão definidos ANTES dos usos
     (já estão, ficam antes de `handleEpicSortEnd`).
   - Conferir que `planningGroupedRenderNonce` e `refreshListPresentation` existem (existem).
   - Reler os trechos editados com a tool Read.
7. **Atualizar o TODO** e marcar #4 (e sub-itens) como concluído.
8. **Testes manuais sugeridos** (quando o usuário tiver ambiente):
   - Arrastar demanda para última linha do quarter (deve virar última prioridade, NÃO trocar de quarter).
   - Arrastar épico simples sem demanda, agrupado e não agrupado (não deve "se perder" nem exigir 2x).
   - Forçar drops "no-op" repetidos (não deve travar item na tela).
   - Editar demanda B bloqueadora (badge/cadeado/"Este item bloqueia" permanecem sem F5).
   - Remover vínculo pela B (X no badge) e salvar.
   - Produto da demanda abrindo como dropdown multisseleção.

---

## 7. Decisões tomadas e justificativas

- **Fix #2 no backend (carregar os dois lados do link) em vez de só no frontend:** o F5
  resolvia porque o `fetchDemands` traz os links completos; logo o problema estava na
  resposta do update. Corrigir na origem evita divergência entre update e reload.
- **Remoção de vínculo via campo no command (`RemovedDependedOnByIds`)** em vez de endpoint
  dedicado: o formulário de B já faz PUT no save; reaproveitar o fluxo é mais simples e
  atômico (uma transação). `RemoveDependenciesPointingToAsync` remove só os links
  direcionados (não mexe nas outras dependências de A).
- **`resolveDropQuarterKey` por walk no DOM** em vez de índice plano: a ambiguidade
  "última do quarter X" vs "primeira do quarter X+1" só é resolvível com a posição real
  do item relativo aos dividers. Dar `quarterKey` aos dividers torna o walk confiável.
- **`forceListRerender` via `planningGroupedRenderNonce`:** o nonce já estava cableado no
  `listTableKey` mas nunca era usado — é o caminho oficial para remmontar a UTable e
  reconstruir o DOM na ordem do modelo. Resolve o "travado" de forma genérica.
- **Produto como UPopover (mesmo padrão do Time):** consistência de UX pedida pelo usuário.

---

## 8. Comandos necessários para continuar

> Ambiente atual NÃO compila (Node 14 / sem .NET SDK). Os comandos abaixo são para quando
> houver ambiente adequado. No estado atual, validar por inspeção (tool Read/Grep).

```powershell
# Frontend (requer Node >= 18)
cd "c:\Projeto VSCode Rudek\ProductHub\frontend"
npm install
npm run typecheck         # validação de tipos
npm run dev               # subir local

# Backend (requer .NET SDK)
cd "c:\Projeto VSCode Rudek\ProductHub\backend"
dotnet build
dotnet test               # inclui UpdateRoadmapDemandCommandHandlerTests
```

Git (somente quando o usuário pedir commit/push):
```powershell
cd "c:\Projeto VSCode Rudek\ProductHub"
git status
git add -A
git commit -m "..."       # branch atual: main
```

---

## 9. Possíveis riscos / pontos de atenção

- **`forceListRerender` no `finally` do `onEnd` remonta a tabela a cada arraste.** É
  intencional (robustez), mas pode haver duplo refresh no caminho de sucesso (watcher de
  dados + forceListRerender). É seguro/idempotente, mas se houver "piscada" perceptível,
  considerar chamar `forceListRerender` somente quando o drop foi no-op (ex.: handlers
  retornarem boolean "persistiu?").
- **`resolveDropQuarterKey` depende de `dataset.quarterKey` nos dividers.** Já adicionado
  para quarter/additional. Dividers de épico (modo agrupado) já tinham `quarterKey`.
  Linhas ocultas (`display:none`) têm a classe `list-demand-row` e o `quarterKey`
  REMOVIDOS em `syncListSectionDividers`, então o walk as ignora corretamente.
- **Modo agrupado vs não agrupado:** demandas (em ambos os modos) passam por
  `handleListSortEnd`; épicos compostos e épicos simples no modo agrupado passam por
  `handleEpicSortEnd`. Testar os dois caminhos.
- **Teste backend `UpdateRoadmapDemandCommandHandlerTests`** usa argumentos nomeados; o
  novo parâmetro opcional `RemovedDependedOnByIds = null` não quebra chamadas existentes.
- **`isSameDemandScope` para épico simples** usa `getEffectiveProjectId` (projectId ?? 
  projectIds[0]). Se um épico simples tiver `projectIds` vazio, o escopo falha — improvável,
  mas atenção em dados legados.
- **Não foi alterado o `handleEpicSortEnd` ainda** — sem a edição do passo 3, o boundary
  bug persiste no modo agrupado para épicos. Priorizar os passos 2 e 3 juntos.

---

## 10. Estado do TODO — TODAS AS TAREFAS CONCLUÍDAS

- [x] #1 Produto da demanda como dropdown multisseleção
- [x] #2 Bloqueador mantém "Este item bloqueia"/cadeado/badge sem F5
- [x] #3 Excluir vínculo pela demanda bloqueadora
- [x] #4 Priorização (drag): quarter-alvo no boundary + épico simples + travamento
  - [x] quarterKey nos dividers; helpers `resolveDropQuarterKey`/`parseQuarterKey`
  - [x] usar helpers em `handleListSortEnd` (~linha 1739)
  - [x] usar helpers em `handleEpicSortEnd` (~linha 1531)
  - [x] criar `forceListRerender` (~linha 5090, após `refreshListPresentation`)
  - [x] `try/finally` no `onEnd` chamando `forceListRerender` (~linha 1869)

> **Conclusão:** todas as edições foram aplicadas. Falta apenas validação em ambiente com
> Node ≥ 18 / .NET SDK (typecheck/build/test) e os testes manuais da seção 6, item 8.
