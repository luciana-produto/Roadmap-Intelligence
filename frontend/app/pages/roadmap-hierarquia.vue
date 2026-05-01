<script setup lang="ts">
definePageMeta({
  middleware: to => navigateTo({
    path: '/roadmap',
    query: {
      ...to.query,
      view: 'hierarchy'
    }
  }, {
    redirectCode: 301
  })
})
</script>
