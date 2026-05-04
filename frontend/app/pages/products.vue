<script setup lang="ts">
import type { RoadmapProduct, RoadmapProject } from '~/types/roadmap'

useSeoMeta({ title: 'Projetos e Produtos · ProductHub' })

const roadmapStore = useRoadmapStore()
const toast = useToast()

const { projects } = storeToRefs(roadmapStore)

const searchQuery = ref('')
const isSubmitting = ref(false)

const showProjectModal = ref(false)
const editingProject = ref<RoadmapProject | null>(null)
const projectForm = ref(emptyProjectForm())

const showProductModal = ref(false)
const editingProduct = ref<RoadmapProduct | null>(null)
const productParentProject = ref<RoadmapProject | null>(null)
const productForm = ref(emptyProductForm())

const showDeleteProjectModal = ref(false)
const projectToDelete = ref<RoadmapProject | null>(null)

const showDeleteProductModal = ref(false)
const productToDelete = ref<RoadmapProduct | null>(null)

const summary = computed(() => ({
  totalProjects: projects.value.length,
  totalProducts: projects.value.reduce((total, project) => total + project.products.length, 0),
  populatedProjects: projects.value.filter(project => project.products.length > 0).length
}))

const filteredProjects = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  if (!query)
    return projects.value

  return projects.value.filter((project) => {
    const matchesProject = project.name.toLowerCase().includes(query) || project.slug.toLowerCase().includes(query)
    const matchesProduct = project.products.some(product => product.name.toLowerCase().includes(query))
    return matchesProject || matchesProduct
  })
})

onMounted(() => roadmapStore.fetchProjects())

function emptyProjectForm() {
  return {
    name: ''
  }
}

function emptyProductForm() {
  return {
    name: ''
  }
}

function openCreateProject() {
  editingProject.value = null
  projectForm.value = emptyProjectForm()
  showProjectModal.value = true
}

function openEditProject(project: RoadmapProject) {
  editingProject.value = project
  projectForm.value = {
    name: project.name
  }
  showProjectModal.value = true
}

async function submitProject() {
  const payload = {
    name: projectForm.value.name.trim()
  }

  if (!payload.name)
    return

  isSubmitting.value = true
  try {
    if (editingProject.value) {
      await roadmapStore.updateProject(editingProject.value.id, payload)
      toast.add({ title: 'Projeto atualizado', color: 'success' })
    }
    else {
      await roadmapStore.createProject(payload)
      toast.add({ title: 'Projeto criado', color: 'success' })
    }

    showProjectModal.value = false
  }
  catch {
    // handled by useApi
  }
  finally {
    isSubmitting.value = false
  }
}

function openCreateProduct(project: RoadmapProject) {
  productParentProject.value = project
  editingProduct.value = null
  productForm.value = emptyProductForm()
  showProductModal.value = true
}

function openEditProduct(project: RoadmapProject, product: RoadmapProduct) {
  productParentProject.value = project
  editingProduct.value = product
  productForm.value = { name: product.name }
  showProductModal.value = true
}

async function submitProduct() {
  if (!productParentProject.value)
    return

  const payload = { name: productForm.value.name.trim() }
  if (!payload.name)
    return

  isSubmitting.value = true
  try {
    if (editingProduct.value) {
      await roadmapStore.updateProduct(productParentProject.value.id, editingProduct.value.id, payload)
      toast.add({ title: 'Produto atualizado', color: 'success' })
    }
    else {
      await roadmapStore.createProduct(productParentProject.value.id, payload)
      toast.add({ title: 'Produto criado', color: 'success' })
    }

    showProductModal.value = false
  }
  catch {
    // handled by useApi
  }
  finally {
    isSubmitting.value = false
  }
}

function confirmDeleteProject(project: RoadmapProject) {
  projectToDelete.value = project
  showDeleteProjectModal.value = true
}

async function executeDeleteProject() {
  if (!projectToDelete.value)
    return

  isSubmitting.value = true
  try {
    await roadmapStore.deleteProject(projectToDelete.value.id)
    toast.add({ title: 'Projeto removido', color: 'success' })
    showDeleteProjectModal.value = false
    projectToDelete.value = null
  }
  catch {
    // handled by useApi
  }
  finally {
    isSubmitting.value = false
  }
}

