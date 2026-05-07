<script setup lang="ts">
const { logout } = useAuth()
const authStore = useAuthStore()
const route = useRoute()
const colorMode = useColorMode()
const { setPrimary, setNeutral } = useThemePreference()
const appConfig = useAppConfig()
const sidebarOpen = ref(false)
const isSidebarCollapsed = ref(true)

type NavLinkChild = {
  label: string
  to: string
}

type NavLinkItem = {
  label: string
  icon: string
  to?: string
  disabled?: boolean
  badge?: string
  children?: NavLinkChild[]
}

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

const navLinks: NavLinkItem[] = [
  { label: 'Home', icon: 'i-lucide-layout-dashboard', to: '/home' },
  { label: 'Roadmap', icon: 'i-lucide-map', to: '/roadmap' },
  {
    label: 'Cadastros',
    icon: 'i-lucide-package',
    children: [
      { label: 'Projetos e Produtos', to: '/products' },
      { label: 'KPIs', to: '/kpis' }
    ]
  },
  { label: 'Indicadores', icon: 'i-lucide-trending-up', disabled: true, badge: 'Em breve' }
]

const expandedNavGroups = ref<Record<string, boolean>>({
  Cadastros: route.path.startsWith('/products') || route.path.startsWith('/kpis')
})

watch(() => route.path, (path) => {
  if (path.startsWith('/products') || path.startsWith('/kpis'))
    expandedNavGroups.value.Cadastros = true
})

function isNavLinkActive(link: NavLinkItem) {
  if (link.to)
    return route.path === link.to

  return link.children?.some(child => route.path === child.to) ?? false
}

function isNavGroupExpanded(label: string) {
  return expandedNavGroups.value[label] ?? false
}

function toggleNavGroup(label: string) {
  if (isSidebarCollapsed.value) {
    isSidebarCollapsed.value = false
    expandedNavGroups.value[label] = true
    return
  }

  expandedNavGroups.value[label] = !isNavGroupExpanded(label)
}

function handleMobileNavGroupToggle(label: string) {
  expandedNavGroups.value[label] = !isNavGroupExpanded(label)
}

