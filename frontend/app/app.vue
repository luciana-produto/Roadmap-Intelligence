<script setup lang="ts">
useHead({
  meta: [
    { name: 'viewport', content: 'width=device-width, initial-scale=1' }
  ],
  link: [
    { rel: 'preconnect', href: 'https://fonts.googleapis.com' },
    { rel: 'stylesheet', href: 'https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@300;400;500;600;700;800;900&display=swap' }
  ],
  htmlAttrs: { lang: 'pt-BR' }
})

useSeoMeta({
  title: 'ProductHub',
  description: 'Gestão de Roadmap e KPIs de Produtos.',
  ogTitle: 'ProductHub',
  ogDescription: 'Gestão de Roadmap e KPIs de Produtos.'
})
</script>

<template>
  <UApp>
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </UApp>
</template>
