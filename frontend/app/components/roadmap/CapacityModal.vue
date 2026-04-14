<script setup lang="ts">
import type { CapacityFormData } from '~/types/roadmap'

const props = defineProps<{
  open: boolean
  projectName?: string
  quarterLabel?: string
  initialValue?: CapacityFormData | null
  isSaving?: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  submit: [data: CapacityFormData]
}>()

const form = reactive<CapacityFormData>({
  projectId: '',
  quarterYear: new Date().getFullYear(),
  quarterNumber: 1,
  capacityHours: 0,
  observation: ''
})

watch(() => props.open, (open) => {
  if (!open || !props.initialValue) return

  form.projectId = props.initialValue.projectId
  form.quarterYear = props.initialValue.quarterYear
  form.quarterNumber = props.initialValue.quarterNumber
  form.capacityHours = props.initialValue.capacityHours
  form.observation = props.initialValue.observation ?? ''
})

const isSubmitDisabled = computed(() => !form.projectId || form.capacityHours <= 0)

function handleSubmit() {
  if (isSubmitDisabled.value) return

  emit('submit', {
    projectId: form.projectId,
    quarterYear: form.quarterYear,
    quarterNumber: form.quarterNumber,
    capacityHours: form.capacityHours,
    observation: form.observation?.trim() || undefined
  })
}
</script>

<template>
  <UModal
    :open="open"
    title="Configurar capacity"
    :description="projectName && quarterLabel ? `${projectName} · ${quarterLabel}` : 'Defina o capacity do quarter em horas.'"
    @update:open="emit('update:open', $event)"
  >
    <template #body>
      <form class="space-y-4" @submit.prevent="handleSubmit">
        <UFormField label="Capacity em horas" required>
          <UInput
            v-model.number="form.capacityHours"
            type="number"
            min="0.5"
            step="0.5"
            placeholder="Ex: 320"
            class="w-full"
            :class="form.capacityHours <= 0 ? 'ring-2 ring-red-400' : ''"
          />
          <p v-if="form.capacityHours <= 0" class="mt-1 text-xs text-red-500">
            Informe um valor maior que zero.
          </p>
        </UFormField>

        <UFormField label="Observação">
          <UTextarea
            v-model="form.observation"
            :rows="3"
            class="w-full"
            placeholder="Opcional: explique o contexto do capacity deste quarter"
          />
        </UFormField>
      </form>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          variant="outline"
          color="neutral"
          label="Cancelar"
          @click="emit('update:open', false)"
        />
        <UButton
          color="primary"
          icon="i-lucide-save"
          :loading="isSaving"
          :disabled="isSubmitDisabled"
          label="Salvar capacity"
          @click="handleSubmit"
        />
      </div>
    </template>
  </UModal>
</template>