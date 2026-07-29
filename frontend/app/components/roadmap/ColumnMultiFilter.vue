<script setup lang="ts">
// Filtro multi-seleção compacto para cabeçalho de coluna (ícone de funil).
// v-model = valores selecionados (vazio = todos).
const props = defineProps<{
  modelValue: string[]
  options: { value: string, label: string }[]
  allLabel?: string
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string[]] }>()

const active = computed(() => props.modelValue.length > 0)

function clear() {
  emit('update:modelValue', [])
}
function toggle(value: string) {
  emit('update:modelValue', props.modelValue.includes(value)
    ? props.modelValue.filter(v => v !== value)
    : [...props.modelValue, value])
}
</script>

<template>
  <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 6 }">
    <button
      type="button"
      class="flex h-5 items-center gap-0.5 rounded border px-1 transition-colors"
      :class="active ? 'border-primary/50 bg-primary/10 text-primary' : 'border-default text-muted hover:border-primary/40 hover:text-highlighted'"
      :title="active ? `${modelValue.length} selecionado(s)` : 'Filtrar'"
    >
      <UIcon name="i-lucide-list-filter" class="h-3 w-3" />
      <span v-if="active" class="text-[10px] font-semibold">{{ modelValue.length }}</span>
    </button>
    <template #content>
      <div class="max-h-64 min-w-[180px] overflow-y-auto py-1" @click.stop>
        <button
          class="flex w-full items-center gap-2 px-3 py-1.5 text-left text-sm transition-colors hover:bg-elevated"
          :class="modelValue.length === 0 ? 'font-medium text-primary' : 'text-highlighted'"
          @click="clear"
        >
          <UIcon v-if="modelValue.length === 0" name="i-lucide-check" class="h-3.5 w-3.5 shrink-0" />
          <span v-else class="inline-block h-3.5 w-3.5 shrink-0" />
          {{ allLabel ?? 'Todos' }}
        </button>
        <button
          v-for="opt in options"
          :key="opt.value"
          class="flex w-full items-center gap-2 px-3 py-1.5 text-left text-sm transition-colors hover:bg-elevated"
          :class="modelValue.includes(opt.value) ? 'text-primary' : 'text-highlighted'"
          @click="toggle(opt.value)"
        >
          <UIcon v-if="modelValue.includes(opt.value)" name="i-lucide-check" class="h-3.5 w-3.5 shrink-0 text-primary" />
          <span v-else class="inline-block h-3.5 w-3.5 shrink-0" />
          <span class="truncate">{{ opt.label }}</span>
        </button>
        <p v-if="!options.length" class="px-3 py-1.5 text-xs text-muted">Sem opções.</p>
      </div>
    </template>
  </UPopover>
</template>
