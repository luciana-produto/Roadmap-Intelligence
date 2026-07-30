<script setup lang="ts">
// Tela de ajuda da priorização: explica Score, Esforço, Matriz e as regras
// dos itens com e sem KPI. Autossuficiente — traz o próprio botão e modal.
const open = ref(false)
</script>

<template>
  <UButton
    size="sm"
    color="primary"
    variant="soft"
    icon="i-lucide-circle-help"
    label="Como funciona?"
    @click="open = true"
  />

  <UModal v-model:open="open" :ui="{ content: 'sm:max-w-4xl' }">
    <template #header>
      <div class="flex items-center gap-2">
        <UIcon name="i-lucide-compass" class="h-5 w-5 text-primary" />
        <h3 class="text-lg font-semibold text-highlighted">Como funciona a priorização</h3>
      </div>
    </template>

    <template #body>
      <div class="space-y-6 p-4 text-sm leading-relaxed text-highlighted">
        <!-- Introdução -->
        <p class="text-muted">
          Realizada com base na <span class="font-medium text-highlighted">matriz Impacto × Esforço</span>, a priorização
          ajuda a comparar épicos por <span class="font-medium text-highlighted">impacto estratégico</span> (Score) e
          <span class="font-medium text-highlighted">esforço</span> (horas). A partir desses dois eixos, cada épico cai em
          um quadrante da <span class="font-medium text-highlighted">Matriz</span>. Abaixo, o detalhe de cada peça.
        </p>

        <!-- ─── SCORE ─────────────────────────────────────────────────── -->
        <section class="space-y-3">
          <div class="flex items-center gap-2">
            <span class="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">1</span>
            <h4 class="text-base font-semibold text-highlighted">Score — o impacto estratégico</h4>
          </div>
          <p>
            <span class="font-medium text-highlighted">Utilizamos uma adaptação do framework RICE.</span>
            O Score é a soma, <span class="font-medium">KPI a KPI</span>, de três fatores multiplicados:
          </p>
          <div class="rounded-lg border border-primary/30 bg-primary/5 px-3 py-2 text-center font-semibold text-highlighted">
            Score = Σ ( Peso do tipo × Confiança × Magnitude )
          </div>

          <!-- Fator: peso do tipo -->
          <div class="space-y-1.5">
            <p class="font-medium text-highlighted">a) Peso do tipo de KPI</p>
            <p class="text-muted">KPIs de negócio movem mais as alavancas do que os de produto:</p>
            <div class="flex flex-wrap gap-2 text-xs">
              <span class="inline-flex items-center gap-1.5 rounded-full border border-default bg-elevated px-2 py-1">
                <span class="h-2 w-2 rounded-full bg-amber-500 dark:bg-amber-400" /> Negócio = <b>2</b>
              </span>
              <span class="inline-flex items-center gap-1.5 rounded-full border border-default bg-elevated px-2 py-1">
                <span class="h-2 w-2 rounded-full bg-slate-400 dark:bg-slate-500" /> Produto = <b>1</b>
              </span>
            </div>
          </div>

          <!-- Fator: confiança -->
          <div class="space-y-1.5">
            <p class="font-medium text-highlighted">b) Confiança</p>
            <p class="text-muted">O quanto acreditamos que o impacto vai se concretizar:</p>
            <div class="flex flex-wrap gap-2 text-xs">
              <span class="rounded-full border border-default bg-elevated px-2 py-1">Alta = <b>3</b></span>
              <span class="rounded-full border border-default bg-elevated px-2 py-1">Média = <b>2</b></span>
              <span class="rounded-full border border-default bg-elevated px-2 py-1">Baixa = <b>1</b></span>
            </div>
          </div>

          <!-- Fator: magnitude -->
          <div class="space-y-1.5">
            <p class="font-medium text-highlighted">c) Magnitude — o tamanho do impacto</p>
            <p class="text-muted">
              Compara o impacto estimado com o <span class="font-medium text-highlighted">maior impacto do mesmo KPI
              entre os épicos deste escopo</span> (Time/Quarter selecionados), numa escala de 0 a 1:
            </p>
            <div class="rounded-lg border border-default bg-elevated/40 px-3 py-2 text-center font-medium">
              Magnitude = impacto estimado ÷ maior impacto do mesmo KPI no escopo
            </div>
            <p class="text-muted">
              Assim, dois épicos com o <span class="font-medium text-highlighted">mesmo KPI</span> mas promessas diferentes
              recebem scores diferentes — quem promete mais, pontua mais. A comparação é sempre dentro do mesmo KPI
              (não misturamos R$ com nº de lojas, por exemplo).
            </p>
            <div class="overflow-x-auto">
              <table class="w-full min-w-[26rem] border-collapse text-xs">
                <thead>
                  <tr class="border-b border-default text-left text-muted">
                    <th class="py-1.5 pr-3 font-medium">Épico</th>
                    <th class="py-1.5 pr-3 font-medium">KPI</th>
                    <th class="py-1.5 pr-3 font-medium">Impacto estimado</th>
                    <th class="py-1.5 font-medium">Magnitude</th>
                  </tr>
                </thead>
                <tbody class="text-highlighted">
                  <tr class="border-b border-default/60">
                    <td class="py-1.5 pr-3">A</td><td class="py-1.5 pr-3">MRR</td>
                    <td class="py-1.5 pr-3">+ R$ 15.000</td><td class="py-1.5 font-semibold">1,00 <span class="font-normal text-muted">(o maior do escopo)</span></td>
                  </tr>
                  <tr>
                    <td class="py-1.5 pr-3">B</td><td class="py-1.5 pr-3">MRR</td>
                    <td class="py-1.5 pr-3">+ R$ 5.000</td><td class="py-1.5 font-semibold">0,33</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <p class="rounded-md border border-amber-500/30 bg-amber-500/5 px-2.5 py-1.5 text-xs text-muted">
              <UIcon name="i-lucide-triangle-alert" class="mr-1 inline h-3.5 w-3.5 text-amber-600 dark:text-amber-400" />
              <b class="text-highlighted">Impacto não preenchido</b> conta como <b class="text-highlighted">magnitude 0</b> — sinaliza que falta informação para priorizar aquele item.
            </p>
          </div>

          <!-- Exemplo completo -->
          <div class="rounded-lg border border-default bg-elevated/40 px-3 py-2.5 text-xs">
            <p class="mb-1 font-semibold text-highlighted">Exemplo completo</p>
            <p class="text-muted">Épico com KPI de <b class="text-highlighted">MRR (Negócio)</b>, confiança <b class="text-highlighted">Alta</b>, impacto <b class="text-highlighted">+ R$ 5.000</b> (metade do maior do escopo):</p>
            <p class="mt-1 font-mono text-highlighted">2 × 3 × 0,5 = <b>3,0</b></p>
            <p class="mt-1 text-muted">O mesmo épico, mas com o maior impacto de MRR do escopo:</p>
            <p class="mt-1 font-mono text-highlighted">2 × 3 × 1,0 = <b>6,0</b></p>
          </div>
          <p class="flex items-center gap-1.5 text-xs text-muted">
            <UIcon name="i-lucide-mouse-pointer-click" class="h-3.5 w-3.5" />
            Passe o mouse sobre o Score na lista para ver o cálculo detalhado de cada KPI.
          </p>
        </section>

        <!-- ─── ESFORÇO ───────────────────────────────────────────────── -->
        <section class="space-y-2">
          <div class="flex items-center gap-2">
            <span class="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">2</span>
            <h4 class="text-base font-semibold text-highlighted">Esforço — o custo em horas</h4>
          </div>
          <p class="text-muted">O esforço é o total de horas do épico:</p>
          <ul class="ml-1 space-y-1">
            <li class="flex gap-2"><span class="text-primary">•</span> <span><b class="text-highlighted">Épico simples</b>: usa as próprias horas.</span></li>
            <li class="flex gap-2"><span class="text-primary">•</span> <span><b class="text-highlighted">Épico com demandas</b>: soma as horas das demandas-filhas que estão no escopo.</span></li>
          </ul>
          <p class="rounded-md border border-red-500/30 bg-red-500/5 px-2.5 py-1.5 text-xs text-muted">
            <UIcon name="i-lucide-triangle-alert" class="mr-1 inline h-3.5 w-3.5 text-red-600 dark:text-red-400" />
            Épicos com <b class="text-highlighted">0 h</b> aparecem em <span class="font-semibold text-red-600 dark:text-red-400">vermelho</span> — falta estimar o esforço para priorizar direito.
          </p>
        </section>

        <!-- ─── MATRIZ ────────────────────────────────────────────────── -->
        <section class="space-y-3">
          <div class="flex items-center gap-2">
            <span class="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">3</span>
            <h4 class="text-base font-semibold text-highlighted">Matriz — impacto × esforço</h4>
          </div>
          <p class="text-muted">
            Cruzamos o <b class="text-highlighted">impacto (Score)</b> com o <b class="text-highlighted">esforço (horas)</b>,
            comparando cada épico com as <b class="text-highlighted">medianas do escopo filtrado</b> — ou seja, "alto" e "baixo"
            são relativos ao conjunto que você está olhando, não a um número fixo.
          </p>
          <div class="grid gap-2 sm:grid-cols-2">
            <div class="rounded-lg border border-emerald-500/30 bg-emerald-500/5 p-2.5">
              <p class="text-sm font-semibold text-emerald-700 dark:text-emerald-300">Ganhos rápidos <span class="font-normal text-muted">(Quick Wins)</span></p>
              <p class="text-xs text-muted">Alto impacto, baixo esforço. Faça primeiro.</p>
            </div>
            <div class="rounded-lg border border-blue-500/30 bg-blue-500/5 p-2.5">
              <p class="text-sm font-semibold text-blue-700 dark:text-blue-300">Grandes apostas <span class="font-normal text-muted">(Big Bets)</span></p>
              <p class="text-xs text-muted">Alto impacto, alto esforço. Vale, mas planeje bem.</p>
            </div>
            <div class="rounded-lg border border-default bg-elevated/40 p-2.5">
              <p class="text-sm font-semibold text-highlighted">Quando possível <span class="font-normal text-muted">(Fill-ins)</span></p>
              <p class="text-xs text-muted">Baixo impacto, baixo esforço. Encaixe nas folgas.</p>
            </div>
            <div class="rounded-lg border border-red-500/30 bg-red-500/5 p-2.5">
              <p class="text-sm font-semibold text-red-700 dark:text-red-300">Evitar / repensar <span class="font-normal text-muted">(Money Pit)</span></p>
              <p class="text-xs text-muted">Baixo impacto, alto esforço. Questione antes de investir.</p>
            </div>
          </div>
        </section>

        <!-- ─── ITENS COM/SEM KPI ─────────────────────────────────────── -->
        <section class="space-y-3">
          <div class="flex items-center gap-2">
            <span class="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">4</span>
            <h4 class="text-base font-semibold text-highlighted">Regras dos itens com e sem KPI</h4>
          </div>

          <div class="space-y-2">
            <p class="font-medium text-highlighted">Com KPI</p>
            <p class="text-muted">O Score é calculado normalmente (regra acima), e o épico é posicionado na Matriz.</p>
          </div>

          <div class="space-y-2">
            <p class="font-medium text-highlighted">Sem KPI (ainda pendente)</p>
            <ul class="ml-1 space-y-1 text-muted">
              <li class="flex gap-2"><span class="text-primary">•</span> Entra com <b class="text-highlighted">Score 0</b> e conta nas medianas (Score 0 é sempre impacto baixo).</li>
              <li class="flex gap-2"><span class="text-primary">•</span> Aparece marcado em <span class="font-semibold text-red-600 dark:text-red-400">vermelho</span> como "Sem KPI" — é um dado inconsistente para priorizar, que pede atenção.</li>
            </ul>
          </div>

          <div class="space-y-2">
            <p class="font-medium text-highlighted">Marcado como "sem KPI" (decisão consciente)</p>
            <p class="text-muted">Quando o épico é marcado como sem KPI, a <b class="text-highlighted">Classificação da demanda sem KPI</b> define o comportamento:</p>
            <div class="space-y-1.5">
              <div class="flex items-start gap-2">
                <span class="mt-0.5 shrink-0 rounded-full bg-violet-500/15 px-2 py-0.5 text-[11px] font-semibold text-violet-700 dark:text-violet-300">Técnico</span>
                <p class="text-xs text-muted"><b class="text-highlighted">Não tem Score</b> (—) e <b class="text-highlighted">não compete com a matriz</b>; priorizar conforme o <b class="text-highlighted">% do capacity</b> estabelecido.</p>
              </div>
              <div class="flex items-start gap-2">
                <span class="mt-0.5 shrink-0 rounded-full bg-amber-500/15 px-2 py-0.5 text-[11px] font-semibold text-amber-700 dark:text-amber-300">Mandatório</span>
                <p class="text-xs text-muted"><b class="text-highlighted">Não tem Score</b> (—) e <b class="text-highlighted">não compete com a matriz</b>; priorizar conforme o <b class="text-highlighted">prazo da obrigatoriedade</b>.</p>
              </div>
              <div class="flex items-start gap-2">
                <span class="mt-0.5 shrink-0 rounded-full bg-elevated px-2 py-0.5 text-[11px] font-semibold text-muted">Outras</span>
                <p class="text-xs text-muted">Ex.: Relacionamento. Entram com <b class="text-highlighted">Score 0</b> e contam nas medianas, como um item sem KPI comum.</p>
              </div>
            </div>
          </div>

          <p class="rounded-md border border-default bg-elevated/40 px-2.5 py-1.5 text-xs text-muted">
            <UIcon name="i-lucide-info" class="mr-1 inline h-3.5 w-3.5" />
            Itens <b class="text-highlighted">sem Score</b> (Técnico e Mandatório) ficam de fora do cálculo das medianas, para não distorcer o "alto/baixo" dos épicos que têm impacto medido.
          </p>
        </section>
      </div>
    </template>

    <template #footer>
      <div class="flex w-full justify-end">
        <UButton label="Entendi" color="primary" @click="open = false" />
      </div>
    </template>
  </UModal>
</template>
