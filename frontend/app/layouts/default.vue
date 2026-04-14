<script setup lang="ts">
const { logout } = useAuth()
const authStore = useAuthStore()
const route = useRoute()
const colorMode = useColorMode()
const { setPrimary, setNeutral } = useThemePreference()
const appConfig = useAppConfig()
const sidebarOpen = ref(false)
const isSidebarCollapsed = ref(true)

const userInitials = computed(() => {
  if (!authStore.user) return '?'
  const first = authStore.user.firstName?.[0] ?? ''
  const last = authStore.user.lastName?.[0] ?? ''
  return `${first}${last}`.toUpperCase()
})

const primaryColors = [
  'red', 'orange', 'amber', 'yellow', 'lime', 'green', 'emerald', 'teal',
  'cyan', 'sky', 'blue', 'indigo', 'violet', 'purple', 'fuchsia', 'pink', 'rose'
]
const neutralColors = ['slate', 'gray', 'zinc', 'neutral', 'stone']

const userMenuItems = computed(() => [[
  {
    label: 'Tema',
    icon: 'i-lucide-palette',
    children: [
      {
        label: 'Principal',
        slot: 'chip',
        chip: appConfig.ui.colors.primary,
        content: { align: 'center', collisionPadding: 16 },
        children: primaryColors.map(color => ({
          label: color.charAt(0).toUpperCase() + color.slice(1),
          chip: color,
          slot: 'chip',
          type: 'checkbox',
          checked: appConfig.ui.colors.primary === color,
          onSelect: (e: Event) => { e.preventDefault(); setPrimary(color) }
        }))
      },
      {
        label: 'Neutro',
        slot: 'chip',
        chip: appConfig.ui.colors.neutral === 'neutral' ? 'stone' : appConfig.ui.colors.neutral,
        content: { align: 'end', collisionPadding: 16 },
        children: neutralColors.map(color => ({
          label: color.charAt(0).toUpperCase() + color.slice(1),
          chip: color === 'neutral' ? 'stone' : color,
          slot: 'chip',
          type: 'checkbox',
          checked: appConfig.ui.colors.neutral === color,
          onSelect: (e: Event) => { e.preventDefault(); setNeutral(color) }
        }))
      }
    ]
  },
  {
    label: 'Aparência',
    icon: 'i-lucide-sun-moon',
    children: [
      {
        label: 'Claro',
        icon: 'i-lucide-sun',
        type: 'checkbox',
        checked: colorMode.value === 'light',
        onSelect: (e: Event) => { e.preventDefault(); colorMode.preference = 'light' }
      },
      {
        label: 'Escuro',
        icon: 'i-lucide-moon',
        type: 'checkbox',
        checked: colorMode.value === 'dark',
        onSelect: (e: Event) => { e.preventDefault(); colorMode.preference = 'dark' }
      }
    ]
  }
], [
  {
    label: 'Sair',
    icon: 'i-lucide-log-out',
    color: 'error' as const,
    onSelect: logout
  }
]])

const navLinks = [
  { label: 'Home', icon: 'i-lucide-layout-dashboard', to: '/home' },
  { label: 'Roadmap', icon: 'i-lucide-map', to: '/roadmap' },
  { label: 'Produtos', icon: 'i-lucide-package', to: '/products' },
  { label: 'KPIs', icon: 'i-lucide-bar-chart-2', to: '/kpis' },
  { label: 'Indicadores', icon: 'i-lucide-trending-up', to: '/indicators' }
]

const sidebarStyle = computed(() => `background-color: var(--color-${appConfig.ui.colors.primary}-950);`)
const desktopSidebarClasses = computed(() =>
  isSidebarCollapsed.value ? 'md:w-20' : 'md:w-72'
)
</script>

