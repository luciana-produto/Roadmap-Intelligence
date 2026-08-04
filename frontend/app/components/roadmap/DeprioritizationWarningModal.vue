<script setup lang="ts">
import type { DeprioritizationGuardAction, DeprioritizationGuardContext } from '~/composables/useDeprioritizationGuard'

const props = defineProps<{
  open: boolean
  context: DeprioritizationGuardContext | null
}>()

const emit = defineEmits<{
  (e: 'action', action: DeprioritizationGuardAction): void
}>()

const itemCount = computed(() => props.context?.items.length ?? 0)
const isBulk = computed(() => itemCount.value > 1)
</script>

<template>
  <UModal
    :open="open"
    title="Demanda despriorizada"
    :ui="{ content: 'sm:max-w-lg' }"
    @update:open="(val) => { if (!val) emit('action', 'cancel') }"
  >
    <template #body>
      <p class="text-sm text-muted">
        <template v-if="isBulk">
          {{ itemCount }} itens selecionados estão despriorizados no quarter atual.
          Alterar fará com que o histórico e trade-off de despriorização sejam perdidos.
          Você pode optar por copiar os itens para um novo quarter, mantendo o registro atual.
        </template>
        <template v-else>
          A demanda está despriorizada no quarter atual.
          Alterar fará com que o histórico e trade-off de despriorização sejam perdidos.
          Você pode optar por copiar a demanda para um novo quarter, mantendo o registro atual.
        </template>
      </p>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          color="primary"
          label="Confirmar alteração"
          @click="emit('action', 'confirm')"
        />
        <UButton
          variant="outline"
          color="neutral"
          icon="i-lucide-copy"
          label="Copiar demanda"
          @click="emit('action', 'copy')"
        />
        <UButton
          variant="ghost"
          color="neutral"
          label="Cancelar"
          @click="emit('action', 'cancel')"
        />
      </div>
    </template>
  </UModal>
</template>