function confirmDeleteProduct(project: RoadmapProject, product: RoadmapProduct) {
  productParentProject.value = project
  productToDelete.value = product
  showDeleteProductModal.value = true
}

async function executeDeleteProduct() {
  if (!productParentProject.value || !productToDelete.value)
    return

  isSubmitting.value = true
  try {
    await roadmapStore.deleteProduct(productParentProject.value.id, productToDelete.value.id)
    toast.add({ title: 'Produto removido', color: 'success' })
    showDeleteProductModal.value = false
    productToDelete.value = null
  }
  catch {
    // handled by useApi
  }
  finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between gap-4 flex-wrap">
      <div>
        <h1 class="text-2xl font-bold text-highlighted">Projetos e produtos</h1>
        <p class="text-sm text-muted mt-1">
          Cadastre a estrutura base que abastece o roadmap e os indicadores.
        </p>
      </div>
      <UButton icon="i-lucide-folder-plus" label="Novo projeto" @click="openCreateProject" />
    </div>

    <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      <UCard :ui="{ body: 'p-4' }">
        <div class="text-sm text-muted">Projetos cadastrados</div>
        <div class="text-2xl font-bold text-highlighted">{{ summary.totalProjects }}</div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="text-sm text-muted">Produtos cadastrados</div>
        <div class="text-2xl font-bold text-highlighted">{{ summary.totalProducts }}</div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="text-sm text-muted">Projetos com produtos</div>
        <div class="text-2xl font-bold text-success">{{ summary.populatedProjects }}</div>
      </UCard>
    </div>

    <div class="flex items-center gap-3 flex-wrap">
      <UInput
        v-model="searchQuery"
        icon="i-lucide-search"
        placeholder="Buscar projeto, slug ou produto..."
        class="w-full max-w-md"
      />
    </div>

    <div v-if="filteredProjects.length" class="grid gap-4 xl:grid-cols-2">
      <UCard
        v-for="project in filteredProjects"
        :key="project.id"
        class="h-full"
        :ui="{ body: 'p-5' }"
      >
        <div class="flex items-start justify-between gap-4">
          <div class="min-w-0 space-y-2">
            <div class="flex items-center gap-2 flex-wrap">
              <h2 class="text-lg font-semibold text-highlighted truncate">{{ project.name }}</h2>
              <UBadge color="neutral" variant="subtle">{{ project.products.length }} produtos</UBadge>
            </div>
            <div class="flex items-center gap-2 text-sm text-muted flex-wrap">
              <UIcon name="i-lucide-link-2" class="w-4 h-4" />
              <span>/{{ project.slug }}</span>
            </div>
          </div>

          <div class="flex items-center gap-1 shrink-0">
            <UButton icon="i-lucide-pencil" variant="ghost" size="xs" @click="openEditProject(project)" />
            <UButton icon="i-lucide-trash-2" variant="ghost" color="error" size="xs" @click="confirmDeleteProject(project)" />
          </div>
        </div>

        <div class="mt-5 rounded-2xl border border-default bg-elevated/50">
          <div class="flex items-center justify-between gap-3 border-b border-default px-4 py-3">
            <div>
              <p class="text-sm font-medium text-highlighted">Produtos do projeto</p>
              <p class="text-xs text-muted">Gerencie os itens disponíveis para vincular nas demandas.</p>
            </div>
            <UButton size="xs" icon="i-lucide-plus" label="Novo produto" @click="openCreateProduct(project)" />
          </div>

          <div v-if="project.products.length" class="divide-y divide-default">
            <div
              v-for="product in project.products"
              :key="product.id"
              class="flex items-center justify-between gap-3 px-4 py-3"
            >
              <div class="min-w-0">
                <p class="font-medium text-highlighted truncate">{{ product.name }}</p>
              </div>
              <div class="flex items-center gap-1 shrink-0">
                <UButton icon="i-lucide-pencil" variant="ghost" size="xs" @click="openEditProduct(project, product)" />
                <UButton icon="i-lucide-trash-2" variant="ghost" color="error" size="xs" @click="confirmDeleteProduct(project, product)" />
              </div>
            </div>
          </div>

          <div v-else class="px-4 py-8 text-center text-sm text-muted">
            <UIcon name="i-lucide-package-open" class="mx-auto mb-2 text-2xl" />
            <p>Este projeto ainda não possui produtos cadastrados.</p>
          </div>
        </div>
      </UCard>
    </div>

    <UCard v-else :ui="{ body: 'p-8' }">
      <div class="text-center text-muted">
        <UIcon name="i-lucide-package-search" class="mx-auto mb-3 text-4xl" />
        <p class="text-base font-medium text-highlighted">
          {{ projects.length ? 'Nenhum resultado encontrado.' : 'Nenhum projeto cadastrado ainda.' }}
        </p>
        <p class="mt-1 text-sm">
          {{ projects.length ? 'Ajuste a busca para localizar um projeto ou produto.' : 'Crie o primeiro projeto para começar a popular o banco real.' }}
        </p>
        <UButton v-if="!projects.length" label="Criar projeto" class="mt-4" @click="openCreateProject" />
      </div>
    </UCard>

    <UModal v-model:open="showProjectModal">
      <template #header>
        <h3 class="text-lg font-semibold text-highlighted">
          {{ editingProject ? 'Editar projeto' : 'Novo projeto' }}
        </h3>
      </template>

      <template #body>
        <div class="space-y-4 p-4">
          <UFormField label="Nome" required>
            <UInput v-model="projectForm.name" placeholder="Ex: Plataforma de Pagamentos" class="w-full" />
          </UFormField>

          <p class="text-xs text-muted">
            O identificador interno do projeto agora é gerado automaticamente a partir do nome.
          </p>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton label="Cancelar" variant="ghost" @click="showProjectModal = false" />
          <UButton
            :label="editingProject ? 'Salvar' : 'Criar projeto'"
            :loading="isSubmitting"
            :disabled="!projectForm.name.trim()"
            @click="submitProject"
          />
        </div>
      </template>
    </UModal>

    <UModal v-model:open="showProductModal">
      <template #header>
        <h3 class="text-lg font-semibold text-highlighted">
          {{ editingProduct ? 'Editar produto' : 'Novo produto' }}
        </h3>
      </template>

      <template #body>
        <div class="space-y-4 p-4">
          <div class="rounded-xl border border-default bg-elevated/60 px-3 py-2 text-sm text-muted">
            Projeto vinculado: <strong class="text-highlighted">{{ productParentProject?.name }}</strong>
          </div>

          <UFormField label="Nome" required>
            <UInput v-model="productForm.name" placeholder="Ex: Checkout B2B" class="w-full" />
          </UFormField>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton label="Cancelar" variant="ghost" @click="showProductModal = false" />
          <UButton
            :label="editingProduct ? 'Salvar' : 'Criar produto'"
            :loading="isSubmitting"
            :disabled="!productForm.name.trim()"
            @click="submitProduct"
          />
        </div>
      </template>
    </UModal>

    <UModal v-model:open="showDeleteProjectModal">
      <template #header>
        <h3 class="text-lg font-semibold text-error">Remover projeto</h3>
      </template>

      <template #body>
        <p class="p-4 text-sm text-muted">
          Tem certeza que deseja remover o projeto <strong>{{ projectToDelete?.name }}</strong>?
          A exclusão só será permitida se ele ainda não estiver em uso no roadmap.
        </p>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton label="Cancelar" variant="ghost" @click="showDeleteProjectModal = false" />
          <UButton label="Remover" color="error" :loading="isSubmitting" @click="executeDeleteProject" />
        </div>
      </template>
    </UModal>

    <UModal v-model:open="showDeleteProductModal">
      <template #header>
        <h3 class="text-lg font-semibold text-error">Remover produto</h3>
      </template>

      <template #body>
        <p class="p-4 text-sm text-muted">
          Tem certeza que deseja remover o produto <strong>{{ productToDelete?.name }}</strong>?
          A exclusão só será permitida se ele ainda não estiver vinculado a demandas.
        </p>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton label="Cancelar" variant="ghost" @click="showDeleteProductModal = false" />
          <UButton label="Remover" color="error" :loading="isSubmitting" @click="executeDeleteProduct" />
        </div>
      </template>
    </UModal>
  </div>
</template>