<template>
  <div class="min-h-screen bg-default md:flex">
    <aside
      class="relative hidden md:flex md:shrink-0 md:flex-col md:border-r md:border-white/10 dark transition-[width] duration-200"
      :class="desktopSidebarClasses"
      :style="sidebarStyle"
    >
      <div
        class="flex px-4 py-4 border-b border-white/10"
        :class="isSidebarCollapsed ? 'justify-center' : 'items-center gap-3 justify-between'"
      >
        <div class="flex items-center gap-3 min-w-0">
          <div class="w-10 h-10 rounded-xl bg-white/10 flex items-center justify-center shrink-0">
            <UIcon name="i-lucide-package" class="w-5 h-5 text-white" />
          </div>
          <div v-if="!isSidebarCollapsed" class="min-w-0">
            <p class="text-sm font-bold tracking-wide text-white">ProductHub</p>
            <p class="text-xs text-white/70">Gestão de portfólio</p>
          </div>
        </div>
        <UButton
          v-if="!isSidebarCollapsed"
          icon="i-lucide-panel-left-close"
          variant="ghost"
          color="neutral"
          class="text-white/80 hover:text-white"
          @click="isSidebarCollapsed = true"
        />
        <UButton
          v-else
          icon="i-lucide-panel-left-open"
          variant="ghost"
          color="neutral"
          class="absolute top-4 -right-4 z-10 rounded-full border border-white/10 bg-neutral-950 text-white shadow-lg hover:bg-neutral-900"
          @click="isSidebarCollapsed = false"
        />
      </div>

      <nav class="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
        <NuxtLink
          v-for="link in navLinks"
          :key="link.to"
          :to="link.to"
          :title="isSidebarCollapsed ? link.label : undefined"
          class="flex rounded-xl px-3 py-2.5 text-sm transition-colors"
          :class="[
            isSidebarCollapsed ? 'justify-center' : 'items-center gap-3',
            route.path === link.to
              ? 'bg-white/12 text-white'
              : 'text-white/75 hover:bg-white/8 hover:text-white'
          ]"
        >
          <UIcon :name="link.icon" class="w-4 h-4 shrink-0" />
          <span v-if="!isSidebarCollapsed">{{ link.label }}</span>
        </NuxtLink>
      </nav>

      <div class="px-3 py-3 border-t border-white/10">
        <UDropdownMenu :items="userMenuItems">
          <button
            class="w-full flex rounded-xl px-3 py-2.5 text-left text-white/85 hover:bg-white/8 transition-colors"
            :class="isSidebarCollapsed ? 'justify-center' : 'items-center gap-3'"
          >
            <UAvatar :text="userInitials" size="sm" class="shrink-0" />
            <div v-if="!isSidebarCollapsed" class="min-w-0 flex-1">
              <p class="truncate text-sm font-medium text-white">
                {{ authStore.user?.firstName }} {{ authStore.user?.lastName }}
              </p>
              <p class="truncate text-xs text-white/60">Conta</p>
            </div>
            <UIcon v-if="!isSidebarCollapsed" name="i-lucide-chevrons-up-down" class="w-4 h-4 text-white/60" />
          </button>
        </UDropdownMenu>
      </div>
    </aside>

    <div class="min-w-0 flex-1 flex flex-col">
      <header class="md:hidden border-b border-default bg-elevated/80 backdrop-blur sticky top-0 z-40">
        <div class="px-4 h-14 flex items-center gap-3">
          <UButton
            icon="i-lucide-menu"
            variant="ghost"
            color="neutral"
            @click="sidebarOpen = true"
          />
          <NuxtLink to="/home" class="flex items-center gap-2 min-w-0">
            <div class="w-7 h-7 rounded-lg bg-primary flex items-center justify-center">
              <UIcon name="i-lucide-package" class="w-4 h-4 text-white" />
            </div>
            <span class="font-bold text-sm tracking-wide text-highlighted">ProductHub</span>
          </NuxtLink>
          <div class="flex-1" />
          <UDropdownMenu :items="userMenuItems">
            <UAvatar :text="userInitials" size="sm" class="cursor-pointer" />
          </UDropdownMenu>
        </div>
      </header>

      <main class="flex-1 w-full px-4 py-5 md:px-6 md:py-6">
        <div class="mx-auto w-full max-w-[1600px]">
          <slot />
        </div>
      </main>
    </div>

    <div v-if="sidebarOpen" class="md:hidden fixed inset-0 z-50 flex">
      <button class="absolute inset-0 bg-black/40" @click="sidebarOpen = false" />
      <aside class="relative z-10 flex h-full w-72 flex-col bg-neutral-950 text-white shadow-xl">
        <div class="flex items-center justify-between px-4 py-4 border-b border-white/10">
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 rounded-xl bg-white/10 flex items-center justify-center">
              <UIcon name="i-lucide-package" class="w-5 h-5 text-white" />
            </div>
            <div>
              <p class="text-sm font-bold tracking-wide text-white">ProductHub</p>
              <p class="text-xs text-white/70">Gestão de portfólio</p>
            </div>
          </div>
          <UButton icon="i-lucide-x" variant="ghost" color="neutral" @click="sidebarOpen = false" />
        </div>
        <nav class="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
          <NuxtLink
            v-for="link in navLinks"
            :key="link.to"
            :to="link.to"
            class="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm transition-colors"
            :class="route.path === link.to ? 'bg-white/12 text-white' : 'text-white/75 hover:bg-white/8 hover:text-white'"
            @click="sidebarOpen = false"
          >
            <UIcon :name="link.icon" class="w-4 h-4 shrink-0" />
            <span>{{ link.label }}</span>
          </NuxtLink>
        </nav>
      </aside>
    </div>
  </div>
</template>