const sidebarStyle = computed(() => `background-color: var(--color-${appConfig.ui.colors.primary}-950);`)
const desktopSidebarClasses = computed(() =>
  isSidebarCollapsed.value ? 'md:w-16' : 'md:w-72'
)
const mainContentWidthClass = computed(() =>
  route.path.startsWith('/roadmap') ? 'max-w-none' : 'max-w-[1600px]'
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
        class="flex border-b border-white/10"
        :class="isSidebarCollapsed ? 'justify-center px-2 py-3' : 'items-center gap-3 justify-between px-4 py-4'"
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

      <nav class="flex-1 space-y-1 overflow-y-auto" :class="isSidebarCollapsed ? 'px-2 py-3' : 'px-3 py-4'">
        <template v-for="link in navLinks" :key="link.label">
          <button
            v-if="link.disabled"
            type="button"
            :title="isSidebarCollapsed ? (link.badge ? `${link.label} · ${link.badge}` : link.label) : undefined"
            class="flex rounded-xl text-sm transition-colors"
            :class="[
              isSidebarCollapsed ? 'justify-center px-2 py-2.5' : 'items-center gap-3 px-3 py-2.5',
              'cursor-not-allowed text-white/40'
            ]"
            disabled
          >
            <UIcon :name="link.icon" class="w-4 h-4 shrink-0" />
            <span v-if="!isSidebarCollapsed" class="flex min-w-0 items-center gap-2">
              <span>{{ link.label }}</span>
              <UBadge
                v-if="link.badge"
                size="xs"
                color="neutral"
                variant="solid"
                class="shrink-0 bg-white/12 text-white/80"
              >
                {{ link.badge }}
              </UBadge>
            </span>
          </button>

          <template v-else-if="link.children?.length">
            <button
              type="button"
              :title="isSidebarCollapsed ? link.label : undefined"
              class="flex w-full rounded-xl text-sm transition-colors"
              :class="[
                isSidebarCollapsed ? 'justify-center px-2 py-2.5' : 'items-center gap-3 px-3 py-2.5',
                isNavLinkActive(link)
                  ? 'bg-white/12 text-white'
                  : 'text-white/75 hover:bg-white/8 hover:text-white'
              ]"
              @click="toggleNavGroup(link.label)"
            >
              <UIcon :name="link.icon" class="w-4 h-4 shrink-0" />
              <template v-if="!isSidebarCollapsed">
                <span class="min-w-0 flex-1 text-left">{{ link.label }}</span>
                <UIcon
                  name="i-lucide-chevron-down"
                  class="h-4 w-4 shrink-0 transition-transform"
                  :class="isNavGroupExpanded(link.label) ? 'rotate-180' : ''"
                />
              </template>
            </button>

            <div
              v-if="!isSidebarCollapsed && isNavGroupExpanded(link.label)"
              class="mt-1 space-y-1 pl-4"
            >
              <NuxtLink
                v-for="child in link.children"
                :key="child.to"
                :to="child.to"
                class="flex items-center rounded-lg px-3 py-2 text-sm transition-colors"
                :class="route.path === child.to
                  ? 'bg-white/10 text-white'
                  : 'text-white/65 hover:bg-white/6 hover:text-white'"
              >
                <span>{{ child.label }}</span>
              </NuxtLink>
            </div>
          </template>

          <NuxtLink
            v-else
            :to="link.to"
            :title="isSidebarCollapsed ? link.label : undefined"
            class="flex rounded-xl text-sm transition-colors"
            :class="[
              isSidebarCollapsed ? 'justify-center px-2 py-2.5' : 'items-center gap-3 px-3 py-2.5',
              isNavLinkActive(link)
                ? 'bg-white/12 text-white'
                : 'text-white/75 hover:bg-white/8 hover:text-white'
            ]"
          >
            <UIcon :name="link.icon" class="w-4 h-4 shrink-0" />
            <span v-if="!isSidebarCollapsed">{{ link.label }}</span>
          </NuxtLink>
        </template>
      </nav>

      <div class="border-t border-white/10" :class="isSidebarCollapsed ? 'px-2 py-3' : 'px-3 py-3'">
        <UDropdownMenu :items="userMenuItems">
          <button
            class="w-full flex rounded-xl text-left text-white/85 hover:bg-white/8 transition-colors"
            :class="isSidebarCollapsed ? 'justify-center px-2 py-2.5' : 'items-center gap-3 px-3 py-2.5'"
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
        <div class="mx-auto w-full" :class="mainContentWidthClass">
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
          <template v-for="link in navLinks" :key="link.label">
            <button
              v-if="link.disabled"
              type="button"
              class="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-white/40"
              disabled
            >
              <UIcon :name="link.icon" class="w-4 h-4 shrink-0" />
              <span class="flex min-w-0 items-center gap-2">
                <span>{{ link.label }}</span>
                <UBadge
                  v-if="link.badge"
                  size="xs"
                  color="neutral"
                  variant="solid"
                  class="shrink-0 bg-white/12 text-white/80"
                >
                  {{ link.badge }}
                </UBadge>
              </span>
            </button>

            <template v-else-if="link.children?.length">
              <button
                type="button"
                class="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm transition-colors"
                :class="isNavLinkActive(link)
                  ? 'bg-white/12 text-white'
                  : 'text-white/75 hover:bg-white/8 hover:text-white'"
                @click="handleMobileNavGroupToggle(link.label)"
              >
                <UIcon :name="link.icon" class="w-4 h-4 shrink-0" />
                <span class="min-w-0 flex-1 text-left">{{ link.label }}</span>
                <UIcon
                  name="i-lucide-chevron-down"
                  class="h-4 w-4 shrink-0 transition-transform"
                  :class="isNavGroupExpanded(link.label) ? 'rotate-180' : ''"
                />
              </button>

              <div v-if="isNavGroupExpanded(link.label)" class="mt-1 space-y-1 pl-4">
                <NuxtLink
                  v-for="child in link.children"
                  :key="child.to"
                  :to="child.to"
                  class="flex items-center rounded-lg px-3 py-2 text-sm transition-colors"
                  :class="route.path === child.to
                    ? 'bg-white/10 text-white'
                    : 'text-white/65 hover:bg-white/6 hover:text-white'"
                  @click="sidebarOpen = false"
                >
                  <span>{{ child.label }}</span>
                </NuxtLink>
              </div>
            </template>

            <NuxtLink
              v-else
              :to="link.to"
              class="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm transition-colors"
              :class="isNavLinkActive(link) ? 'bg-white/12 text-white' : 'text-white/75 hover:bg-white/8 hover:text-white'"
              @click="sidebarOpen = false"
            >
              <UIcon :name="link.icon" class="w-4 h-4 shrink-0" />
              <span>{{ link.label }}</span>
            </NuxtLink>
          </template>
        </nav>
      </aside>
    </div>
  </div>
</template>
