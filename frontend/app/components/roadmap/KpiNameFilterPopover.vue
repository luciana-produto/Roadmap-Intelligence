<script setup lang="ts">
// Filtro compacto de KPI por nome, usado no cabeçalho dos cards do dashboard.
// v-model = ids selecionados (vazio = todos).
const props = defineProps<{
  modelValue: string[]
  options: { id: string, name: string }[]
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string[]] }>()

const label = computed(() => {
  if (!props.modelValue.length)
    return 'Todos os KPIs'
  if (props.modelValue.length === 1)
    return props.options.find(o => o.id === props.modelValue[0])?.name ?? '1 KPI'
  return `${props.modelValue.length} KPIs`
})

function clear() {
  emit('update:modelValue', [])
}
function toggle(id: string) {
  emit('update:modelValue', props.modelValue.includes(id)
    ? props.modelValue.filter(x => x !== id)
    : [...props.modelValue, id])
}
</script>

<template>
  <UPopover :content="{ side: 'bottom', align: 'end', sideOffset: 6 }">
    <button class="flex max-w-[11rem] items-center gap-1 rounded-md border border-default bg-default px-2 py-1 text-xs text-highlighted transition-colors hover:border-primary/40">
      <UIcon name="i-lucide-filter" class="h-3 w-3 shrink-0 text-muted" />
      <span class="truncate">{{ label }}</span>
      <UIcon name="i-lucide-chevron-down" class="h-3 w-3 shrink-0 text-muted" />
    </button>
    <template #content>
      <div class="max-h-64 min-w-[220px] overflow-y-auto py-1">
        <button
          class="flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors hover:bg-elevated"
          :class="modelValue.length === 0 ? 'font-medium text-primary' : 'text-highlighted'"
          @click="clear"
        >
          <UIcon v-if="modelValue.length === 0" name="i-lucide-check" class="h-3.5 w-3.5 shrink-0" />
          <span v-else class="inline-block h-3.5 w-3.5 shrink-0" />
          Todos os KPIs
        </button>
        <button
          v-for="opt in options"
          :key="opt.id"
          class="flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors hover:bg-elevated"
          :class="modelValue.includes(opt.id) ? 'text-primary' : 'text-highlighted'"
          @click="toggle(opt.id)"
        >
          <UIcon v-if="modelValue.includes(opt.id)" name="i-lucide-check" class="h-3.5 w-3.5 shrink-0 text-primary" />
          <span v-else class="inline-block h-3.5 w-3.5 shrink-0" />
          <span class="truncate">{{ opt.name }}</span>
        </button>
      </div>
    </template>
  </UPopover>
</template>
